using Ewan.Core.IO;
using Ewan.Core.Module;
using Ewan.Core.Plc;
using Ewan.Core.ScanCode;
using Ewan.Model;
using Ewan.Model.Messages;
using Ewan.Model.Production;
using Ewan.Model.System;
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;
using System.Threading.Tasks;

namespace Ewan.Core.Logic
{
    /// <summary>
    /// 物料卸载逻辑状态机
    /// 负责将料仓中的料片卸载到环线小车
    /// 基于 LogicBase + SwitchIndex 模式实现
    /// </summary>
    /// <remarks>
    /// 状态流程：
    /// 初始状态 → 检查环线信号 → 检查料仓有料
    /// → 发送取料指令 → 等待取料完成 → 等待扫码位置 → 执行扫码
    /// → 发送放入小车指令 → 等待放入完成 → 发送Modbus完成 → 清理状态 → 结束状态
    /// </remarks>
    public class MaterialUnloadingLogic : LogicBase
    {
        #region 私有字段

        private readonly ProductionLineSharedState _sharedState;
        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
        private readonly SystemParametersManager _parametersManager;
        private readonly ModbusRTUManager _modbusRTUManager;

        private IBinElevator _binElevator;

        private bool _beltStopRequested = false;
        private bool _ringLineSignal = false;
        private bool _requestProcessed = false;
        private int _emptyCount = 0;
        private int _cuttingBridgeCarCount = 0;
        private int _selectedBin = 1;
        private string _lastScannedQrCode = string.Empty;
        private int _scanRetryCount = 0;
        private bool _hasMaterial = true;
        private Guid _currentMaterialCheckRequestId;
        private TaskCompletionSource<BinElevatorStatusMessage> _materialCheckTcs;

        // Modbus寄存器地址
        private const string CART_COMPLETION_REGISTER = "153";
        private const string MATERIAL_STATUS_REGISTER = "178";

        // 超时配置（毫秒）
        private const int WAIT_PICKING_TIMEOUT = 15000;
        private const int WAIT_SCAN_POSITION_TIMEOUT = 10000;
        private const int WAIT_PUT_CART_TIMEOUT = 15000;

