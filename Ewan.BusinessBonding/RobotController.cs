using Ewan.Core;
using Ewan.Core.IO;
using System;
using System.Threading.Tasks;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 机械臂控制器 - 管理机械臂的各种动作
    /// 基于IO表中的机器人IO定义
    /// </summary>
    public class RobotController : BaseManager<RobotController>
    {
        private readonly IOController _ioController;

        // 机械臂IO定义 (基于K1控制卡的输出点)
        private const int Y10_PlaceToBin = 10;    // 触发机械手放置料仓
        private const int Y11_Bin1Select = 11;    // 料仓1选择信号
        private const int Y12_Bin2Select = 12;    // 料仓2选择信号
        private const int Y13_Bin3Select = 13;    // 料仓3选择信号
        private const int Y14_AllowPickFromBelt = 14;  // 触发机械手皮带线允许取料
        private const int Y15_PickCommand = 15;   // 发送取料指令
        private const int Y16_HighSpeed = 16;     // 高速运行
        private const int Y17_PlaceToCart = 17;   // 发送放入小车指令

        // 机械臂输入信号 (基于K1控制卡的输入点)
        private const int X3_DetectMaterial = 3;       // 检测到料片信号
        private const int X4_GripComplete = 4;         // 机械臂抓取完成信号
        private const int X5_LowerCameraPos = 5;       // 下相机精定位完成信号
        private const int X6_PositionComplete = 6;     // 机械臂定位完成信号
        private const int X7_MoveToScanArea = 7;       // 移至扫码区到位信号
        private const int X8_PlaceComplete = 8;        // 机械臂放置完成信号
        private const int X9_Initialize = 9;           // 初始化信号
        private const int X10_PickComplete = 10;       // 机械臂取料完成信号
        private const int X11_InsertCartComplete = 11; // 放入小车完成信号
        private const int X15_RobotAlarm = 15;         // 机械手报警信号
        private const int X19_CylinderAlarm = 19;      // 机械臂气缸报警信号
        private const int X20_RobotBusy = 20;          // 机械手忙碌状态信号

        // 动作超时时间(毫秒)
        private const int ACTION_TIMEOUT = 10000;  // 10秒超时
        private const int BIN_SELECT_DELAY = 100;  // 料仓选择延迟

        public RobotController()
        {
            _ioController = IOController.Instance();
        }

        public override bool Init()
        {
            _uiLogger.Info(() => "RobotController initialized");
            return base.Init();
        }

        #region 公共方法 - 机械臂基本动作

        /// <summary>
        /// 抓取上料皮带物料到扫码区
        /// </summary>
        public async Task<bool> GrabFromBeltToScanArea()
        {
            try
            {
                _uiLogger.Info(() => "开始执行: 抓取上料皮带物料到扫码区");

                // 1. 触发允许取料信号 (Y14)
                _ioController.WriteOutput(Y14_AllowPickFromBelt, true);

                // 2. 等待机械臂完成抓取和移动 (X4 && X7)
                bool success = await WaitForCondition(
                    () => ReadInput(X4_GripComplete) && ReadInput(X7_MoveToScanArea),
                    ACTION_TIMEOUT,
                    "等待抓取并移至扫码区"
                );

                // 3. 复位信号
                _ioController.WriteOutput(Y14_AllowPickFromBelt, false);

                if (success)
                {
                    _uiLogger.Info(() => "完成: 物料已抓取至扫码区");
                }

                return success;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => "执行失败: 抓取物料到扫码区", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 放置物料到指定料仓
        /// </summary>
        /// <param name="binNumber">料仓编号 (1, 2, 3)</param>
        public async Task<bool> PlaceToBin(int binNumber)
        {
            try
            {
                _uiLogger.Info(() => $"开始执行: 放置物料到料仓{binNumber}");

                // 1. 选择目标料仓
                if (!SelectBin(binNumber))
                {
                    _uiLogger.Error(() => $"料仓选择失败: 无效的料仓编号 {binNumber}");
                    return false;
                }

                // 等待料仓选择稳定
                await Task.Delay(BIN_SELECT_DELAY);

                // 2. 触发放置信号 (Y10)
                _ioController.WriteOutput(Y10_PlaceToBin, true);

                // 3. 等待放置完成 (X8)
                bool success = await WaitForCondition(
                    () => ReadInput(X8_PlaceComplete),
                    ACTION_TIMEOUT,
                    $"等待放置到料仓{binNumber}完成"
                );

                // 4. 复位信号
                _ioController.WriteOutput(Y10_PlaceToBin, false);
                ClearBinSelection();

                if (success)
                {
                    _uiLogger.Info(() => $"完成: 物料已放置到料仓{binNumber}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => $"执行失败: 放置物料到料仓{binNumber}", ex.Message);
                ClearBinSelection();
                return false;
            }
        }

        /// <summary>
        /// 从指定料仓取料到扫码区
        /// </summary>
        /// <param name="binNumber">料仓编号 (1, 2, 3)</param>
        public async Task<bool> PickFromBinToScanArea(int binNumber)
        {
            try
            {
                _uiLogger.Info(() => $"开始执行: 从料仓{binNumber}取料到扫码区");

                // 1. 选择目标料仓
                if (!SelectBin(binNumber))
                {
                    _uiLogger.Error(() => $"料仓选择失败: 无效的料仓编号 {binNumber}");
                    return false;
                }

                // 等待料仓选择稳定
                await Task.Delay(BIN_SELECT_DELAY);

                // 2. 触发取料信号 (Y15)
                _ioController.WriteOutput(Y15_PickCommand, true);

                // 3. 等待取料完成并移至扫码区 (X10 && X7)
                bool success = await WaitForCondition(
                    () => ReadInput(X10_PickComplete) && ReadInput(X7_MoveToScanArea),
                    ACTION_TIMEOUT,
                    $"等待从料仓{binNumber}取料到扫码区"
                );

                // 4. 复位信号
                _ioController.WriteOutput(Y15_PickCommand, false);
                ClearBinSelection();

                if (success)
                {
                    _uiLogger.Info(() => $"完成: 已从料仓{binNumber}取料到扫码区");
                }

                return success;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => $"执行失败: 从料仓{binNumber}取料", ex.Message);
                ClearBinSelection();
                return false;
            }
        }

        /// <summary>
        /// 放入小车
        /// </summary>
        public async Task<bool> PlaceToCart()
        {
            try
            {
                _uiLogger.Info(() => "开始执行: 放入小车");

                // 1. 触发放入小车信号 (Y17)
                _ioController.WriteOutput(Y17_PlaceToCart, true);

                // 2. 等待放入小车完成 (X11)
                bool success = await WaitForCondition(
                    () => ReadInput(X11_InsertCartComplete),
                    ACTION_TIMEOUT,
                    "等待放入小车完成"
                );

                // 3. 复位信号
                _ioController.WriteOutput(Y17_PlaceToCart, false);

                if (success)
                {
                    _uiLogger.Info(() => "完成: 物料已放入小车");
                }

                return success;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => "执行失败: 放入小车", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 设置高速运行模式
        /// </summary>
        /// <param name="enabled">是否启用高速模式</param>
        public void SetHighSpeedMode(bool enabled)
        {
            _ioController.WriteOutput(Y16_HighSpeed, enabled);
            _uiLogger.Info(() => $"机械臂速度模式: {(enabled ? "高速" : "正常")}");
        }

        #endregion

        #region 状态查询方法

        /// <summary>
        /// 检查机械臂是否有报警
        /// </summary>
        public bool HasAlarm()
        {
            return ReadInput(X15_RobotAlarm) || ReadInput(X19_CylinderAlarm);
        }

        /// <summary>
        /// 检查机械臂是否忙碌
        /// </summary>
        public bool IsBusy()
        {
            return ReadInput(X20_RobotBusy);
        }

        /// <summary>
        /// 检查是否检测到物料
        /// </summary>
        public bool HasMaterial()
        {
            return ReadInput(X3_DetectMaterial);
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 选择料仓
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        private bool SelectBin(int binNumber)
        {
            // 先清除所有料仓选择
            ClearBinSelection();

            // 选择指定料仓
            switch (binNumber)
            {
                case 1:
                    _ioController.WriteOutput(Y11_Bin1Select, true);
                    return true;
                case 2:
                    _ioController.WriteOutput(Y12_Bin2Select, true);
                    return true;
                case 3:
                    _ioController.WriteOutput(Y13_Bin3Select, true);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 清除所有料仓选择
        /// </summary>
        private void ClearBinSelection()
        {
            _ioController.WriteOutput(Y11_Bin1Select, false);
            _ioController.WriteOutput(Y12_Bin2Select, false);
            _ioController.WriteOutput(Y13_Bin3Select, false);
        }

        /// <summary>
        /// 读取输入点状态
        /// </summary>
        private bool ReadInput(int index)
        {
            try
            {
                return LayeredIOManager.Instance().LayeredIO.ReadInBit(index, true);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => $"读取输入X{index}失败", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 等待条件满足(带超时)
        /// </summary>
        private async Task<bool> WaitForCondition(Func<bool> condition, int timeoutMs, string actionName)
        {
            DateTime startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }

                // 检查是否有报警
                if (HasAlarm())
                {
                    _uiLogger.Error(() => $"{actionName}: 检测到机械臂报警");
                    return false;
                }

                await Task.Delay(50);  // 50ms检查间隔
            }

            _uiLogger.Error(() => $"{actionName}: 超时 ({timeoutMs}ms)");
            return false;
        }
        #endregion
    }
}
