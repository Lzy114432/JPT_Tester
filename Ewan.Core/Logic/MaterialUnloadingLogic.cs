using Ewan.Core.IO;
using Ewan.Core.Mes;
using Ewan.Core.Plc;
using Ewan.Core.ScanCode;
using Ewan.Mes.Devices.ZHJW.DicingMachine;
using Ewan.Mes.Models.Domain.ZHJW.DicingMachine;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Mqtt;
using Ewan.Mes.Services.ZHJW;
using Ewan.Model;
using Ewan.Model.Messages;
using Ewan.Model.Production;
using Ewan.Model.System;
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
    /// 初始状态 → 检查环线信号 → 检查空车数量 → 检查料仓有料
    /// → 发送取料指令 → 等待取料完成 → 等待扫码位置 → 执行扫码
    /// → 发送放入小车指令 → 等待放入完成 → 发送Modbus完成 → 清理状态 → 结束状态
    /// （检查空车数量不足或检查料仓无料时 → 释放空车 → 结束）
    /// </remarks>
    public class MaterialUnloadingLogic : LogicBase
    {
        #region 私有字段

        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
        private readonly SystemParametersManager _parametersManager;
        private readonly ModbusRTUManager _modbusRTUManager;
        private Task<MesRingLineFeedback> _mesFeedingTask;
        private CancellationTokenSource _mesFeedingCts;
        private int _mesRetryCount = 0;
        private Stopwatch sw_状态刷新 = new Stopwatch();
        private bool b_状态刷新 = false;//第一次不看时间间隔，看标志位
        // 超时配置（毫秒）
        private const int MES_REQUEST_TIMEOUT_BUFFER_MS = 5000;
        private const int MAX_MES_RETRY_COUNT = 3;
        public Dictionary<string, FeedingUnloadingStateResponseData> dic_上次状态 = new Dictionary<string, FeedingUnloadingStateResponseData>();
        public Dictionary<string, FeedingUnloadingStateResponseData> dic_当前状态 = new Dictionary<string, FeedingUnloadingStateResponseData>();
        private int I_间隔上料 = 0;
        private bool _beltStopRequested = false;
        //private bool _ringLineRisingEdge = false;
        //private bool _ringLineIsLoading = false;
        //private bool _ringLineArmed = true;
        //private int _emptyCount = 0;
        //private int _cuttingBridgeCarCount = 0;
        private int _selectedBin = 1;
        private string _lastScannedQrCode = string.Empty;
        private int _scanRetryCount = 0;
        private bool _hasMaterial = true;
        private Task<BinElevatorStatusMessage> _materialCheckTask;

        private bool b_下空车008 = false;
        private bool b_下空车006 = false;
        private bool b_下空车007 = false;

        //private List<string> ls_上料 = new List<string>();
        //private List<string> ls_下料 = new List<string>();
        //private List<string> ls_状态 = new List<string>();

        // Modbus寄存器地址
        private const string CART_COMPLETION_REGISTER = "153";
        private const string MATERIAL_STATUS_REGISTER = "178";

        // 超时配置（毫秒）
        private const int WAIT_PICKING_TIMEOUT = 60000;
        private const int WAIT_LOADING_COMPLETE_TIMEOUT = 150000;
        private const int WAIT_SCAN_POSITION_TIMEOUT = 10000;
        private const int WAIT_PUT_CART_TIMEOUT = 60000;

        // 消息订阅
        //private IDisposable _ringLineSubscription;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public MaterialUnloadingLogic()
        {
            _parametersManager = SystemParametersManager.Instance;
            _modbusRTUManager = ModbusRTUManager.Instance();

            // 订阅环线数据消息
            //_ringLineSubscription = MessageHub.Current.Subscribe<RingLineDataMessage>(OnRingLineData);
        }

        #endregion

        #region LogicBase 实现

        /// <summary>
        /// 状态机处理器
        /// </summary>
        public override void Handler()
        {

            switch (SwitchIndex)
            {
                #region 初始状态
                case "初始状态":
                    if (Tw.StartCheckIsTimeout(SwitchIndex, 30))
                    {
                        // 检测环线上升沿，有边沿才切换步骤
                        if (!(_parametersManager.Parameters._ringLineRisingEdge))
                        {
                            // 无上升沿时直接标记完成，不切换步骤，避免日志刷屏
                            IsFinish = true;
                            break;
                        }

                        _parametersManager.Parameters._ringLineRisingEdge = false;
                        //_parametersManager.Parameters._ringLineArmed = false;
                        SwitchIndex = "检查环线信号";
                    }

                    break;
                #endregion

                #region 检查环线信号
                case "检查环线信号":
                    // 有环线信号，开始检查空车数量
                    SwitchIndex = "检查空车数量";
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
                            if (_parametersManager.Parameters._emptyCount <= reserveCount)
                            {
                                needSendEmptyCar = true;
                            }
                        }
                        else
                        {
                            var reserveCount = Math.Max(0, cartParameters?.CuttingBridgeCarReserveCount ?? 0);
                            if (_parametersManager.Parameters._cuttingBridgeCarCount > reserveCount)
                            {
                                needSendEmptyCar = true;
                            }
                        }

                        if (needSendEmptyCar || _parametersManager.Parameters.b_启用释放空车)
                        {
                            SwitchIndex = "释放空车";
                            return;
                        }
                        //
                        cartParameters.I_小车间隔数量 = func_设置空车数量();
                        if (cartParameters.I_小车间隔数量 != 0 && (sw_状态刷新.Elapsed.TotalSeconds > 30 || !b_状态刷新))
                        {
                            b_状态刷新 = true;
                            sw_状态刷新.Restart();
                            I_间隔上料 = 0;
                        }
                        if (cartParameters.I_小车间隔数量 > I_间隔上料)
                        {
                            SwitchIndex = "释放空车";
                            I_间隔上料++;
                            return;
                        }
                        //if (b_间隔上料)
                        //{
                        //    SwitchIndex = "释放空车";
                        //    b_间隔上料 = false;
                        //    return;
                        //}

                        //b_间隔上料 = true;

                        //if (I_间隔上料 >= cartParameters.I_小车间隔数量)
                        //{
                        //    SwitchIndex = "释放空车";
                        //    I_间隔上料 = 0;
                        //    return;
                        //}
                        //I_间隔上料++;
                        SwitchIndex = "检查料仓有料";
                    }
                    break;
                #endregion

                #region 检查料仓有料
                case "检查料仓有料":
                    _selectedBin = GetConfiguredBinNumber();
                    bool hasMaterial = true;
                    if (_parametersManager?.Parameters?.MesEnabled == true && _parametersManager.Parameters.dic_料仓单号.Count > 0)
                    {
                        _selectedBin = _parametersManager.Parameters.dic_料仓单号[_parametersManager.Parameters.str_当前工单号];
                    }
                    if (_parametersManager.Parameters.dic_有无料.ContainsKey(_selectedBin))
                    {
                        if (!_parametersManager.Parameters.dic_有无料[_selectedBin])
                        {
                            //hasMaterial = false;
                            SwitchIndex = "释放空车"; return;
                        }
                    }
                    if (_materialCheckTask != null)
                    {
                        if (!_materialCheckTask.IsCompleted)
                        {
                            return;
                        }

                        BinElevatorStatusMessage statusResult = null;
                        try
                        {
                            if (_materialCheckTask.Status == TaskStatus.RanToCompletion)
                            {
                                statusResult = _materialCheckTask.Result;
                            }
                            else if (_materialCheckTask.IsCanceled)
                            {
                                statusResult = BinElevatorStatusMessage.MaterialCheckResult(_selectedBin, BinOperationResult.Timeout, "超时");
                            }
                            else if (_materialCheckTask.IsFaulted)
                            {
                                var error = _materialCheckTask.Exception?.GetBaseException()?.Message ?? "未知错误";
                                _uiLogger.ErrorRaw("料仓{0}检测物料失败: {1}", _selectedBin, error);
                                statusResult = BinElevatorStatusMessage.MaterialCheckResult(_selectedBin, BinOperationResult.Error, "检测失败", error);
                            }
                        }
                        finally
                        {
                            _materialCheckTask = null;
                        }

                        //bool hasMaterial = statusResult?.OperationResult == BinOperationResult.HasMaterial;
                        bool timedOut = statusResult?.OperationResult == BinOperationResult.Timeout;

                        if (hasMaterial)
                        {
                            _hasMaterial = true;
                            func_定位电磁阀(_selectedBin, true);
                            SwitchIndex = "发送取料指令";
                        }
                        else
                        {
                            //if (timedOut)
                            //{
                            //    MessageHub.Current.Post(new AlarmMessage(
                            //        key: "BinElevator.Timeout",
                            //        content: $"料仓{_selectedBin}升降超时，未检测到物料",
                            //        level: AlarmLevel.H,
                            //        needReset: true,
                            //        unit: "BinElevator"));
                            //}

                            _hasMaterial = false;
                            SwitchIndex = "释放空车";
                        }

                        return;
                    }

                    try
                    {
                        var request = BinElevatorCommandMessage.RaiseToSensor(_selectedBin, nameof(MaterialUnloadingLogic));
                        _materialCheckTask = MessageHub.Current.RequestAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                            request,
                            timeoutMs: 10000);
                    }
                    catch (Exception ex)
                    {
                        _uiLogger.ErrorRaw("料仓{0}检测物料请求失败: {1}", _selectedBin, ex.Message);
                        _hasMaterial = false;
                        SwitchIndex = "释放空车";
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

                    SwitchIndex = "等待机械手取料到位";
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 等待取料完成
                case "等待机械手取料到位":
                    if (_ioManager?.Ctx?.R.DI4 == true)
                    {
                        func_定位电磁阀(_selectedBin, false);
                        // 2. 发送消息！让Z轴下降5mm
                        // 这里的 -5.0 就是下降距离，底层监听到后就会自动执行 OnMoveRelative
                        var moveDownMsg = BinElevatorCommandMessage.MoveRelative(_selectedBin, -40.0, "取料时下降");
                        MessageHub.Current.Post(moveDownMsg);
                        //// 3. 开启对应料仓吹气
                        //Thread.Sleep(2000);
                        func_吹气(_selectedBin, true);
                        Thread.Sleep(2000);
                        _ioManager?.Ctx?.On(x => x.DO16);
                        SwitchIndex = "等待取料完成";
                        Tw.StartWatch(SwitchIndex);
                        return;
                    }
                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_PICKING_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Unloading.Timeout",
                            content: "等待机械手到位超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Unloading"));
                        ForceCleanup("取等待机械手到位超时");
                    }


                    break;

                case "等待取料完成":
                    if (_ioManager?.Ctx?.R.机械臂取料完成信号 == true)
                    {
                        _ioManager?.Ctx?.Off(x => x.发送取料指令);
                        _ioManager?.Ctx?.Off(x => x.DO16);
                        // 2. 发送消息！让Z轴下降5mm
                        // 这里的 -5.0 就是下降距离，底层监听到后就会自动执行 OnMoveRelative

                        //Thread.Sleep(2000);
                        //// 3. 开启对应料仓吹气

                        //ClearBinSelectSignals();
                        Thread.Sleep(200);
                        SwitchIndex = "等待扫码位置";

                        Tw.StartWatch(SwitchIndex);
                        return;
                    }
                    if (_ioManager.Ctx.R.X16 == true)
                    {
                        func_吹气(_selectedBin, false);
                        _ioManager?.Ctx?.Off(x => x.DO16);
                        _parametersManager.Parameters.dic_有无料[_selectedBin] = false;
                        SwitchIndex = "释放空车";
                        return;
                    }
                    //if (_ioManager.Ctx.Edge.R(x => x.下相机报警信号))
                    //{
                    //    Thread.Sleep(5000);
                    //    SwitchIndex = "移动到料仓";
                    //    return;
                    //}
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

                    if (_ioManager?.Ctx?.R.移至扫码区到位信号 == true)
                    {
                        //var moveDownMsg = BinElevatorCommandMessage.MoveRelative(_selectedBin, 25.0, "取料完成上升");
                        //MessageHub.Current.Post(moveDownMsg);
                        //Thread.Sleep(100);
                        //if (_ioManager?.Ctx?.Edge.R(x => x.移至扫码区到位信号) == true)
                        //{
                        SwitchIndex = "执行扫码";
                        _scanRetryCount = 0;
                        return;
                        //}

                    }
                    //if (_ioManager.Ctx.Edge.R(x => x.下相机报警信号))
                    //{
                    //    //Thread.Sleep(5000);
                    //    SwitchIndex = "移动到料仓";
                    //    return;
                    //}
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
                case "到达扫码位置":
                    // 禁止继续取料
                    _ioManager?.Ctx?.On(x => x.定位夹持);//定位气缸夹料

                    var beltReleaseMessage1 = BeltConveyorControlMessage.Release(
                        BeltConveyorControlSource.MaterialLoading,
                        "装料完成，释放皮带");
                    MessageHub.Current.Post(beltReleaseMessage1);

                    SwitchIndex = "等待夹料完成";
                    _scanRetryCount = 0;
                    Tw.StartWatch(SwitchIndex);
                    break;
                case "等待夹料完成":
                    if (_ioManager?.Ctx?.R.夹持夹紧 == true) // 等待夹料完成信号
                    {
                        // 1. 通知机械手取料，同时立刻开始执行扫码
                        _ioManager?.Ctx?.On(x => x.发送扫码完成信号);
                        //Thread.Sleep(500);
                        //_ioManager?.Ctx?.Off(x => x.发送扫码完成信号);

                        _scanRetryCount = 0;
                        SwitchIndex = "信号交互";
                        Tw.StartWatch(SwitchIndex);
                        return;
                    }

                    // 超时检查
                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    {
                        ForceCleanup("夹料超时");
                        SwitchIndex = "清理状态";
                        return;
                    }
                    break;

                case "信号交互":
                    if (_ioManager?.Ctx?.R.移至扫码区到位信号 == true)
                    {
                        // 超时检查
                        if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                        {
                            ForceCleanup("夹料超时");
                            SwitchIndex = "清理状态";
                            return;
                        }
                    }
                    else
                    {
                        _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
                        SwitchIndex = "执行扫码";
                    }
                    break;

                case "执行扫码":
                    {


                        var scanParameters = _parametersManager?.Parameters;
                        bool mesEnabled = scanParameters?.MesEnabled ?? false;
                        int maxRetry = scanParameters?.CodeReaderScanRetryCount ?? 3;
                        if (maxRetry <= 0) maxRetry = 3;

                        bool isScanDone = false;
                        bool isScanSuccess = false;

                        if (mesEnabled)
                        {
                            _scanRetryCount++;
                            if (_scanRetryCount < maxRetry)
                                _lastScannedQrCode = (ScannerManager.Instance().TriggerScan() ?? string.Empty).Trim();

                            if (!string.IsNullOrWhiteSpace(_lastScannedQrCode) && _lastScannedQrCode != "")
                            {
                                _uiLogger.InfoRaw("扫码内容: {0}", _lastScannedQrCode);
                                isScanDone = true;
                                isScanSuccess = true;
                            }
                            else if (_scanRetryCount < maxRetry)
                            {
                                return; // 重试条件未竭，继续等待下一周期扫码
                            }
                            else
                            {
                                _uiLogger.WarnRaw("扫码失败：连续扫码{0}次无结果", maxRetry);
                                isScanDone = true;
                                isScanSuccess = false;
                            }
                        }
                        else
                        {
                            isScanDone = true;
                            isScanSuccess = true;
                        }

                        if (isScanDone)
                        {
                            // 扫码动作结束（无论成功或失败），开始结算外围动作
                            if (_ioManager?.Ctx?.R.移至扫码区到位信号 == true)//等待机械手二次到位
                            {
                                //_ioManager?.Ctx?.On(x => x.发送扫码完成信号);
                                //Thread.Sleep(100);
                                _ioManager?.Ctx?.Off(x => x.定位夹持);
                            }
                            else
                            {
                                break;
                            }

                            // 4. 等待机械手完全把料取走
                            SwitchIndex = "等待松开夹料";
                            Tw.StartWatch(SwitchIndex);
                        }
                    }
                    break;


                case "等待松开夹料":
                    // 假设 X16 为 false 即代表松开到位。如果有专属的松开到位传感器，请替换这里。
                    if (_ioManager?.Ctx?.R.夹持松开 == true)
                    {
                        var moveDownMsg = BinElevatorCommandMessage.MoveRelative(_selectedBin, 40.0, "取料完成上升");
                        MessageHub.Current.Post(moveDownMsg);
                        // 确认气缸松开到位后，才通知扫码完成
                        _ioManager?.Ctx?.On(x => x.发送扫码完成信号);

                        SwitchIndex = "发送MES下料信号";
                        Tw.StartWatch(SwitchIndex);
                        return;
                    }

                    // 超时检查
                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    {
                        ForceCleanup("松开定位气缸超时");
                        SwitchIndex = "清理状态";
                        return;
                    }
                    break;


                #endregion 
                #region 发送MES下料信号
                case "发送MES下料信号":
                    if (_parametersManager?.Parameters?.MesEnabled != true)
                    {
                        _mesRetryCount = 0;
                        ClearMesFeedingRequest();
                        SwitchIndex = "发送放入小车指令";
                        return;
                    }
                    if (_lastScannedQrCode == "")
                    {
                        _uiLogger.Info("扫码失败");
                        //_ioManager?.Ctx?.On(x => x.料仓3选择信号);

                        SystemParametersManager.Instance.Parameters.i_料仓NG数量++;
                        ClearBinSelectSignals();
                        _ioManager?.Ctx?.On(x => x.触发机械手放置料仓);
                        SwitchIndex = "释放空车";
                        return;
                    }

                    if (_mesFeedingTask == null && _parametersManager?.Parameters?.MesEnabled == true)
                    {
                        var mesManager = MesManager.Instance();
                        if (!mesManager.IsConnected || !mesManager.IsRingLineInitialized)
                        {
                            _uiLogger.WarnRaw("MES未连接或未初始化，跳过上料请求");
                            _mesRetryCount = 0;
                            _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                            SwitchIndex = "移动到料仓";
                            return;
                        }

                        var scanParameters = _parametersManager?.Parameters;
                        var timeoutSeconds = scanParameters?.RingLineTimeoutSeconds ?? 30;
                        if (timeoutSeconds <= 0)
                        {
                            timeoutSeconds = 30;
                        }

                        var requestTimeoutMs = timeoutSeconds * 1000;
                        var request = new MesRingLineRequest
                        {
                            Action = MesRingLineAction.UnloadingQianLiaocang,
                            PlateCode = _lastScannedQrCode,
                            FeedingLiaokuangCode = GetLiaokuangCode(_selectedBin)
                        };

                        _mesRetryCount++;
                        ClearMesFeedingRequest();
                        _mesFeedingCts = new CancellationTokenSource();
                        _mesFeedingTask = MessageHub.Current.RequestAsync<MesRingLineRequest, MesRingLineFeedback>(
                            request,
                            timeoutMs: requestTimeoutMs + MES_REQUEST_TIMEOUT_BUFFER_MS,
                            cancellationToken: _mesFeedingCts.Token);

                    }
                    if (!_mesFeedingTask.IsCompleted)
                    {
                        return;
                    }

                    try
                    {
                        if (_mesFeedingTask.IsCanceled)
                        {
                            _uiLogger.WarnRaw("MES下料请求已取消");
                            _mesRetryCount = 0;
                            _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                            SwitchIndex = "移动到料仓";
                        }
                        else if (_mesFeedingTask.IsFaulted)
                        {
                            var error = _mesFeedingTask.Exception?.GetBaseException()?.Message ?? "未知错误";
                            if (TryRetryMesFeedingRequest($"MES下料请求异常: {error}"))
                            {
                                return;
                            }
                            _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                            SwitchIndex = "移动到料仓";
                        }
                        else
                        {
                            var feedback = _mesFeedingTask.Result;
                            if (feedback.Success)
                            {
                                //var responseData = feedback.Data as UnloadingQianLiaocangResponse;
                                //if (responseData.Success)
                                //{
                                _mesRetryCount = 0;
                                SwitchIndex = "发送放入小车指令";
                                //}
                                //else
                                //{

                                //    SwitchIndex = "移动到料仓";
                                //}
                            }
                            else
                            {
                                if (TryRetryMesFeedingRequest($"MES下料请求失败: {feedback.Message}"))
                                {
                                    return;
                                }
                                _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                                SwitchIndex = "移动到料仓";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (TryRetryMesFeedingRequest($"MES下料请求异常: {ex.Message}"))
                        {
                            return;
                        }
                        _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                        SwitchIndex = "移动到料仓";
                    }
                    finally
                    {
                        if (SwitchIndex != "发送MES下料请求")
                        {
                            ClearMesFeedingRequest();
                        }
                    }
                    break;
                #endregion

                #region 移动到料仓
                case "移动到料仓":
                    // 触发放入料仓信号
                    //_ioManager?.Ctx?.On(x => x.发送扫码完成信号);

                    _ioManager.Ctx.Off(x => x.发送取料指令);
                    _ioManager?.Ctx?.On(x => x.触发机械手放置料仓);
                    if (_lastScannedQrCode != "" && _parametersManager.Parameters.MesEnabled)
                        ModbusRTUManager.Instance()?.WriteWorkOrderToFirstAvailable(_lastScannedQrCode.Remove(_lastScannedQrCode.Length - 3));
                    SwitchIndex = "等待装载完成";
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 等待装载完成
                case "等待装载完成":
                    if (_ioManager?.Ctx?.Edge.F(x => x.机械臂放置完成信号) == true)
                    {
                        MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.LoadingCompleted(
                            _selectedBin,
                            nameof(MaterialLoadingLogic)));

                        SystemParametersManager.Instance.Parameters.i_上料速率++;
                        MessageHub.Current.Post(LoadingUnloadingStateMessage.LoadingCompleted(_selectedBin, nameof(MaterialLoadingLogic)));
                        SwitchIndex = "清理状态";
                        return;
                    }

                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_LOADING_COMPLETE_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Loading.Timeout",
                            content: "等待装载完成超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Loading"));
                        ForceCleanup("装载超时");
                    }
                    break;
                #endregion

                #region 发送放入小车指令
                case "发送放入小车指令":
                    if (_parametersManager.Parameters._ringLineFallingEdge)
                    {
                        _ioManager?.Ctx?.On(x => x.发送扫码完成信号);
                        // 发送放入小车信号
                        _ioManager?.Ctx?.On(x => x.发送放入小车指令);

                        _parametersManager.Parameters._ringLineFallingEdge = false;
                        SwitchIndex = "等待机械手到位";
                        Tw.StartWatch(SwitchIndex);
                    }
                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_LOADING_COMPLETE_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "UnLoading.Timeout",
                            content: "等待环形线上料到位",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "UnLoading"));
                        ForceCleanup("等待环形线上料到位");
                    }
                    // 发送扫码完成信号给机械臂

                    break;
                #endregion

                #region 等待放入完成
                case "等待机械手到位"://判断小车是否到位
                    if (_ioManager?.Ctx?.R.DI5 == true)
                    {
                        // 清除放入小车信号
                        _ioManager?.Ctx?.On(x => x.DO24);
                        SwitchIndex = "等待放入完成";
                        return;
                    }

                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_PUT_CART_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Unloading.Timeout",
                            content: "等待机械手到位超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Unloading"));
                        ForceCleanup("等待机械手到位超时");
                    }
                    break;
                case "等待放入完成":
                    //bool b = _ioManager.Ctx.Edge.R(x => x.放入小车完成信号);
                    //bool b1 = _ioManager.Ctx.Edge.R(x => x.移至扫码区到位信号);
                    //if (_ioManager?.Ctx?.Edge.R(x => x.放入小车完成信号) == true)
                    if (_ioManager?.Ctx?.R.放入小车完成信号 == true)
                    {
                        func_吹气(_selectedBin, false);
                        // 清除放入小车信号
                        _ioManager?.Ctx?.Off(x => x.发送放入小车指令);
                        _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
                        _ioManager?.Ctx?.Off(x => x.DO24);
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
                    Thread.Sleep(500);
                    SendCartCompletionToModbus(true);

                    SystemParametersManager.Instance.Parameters.i_下料速率++;
                    MessageHub.Current.Post(LoadingUnloadingStateMessage.UnloadingCompleted(_selectedBin, nameof(MaterialLoadingLogic)));
                    SwitchIndex = "清理状态";
                    break;
                #endregion

                #region 清理状态
                case "清理状态":
                    // 重置标志

                    if (_ioManager?.Ctx?.R.放入小车完成信号 == true)
                    {
                        break;
                    }
                    _lastScannedQrCode = string.Empty;
                    _ioManager?.Ctx?.Off(x => x.触发机械手放置料仓);
                    ClearBinSelectSignals();
                    func_吹气(_selectedBin, false);
                    _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
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
                    _ioManager?.Ctx?.Off(x => x.发送取料指令);
                    ClearBinSelectSignals();
                    SendCartCompletionToModbus(false);
                    _ioManager?.Ctx?.Off(x => x.触发机械手放置料仓);
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
            _selectedBin = 1;
            _lastScannedQrCode = string.Empty;
            _scanRetryCount = 0;
            _hasMaterial = true;
            _materialCheckTask = null;
            base.Rset();
        }
        private bool TryRetryMesFeedingRequest(string message)
        {
            if (_mesRetryCount < MAX_MES_RETRY_COUNT)
            {
                _uiLogger.WarnRaw("{0}，重试次数: {1}/{2}", message, _mesRetryCount, MAX_MES_RETRY_COUNT);
                ClearMesFeedingRequest();
                return true;
            }

            _uiLogger.ErrorRaw("{0}，已达到最大重试次数", message);
            _mesRetryCount = 0;
            ClearMesFeedingRequest();
            return false;
        }


        private void ClearMesFeedingRequest()
        {
            if (_mesFeedingCts != null)
            {
                try
                {
                    _mesFeedingCts.Cancel();
                }
                catch
                {
                }
                _mesFeedingCts.Dispose();
                _mesFeedingCts = null;
            }
            _mesFeedingTask = null;
        }
        /// <summary>
        /// 销毁时清理资源
        /// </summary>
        //public void Dispose()
        //{
        //    _ringLineSubscription?.Dispose();
        //    _ringLineSubscription = null;
        //}

        #endregion

        #region 辅助方法

        /// <summary>
        /// 处理环线数据消息
        /// </summary>
        /// <remarks>
        /// 边沿信号使用"或"逻辑锁存，防止被后续消息覆盖。
        /// 边沿信号在 Handler() 的初始状态中消费后清除。
        /// </remarks>
        private void OnRingLineData(RingLineDataMessage msg)
        {
            _parametersManager.Parameters._ringLineIsLoading = msg.IsLoading;
            if (msg.RisingEdge)
            {
                _parametersManager.Parameters._ringLineRisingEdge = true;
            }
            if (msg.FallingEdge)
            {
                _parametersManager.Parameters._ringLineFallingEdge = true;
            }
            if (!msg.IsLoading)
            {
                //_parametersManager.Parameters._ringLineArmed = true;
            }
            _parametersManager.Parameters._emptyCount = msg.EmptyCarCount;
            _parametersManager.Parameters._cuttingBridgeCarCount = msg.CuttingBridgeCarCount;
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
        /// 获取料框编号
        /// </summary>
        private string GetLiaokuangCode(int binNumber)
        {
            var parameters = _parametersManager?.Parameters;
            var template = parameters?.LiaokuangCodeTemplate ?? "BIN{0:D2}";

            try
            {
                return string.Format(template, binNumber);
            }
            catch
            {
                return $"BIN{binNumber:D2}";
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
                    _ioManager.Ctx.On(x => x.料仓1吹气电磁阀);
                    break;
                case 2:
                    _ioManager.Ctx.On(x => x.料仓2选择信号);
                    _ioManager.Ctx.On(x => x.料仓2吹气电磁阀);
                    break;
                case 3:
                    _ioManager.Ctx.On(x => x.料仓3选择信号);
                    _ioManager.Ctx.On(x => x.料仓3吹气电磁阀);
                    break;
            }
        }
        private void func_吹气(int binNumber, bool b_status)
        {
            if (_ioManager?.Ctx == null) return;

            //ClearBinSelectSignals();

            switch (binNumber)
            {
                case 1:
                    if (b_status)
                        _ioManager.Ctx.On(x => x.料仓1吹气电磁阀);
                    else
                        _ioManager.Ctx.Off(x => x.料仓1吹气电磁阀);
                    break;
                case 2:
                    if (b_status)
                        _ioManager.Ctx.On(x => x.料仓2吹气电磁阀);
                    else
                        _ioManager.Ctx.Off(x => x.料仓2吹气电磁阀);
                    break;
                case 3:
                    if (b_status)
                        _ioManager.Ctx.On(x => x.料仓3吹气电磁阀);
                    else
                        _ioManager.Ctx.Off(x => x.料仓3吹气电磁阀);
                    break;
            }
        }
        private void func_定位电磁阀(int binNumber, bool b_status)
        {
            if (_ioManager?.Ctx == null) return;

            //ClearBinSelectSignals();

            switch (binNumber)
            {
                case 1:
                    if (b_status)
                        _ioManager.Ctx.On(x => x.料仓1定位电磁阀);
                    else
                        _ioManager.Ctx.Off(x => x.料仓1定位电磁阀);
                    break;
                case 2:
                    if (b_status)
                        _ioManager.Ctx.On(x => x.料仓2定位电磁阀);
                    else
                        _ioManager.Ctx.Off(x => x.料仓2定位电磁阀);
                    break;
                case 3:
                    if (b_status)
                        _ioManager.Ctx.On(x => x.料仓3定位电磁阀);
                    else
                        _ioManager.Ctx.Off(x => x.料仓3定位电磁阀);
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
            _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
            //_ioManager.Ctx.Off(x => x.料仓1吹气电磁阀);
            //_ioManager.Ctx.Off(x => x.料仓2吹气电磁阀);
            //_ioManager.Ctx.Off(x => x.料仓3吹气电磁阀);
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
                _parametersManager.Parameters._ringLineRisingEdge = false;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "发送完成信号到Modbus失败", ex.Message);
            }
        }
        /// <summary>
        /// 只管前面三台
        /// </summary>

        public void func_获取状态()
        {
            var dict = mqttNet.Instance.DeviceDictionary;

            foreach (var temp in dict)
            {
                var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(temp.Value);
            }
        }

        public int func_设置空车数量()
        {
            var dict = mqttNet.Instance.DeviceDictionary;
            int i_空车数量 = 0;
            DateTime msgTime;
            bool b_008is_unloading = false;
            bool b_007is_unloading = false;
            foreach (var temp in dict)
            {
                var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(temp.Value);
                if (temp.Key.Contains("Z-JQ-S-82-008"))
                {
                    if (jsonObj["is_unloading"].ToString() == "True" && jsonObj["is_feeding"].ToString() == "False"
                        && jsonObj["is_running"].ToString() == "True")
                    {

                        // 解析时间格式
                        DateTime.TryParseExact(jsonObj["t"].ToString(), "yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture,
                                                   System.Globalization.DateTimeStyles.None, out msgTime);
                        if ((DateTime.Now - msgTime).TotalSeconds > 10)
                        {
                            i_空车数量++;
                        }
                    }
                    b_008is_unloading = jsonObj["is_unloading"].ToString() == "True";

                }
                else if (temp.Key.Contains("Z-JQ-S-82-006"))
                {
                    bool b = jsonObj["is_unloading"].ToString() == "True";
                    if (jsonObj["is_unloading"].ToString() == "True" && jsonObj["is_feeding"].ToString() == "False"
                         && jsonObj["is_running"].ToString() == "True" && !b_008is_unloading)//前面工站不需要下料，则后面工站不需要额外下空车
                    {
                        // 解析时间格式
                        DateTime.TryParseExact(jsonObj["t"].ToString(), "yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture,
                                                   System.Globalization.DateTimeStyles.None, out msgTime);
                        if ((DateTime.Now - msgTime).TotalSeconds > 10)
                        {
                            i_空车数量++;
                        }
                    }
                    b_007is_unloading = jsonObj["is_unloading"].ToString() == "True";
                }
                else if (temp.Key.Contains("Z-JQ-S-82-007"))
                {
                    if (jsonObj["is_unloading"].ToString() == "True" && jsonObj["is_feeding"].ToString() == "False"
                          && jsonObj["is_running"].ToString() == "True" && !b_007is_unloading)//前面工站不需要下料，则后面工站不需要额外下空车
                    {
                        // 解析时间格式
                        DateTime.TryParseExact(jsonObj["t"].ToString(), "yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture,
                                                   System.Globalization.DateTimeStyles.None, out msgTime);
                        if ((DateTime.Now - msgTime).TotalSeconds > 10)
                        {
                            i_空车数量++;
                        }
                    }
                }
            }
            return i_空车数量;
        }
        #endregion
    }

}