        // 消息订阅
        private IDisposable _ringLineSubscription;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        public MaterialUnloadingLogic(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
            _parametersManager = SystemParametersManager.Instance;
            _modbusRTUManager = ModbusRTUManager.Instance();

            // 订阅环线数据消息
            _ringLineSubscription = MessageHub.Current.Subscribe<RingLineDataMessage>(OnRingLineData);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置料仓升降模块引用
        /// </summary>
        public void SetBinElevatorModule(IBinElevator binElevator)
        {
            _binElevator = binElevator;
        }

        #endregion

        #region LogicBase 实现

        /// <summary>
        /// 状态机处理器
        /// </summary>
        public override void Handler()
        {
            // 系统暂停时不处理
            if (_sharedState.IsSystemPaused())
            {
                return;
            }

            switch (SwitchIndex)
            {
                #region 初始状态
                case "初始状态":
                    // 检测环线信号，有信号才切换步骤
                    if (_ringLineSignal && !_requestProcessed)
                    {
                        SwitchIndex = "检查环线信号";
                    }
                    else
                    {
                        // 无信号时直接标记完成，不切换步骤，避免日志刷屏
                        // 同时清除处理标志（信号变为0时）
                        if (!_ringLineSignal && _requestProcessed)
                        {
                            _requestProcessed = false;
                        }
                        IsFinish = true;
                    }
                    break;
                #endregion

                #region 检查环线信号
                case "检查环线信号":
                    // 有环线信号，开始处理
                    _requestProcessed = true;
                    SwitchIndex = "检查料仓有料";
                    break;
                #endregion

                #region 检查空车数量
                case "检查空车数量":
                    {
                        var cartParameters = _parametersManager?.Parameters;
                        var cartCheckMode = cartParameters?.CartCheckMode ?? CartCheckMode.EmptyCart;
                        bool needSendEmptyCar = false;

                        if (cartCheckMode == CartCheckMode.EmptyCart)
                        {
                            var reserveCount = Math.Max(0, cartParameters?.EmptyCartReserveCount ?? 0);
                            if (_emptyCount <= reserveCount)
                            {
                                needSendEmptyCar = true;
                            }
                        }
                        else
                        {
                            var reserveCount = Math.Max(0, cartParameters?.CuttingBridgeCarReserveCount ?? 0);
                            if (_cuttingBridgeCarCount > reserveCount)
                            {
                                needSendEmptyCar = true;
                            }
                        }

                        if (needSendEmptyCar)
                        {
                            SwitchIndex = "释放空车";
                            return;
                        }

                        SwitchIndex = "检查料仓有料";
                    }
                    break;
                #endregion

                #region 检查料仓有料
                case "检查料仓有料":
                    _selectedBin = GetConfiguredBinNumber();
                    if (_binElevator == null)
                    {
                        _hasMaterial = true;
                        SwitchIndex = "发送取料指令";
                        return;
                    }

                    if (_materialCheckTcs != null)
                    {
                        if (!_materialCheckTcs.Task.IsCompleted)
                        {
                            return;
                        }

                        BinElevatorStatusMessage statusResult = null;
                        try
                        {
                            if (_materialCheckTcs.Task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                            {
                                statusResult = _materialCheckTcs.Task.Result;
                            }
                            else if (_materialCheckTcs.Task.IsCanceled)
                            {
                                statusResult = BinElevatorStatusMessage.MaterialCheckResult(_selectedBin, BinOperationResult.Error, "操作取消");
                            }
                            else if (_materialCheckTcs.Task.IsFaulted)
                            {
                                var error = _materialCheckTcs.Task.Exception?.GetBaseException()?.Message ?? "未知错误";
                                _uiLogger.ErrorRaw("料仓{0}检测物料失败: {1}", _selectedBin, error);
                                statusResult = BinElevatorStatusMessage.MaterialCheckResult(_selectedBin, BinOperationResult.Error, "检测失败", error);
                            }
                        }
                        finally
                        {
                            _materialCheckTcs = null;
                            _currentMaterialCheckRequestId = Guid.Empty;
                        }

                        bool hasMaterial = statusResult?.OperationResult == BinOperationResult.HasMaterial;
                        bool timedOut = statusResult?.OperationResult == BinOperationResult.Timeout;

                        if (hasMaterial)
                        {
                            _hasMaterial = true;
                            SwitchIndex = "发送取料指令";
                        }
                        else
                        {
                            if (timedOut)
                            {
                                MessageHub.Current.Post(new AlarmMessage(
                                    key: "BinElevator.Timeout",
                                    content: $"料仓{_selectedBin}升降超时，未检测到物料",
                                    level: AlarmLevel.H,
                                    needReset: true,
                                    unit: "BinElevator"));
                            }

                            _hasMaterial = false;
                            SwitchIndex = "释放空车";
                        }

                        return;
                    }

                    {
                        int binNumber = _selectedBin;
                        var requestId = Guid.NewGuid();
                        _currentMaterialCheckRequestId = requestId;
                        _materialCheckTcs = new TaskCompletionSource<BinElevatorStatusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var tcs = _materialCheckTcs;

                        Task.Run(async () =>
                        {
                            try
                            {
                                var result = await _binElevator.RaiseToSensorAsync(binNumber).ConfigureAwait(false);
                                if (_currentMaterialCheckRequestId != requestId)
                                {
                                    return;
                                }
                                tcs?.TrySetResult(result);
                            }
                            catch (Exception ex)
                            {
                                if (_currentMaterialCheckRequestId != requestId)
                                {
                                    return;
                                }
                                _uiLogger.ErrorRaw("料仓{0}检测物料异常: {1}", binNumber, ex.Message);
                                tcs?.TrySetResult(BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Error, "检测异常", ex.Message));
                            }
                        });
                    }
                    break;
                #endregion

                #region 发送取料指令
                case "发送取料指令":
                    // 设置料仓选择信号
                    SetBinSelectSignal(_selectedBin);

                    // 触发取料信号
                    _ioManager?.Ctx?.On(x => x.发送取料指令);

                    // 请求停止皮带
                    _beltStopRequested = true;
                    {
                        var message = new BeltConveyorControlMessage(
                            BeltConveyorControlSource.MaterialUnloading,
                            true,
                            "下料流程请求停止皮带");
                        MessageHub.Current.Post(message);
                    }

                    SwitchIndex = "等待取料完成";
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 等待取料完成
                case "等待取料完成":
                    if (_ioManager?.Ctx?.Edge.F(x => x.机械臂取料完成信号) == true)
                    {
                        MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.UnloadingCompleted(nameof(MaterialUnloadingLogic)));

                        _ioManager?.Ctx?.Off(x => x.发送取料指令);
                        ClearBinSelectSignals();

                        SwitchIndex = "等待扫码位置";
                        Tw.StartWatch(SwitchIndex);
                        return;
                    }

                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_PICKING_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Unloading.Timeout",
                            content: "等待取料完成超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Unloading"));
                        ForceCleanup("取料超时");
                    }
                    break;
                #endregion

