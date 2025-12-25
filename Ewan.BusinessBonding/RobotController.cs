using Ewan.Core;
using Ewan.Core.IO;
using Ewan.Model.IO;
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

        // IO点位通过 LayeredIOManager.Ctx 的 Layout 访问（ctx.R / ctx.On / ctx.Off）

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

                // 1. 触发允许取料信号
                _ioController.WriteOutput(x => x.触发机械手皮带线允许取料, true);

                // 2. 等待机械臂完成抓取和移动
                bool success = await WaitForCondition(
                    () => ReadInput(r => r.机械臂抓取完成信号) && ReadInput(r => r.移至扫码区到位信号),
                    ACTION_TIMEOUT,
                    "等待抓取并移至扫码区"
                );

                // 3. 复位信号
                _ioController.WriteOutput(x => x.触发机械手皮带线允许取料, false);

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

                // 2. 触发放置信号
                _ioController.WriteOutput(x => x.触发机械手放置料仓, true);

                // 3. 等待放置完成
                bool success = await WaitForCondition(
                    () => ReadInput(r => r.机械臂放置完成信号),
                    ACTION_TIMEOUT,
                    $"等待放置到料仓{binNumber}完成"
                );

                // 4. 复位信号
                _ioController.WriteOutput(x => x.触发机械手放置料仓, false);
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

                // 2. 触发取料信号
                _ioController.WriteOutput(x => x.发送取料指令, true);

                // 3. 等待取料完成并移至扫码区
                bool success = await WaitForCondition(
                    () => ReadInput(r => r.机械臂取料完成信号) && ReadInput(r => r.移至扫码区到位信号),
                    ACTION_TIMEOUT,
                    $"等待从料仓{binNumber}取料到扫码区"
                );

                // 4. 复位信号
                _ioController.WriteOutput(x => x.发送取料指令, false);
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

                // 1. 触发放入小车信号
                _ioController.WriteOutput(x => x.发送放入小车指令, true);

                // 2. 等待放入小车完成
                bool success = await WaitForCondition(
                    () => ReadInput(r => r.放入小车完成信号),
                    ACTION_TIMEOUT,
                    "等待放入小车完成"
                );

                // 3. 复位信号
                _ioController.WriteOutput(x => x.发送放入小车指令, false);

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
            _ioController.WriteOutput(x => x.高速运行, enabled);
            _uiLogger.Info(() => $"机械臂速度模式: {(enabled ? "高速" : "低速")}");
        }

        /// <summary>
        /// 清除报警信号
        /// </summary>
        public async Task<bool> ClearAlarm()
        {
            try
            {
                _uiLogger.Info(() => "开始执行: 清除报警");

                // 1. 触发清除报警信号
                _ioController.WriteOutput(x => x.清除报警, true);

                // 2. 等待500ms后复位信号
                await Task.Delay(100);

                // 3. 复位信号
                _ioController.WriteOutput(x => x.清除报警, false);

                _uiLogger.Info(() => "完成: 清除报警信号已发送");
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => "执行失败: 清除报警", ex.Message);
                return false;
            }
        }

        #endregion

        #region 状态查询方法

        /// <summary>
        /// 检查机械臂是否有报警
        /// </summary>
        public bool HasAlarm()
        {
            return ReadInput(r => r.机械手报警信号) || ReadInput(r => r.机械臂气缸报警信号);
        }

        /// <summary>
        /// 检查机械臂是否忙碌
        /// </summary>
        public bool IsBusy()
        {
            return ReadInput(r => r.机械手忙碌状态信号);
        }

        /// <summary>
        /// 检查是否检测到物料
        /// </summary>
        public bool HasMaterial()
        {
            return ReadInput(r => r.检测到料片信号);
        }
        
        /// <summary>
        /// 读取初始化信号状态
        /// </summary>
        public bool ReadInitializeSignal()
        {
            return ReadInput(r => r.初始化信号);
        }
        
        /// <summary>
        /// 读取料仓感应信号状态
        /// </summary>
        /// <param name="binNumber">料仓编号 (1, 2, 3)</param>
        public bool ReadBinSensor(int binNumber)
        {
            switch (binNumber)
            {
                case 1:
                    return ReadInput(r => r.料仓1有料感应);
                case 2:
                    return ReadInput(r => r.料仓2有料感应);
                case 3:
                    return ReadInput(r => r.料仓3有料感应);
                default:
                    _uiLogger.Error(() => $"无效的料仓编号: {binNumber}");
                    return false;
            }
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
                    _ioController.WriteOutput(x => x.料仓1选择信号, true);
                    return true;
                case 2:
                    _ioController.WriteOutput(x => x.料仓2选择信号, true);
                    return true;
                case 3:
                    _ioController.WriteOutput(x => x.料仓3选择信号, true);
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
            _ioController.WriteOutput(x => x.料仓1选择信号, false);
            _ioController.WriteOutput(x => x.料仓2选择信号, false);
            _ioController.WriteOutput(x => x.料仓3选择信号, false);
        }

        /// <summary>
        /// 读取输入点状态
        /// </summary>
        private bool ReadInput(Func<MarkingMachineFeederIOModel, bool> selector)
        {
            try
            {
                var ctx = LayeredIOManager.Instance().Ctx;
                if (ctx == null)
                {
                    _uiLogger.Error(() => "读取输入失败: IO未初始化");
                    return false;
                }

                return selector(ctx.R);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => "读取输入失败", ex.Message);
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