                #region 等待扫码位置
                case "等待扫码位置":
                    if (_ioManager?.Ctx?.Edge.R(x => x.移至扫码区到位信号) == true)
                    {
                        SwitchIndex = "执行扫码";
                        _scanRetryCount = 0;
                        return;
                    }

                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Unloading.Timeout",
                            content: "等待扫码位置超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Unloading"));
                        ForceCleanup("扫码位置超时");
                    }
                    break;
                #endregion

                #region 执行扫码
                case "执行扫码":
                    {
                        var scanParameters = _parametersManager?.Parameters;
                        bool mesEnabled = scanParameters?.MesEnabled ?? false;
                        int maxRetry = scanParameters?.CodeReaderScanRetryCount ?? 3;
                        if (maxRetry <= 0) maxRetry = 3;

                        if (mesEnabled)
                        {
                            _scanRetryCount++;
                            _lastScannedQrCode = (ScannerManager.Instance().TriggerScan() ?? string.Empty).Trim();

                            if (!string.IsNullOrWhiteSpace(_lastScannedQrCode))
                            {
                                _uiLogger.InfoRaw("扫码内容: {0}", _lastScannedQrCode);
                            }
                            else if (_scanRetryCount < maxRetry)
                            {
                                return;
                            }
                            else
                            {
                                _lastScannedQrCode = string.Empty;
                                MessageHub.Current.Post(new AlarmMessage(
                                    key: "Scan.Failed",
                                    content: $"下料扫码失败：连续扫码{maxRetry}次无结果",
                                    level: AlarmLevel.L,
                                    needReset: false,
                                    unit: "Scanner"));
                            }
                        }

                        SwitchIndex = "发送放入小车指令";
                    }
                    break;
                #endregion

                #region 发送放入小车指令
                case "发送放入小车指令":
                    // 发送扫码完成信号给机械臂
                    _ioManager?.Ctx?.On(x => x.发送扫码完成信号);

                    // 发送放入小车信号
                    _ioManager?.Ctx?.On(x => x.发送放入小车指令);

                    SwitchIndex = "等待放入完成";
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 等待放入完成
                case "等待放入完成":
                    if (_ioManager?.Ctx?.Edge.R(x => x.放入小车完成信号) == true)
                    {
                        // 清除放入小车信号
                        _ioManager?.Ctx?.Off(x => x.发送放入小车指令);
                        _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);

                        SwitchIndex = "发送Modbus完成";
                        return;
                    }

                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_PUT_CART_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Unloading.Timeout",
                            content: "等待放入小车完成超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Unloading"));
                        ForceCleanup("放入小车超时");
                    }
                    break;
                #endregion

                #region 发送Modbus完成
                case "发送Modbus完成":
                    SendCartCompletionToModbus(true);
                    SwitchIndex = "清理状态";
                    break;
                #endregion

                #region 清理状态
                case "清理状态":
                    // 重置标志
                    _lastScannedQrCode = string.Empty;

                    // 释放皮带控制
                    if (_beltStopRequested)
                    {
                        _beltStopRequested = false;
                        var message = new BeltConveyorControlMessage(
                            BeltConveyorControlSource.MaterialUnloading,
                            false,
                            "下料流程完成");
                        MessageHub.Current.Post(message);
                    }

                    Complete();
                    break;
                #endregion

                #region 释放空车
                case "释放空车":
                    _lastScannedQrCode = string.Empty;
                    ClearBinSelectSignals();
                    SendCartCompletionToModbus(false);

                    if (_beltStopRequested)
                    {
                        _beltStopRequested = false;
                        var message = new BeltConveyorControlMessage(
                            BeltConveyorControlSource.MaterialUnloading,
                            false,
                            $"料仓无料或空车不足，释放皮带");
                        MessageHub.Current.Post(message);
                    }

                    Complete();
                    break;
                #endregion

                #region 结束状态
                case "结束状态":
                    // 完成，等待下一个周期
                    break;
                #endregion
            }
        }

        /// <summary>
        /// 复位状态机
        /// </summary>
        public override void Rset()
        {
            _beltStopRequested = false;
            _requestProcessed = false;
            _selectedBin = 1;
            _lastScannedQrCode = string.Empty;
            _scanRetryCount = 0;
            _hasMaterial = true;
            _currentMaterialCheckRequestId = Guid.Empty;
            _materialCheckTcs = null;
            base.Rset();
        }

        /// <summary>
        /// 销毁时清理资源
        /// </summary>
        public void Dispose()
        {
            _ringLineSubscription?.Dispose();
            _ringLineSubscription = null;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 处理环线数据消息
        /// </summary>
        private void OnRingLineData(RingLineDataMessage msg)
        {
            _ringLineSignal = msg.IsLoading;
            _emptyCount = msg.EmptyCarCount;
            _cuttingBridgeCarCount = msg.CuttingBridgeCarCount;
        }

        /// <summary>
        /// 强制清理并返回初始状态
        /// </summary>
        public void ForceCleanup(string reason)
        {
            try
            {
                if (_ioManager?.Ctx != null)
                {
                    _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);
                    _ioManager.Ctx.Off(x => x.发送扫码完成信号);
                    _ioManager.Ctx.Off(x => x.发送取料指令);
                    _ioManager.Ctx.Off(x => x.发送放入小车指令);
                    ClearBinSelectSignals();
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "强制清理IO", ex.Message);
            }

            _ringLineSignal = false;
            _requestProcessed = false;

            if (_beltStopRequested)
            {
                _beltStopRequested = false;
                var message = new BeltConveyorControlMessage(
                    BeltConveyorControlSource.MaterialUnloading,
                    false,
                    reason);
                MessageHub.Current.Post(message);
            }

            Rset();
        }

        /// <summary>
        /// 获取配置的料仓编号
        /// </summary>
        private int GetConfiguredBinNumber()
        {
            var selected = _parametersManager?.Parameters?.UnloadingBinSelection ?? BinSelection.Bin1;
            switch (selected)
            {
                case BinSelection.Bin2: return 2;
                case BinSelection.Bin3: return 3;
                default: return 1;
            }
        }

        /// <summary>
        /// 设置料仓选择信号
        /// </summary>
        private void SetBinSelectSignal(int binNumber)
        {
            if (_ioManager?.Ctx == null) return;

            ClearBinSelectSignals();

            switch (binNumber)
            {
                case 1:
                    _ioManager.Ctx.On(x => x.料仓1选择信号);
                    break;
                case 2:
                    _ioManager.Ctx.On(x => x.料仓2选择信号);
                    break;
                case 3:
                    _ioManager.Ctx.On(x => x.料仓3选择信号);
                    break;
            }
        }

        /// <summary>
        /// 清除所有料仓选择信号
        /// </summary>
        private void ClearBinSelectSignals()
        {
            if (_ioManager?.Ctx == null) return;

            _ioManager.Ctx.Off(x => x.料仓1选择信号);
            _ioManager.Ctx.Off(x => x.料仓2选择信号);
            _ioManager.Ctx.Off(x => x.料仓3选择信号);
        }

        /// <summary>
        /// 发送完成信号到Modbus
        /// </summary>
        private void SendCartCompletionToModbus(bool hasMaterial)
        {
            try
            {
                _modbusRTUManager?.WriteAny(CART_COMPLETION_REGISTER, (ushort)1);

                ushort statusValue = hasMaterial ? (ushort)1 : (ushort)0;
                _modbusRTUManager?.WriteAny(MATERIAL_STATUS_REGISTER, statusValue);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "发送完成信号到Modbus失败", ex.Message);
            }
        }

        #endregion
    }
}
