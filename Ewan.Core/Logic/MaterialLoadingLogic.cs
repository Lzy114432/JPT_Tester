using Ewan.Core.IO;
using Ewan.Core.Mes;
using Ewan.Core.Plc;
using Ewan.Core.ScanCode;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Mqtt;
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
    /// 物料装载逻辑状态机
    /// 负责将扫码识别后的料片装载到对应的料仓中
    /// 基于 LogicBase + SwitchIndex 模式实现
    /// </summary>
    /// <remarks>
    /// 状态流程：
    /// 初始状态 → 检查前置条件 → 等待料片信号 → 取料中 → 到达扫码位置 → 执行扫码
    /// → 移动到料仓 → 等待装载完成 → 清理状态 → 结束状态
    /// </remarks>
    public class MaterialLoadingLogic : LogicBase
    {
        #region 私有字段

        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
        private readonly SystemParametersManager _parametersManager;

        private bool _beltStopRequested = false;
        private int _scanRetryCount = 0;
        private string _scannedCode = string.Empty;
        private int _targetBin = 1;
        private Task<MesRingLineFeedback> _mesFeedingTask;
        private CancellationTokenSource _mesFeedingCts;
        private string _billNoA = string.Empty;
        private string _billNoB = string.Empty;

        private string str_CurBill = string.Empty;
        private int _mesRetryCount = 0;
        private Stopwatch sw_状态刷新 = new Stopwatch();
        private int I_间隔上料 = 0;
        private bool b_状态刷新 = false;//第一次不看时间间隔，看标志位
        // 超时配置（毫秒）
        private const int WAIT_SCAN_POSITION_TIMEOUT = 50000;
        private const int WAIT_LOADING_COMPLETE_TIMEOUT = 150000;
        private const int MES_REQUEST_TIMEOUT_BUFFER_MS = 5000;
        private const int MAX_MES_RETRY_COUNT = 3;
        private const int WAIT_PUT_CART_TIMEOUT = 60000;
        private bool b_超时报警 = false;
        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public MaterialLoadingLogic()
        {
            _parametersManager = SystemParametersManager.Instance;
        }

        #endregion

        #region LogicBase 实现

        /// <summary>
        /// 状态机处理器
        ///// </summary>
        public override void Handler()
        {
            switch (SwitchIndex)
            {
                #region 初始状态
                case "初始状态":
                    // 检测料片信号，有料才切换步骤
                    if (_ioManager?.Ctx?.R.检测到料片信号 == true)
                    {
                        SwitchIndex = "等待料片信号";
                    }
                    else
                    {
                        // 无料时直接标记完成，不切换步骤，避免日志刷屏
                        IsFinish = true;
                    }
                    break;
                #endregion

                #region 等待料片信号
                case "等待料片信号":
                    // 有料片检测信号，允许取料
                    _ioManager.Ctx.On(x => x.触发机械手皮带线允许取料);
                    SwitchIndex = "取料中";
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 取料中
                case "取料中":
                    // 检测机械手忙碌信号，请求停止皮带
                    if (!_beltStopRequested && _ioManager?.Ctx?.R.机械手忙碌状态信号 == true)
                    {
                        _beltStopRequested = true;
                        var beltStopMessage = BeltConveyorControlMessage.Stop(
                            BeltConveyorControlSource.MaterialLoading,
                            "机械手正在取料，停止皮带");
                        MessageHub.Current.Post(beltStopMessage);
                    }
                    // X17 - 下相机报警信号（使用LayeredIO内置上升沿检测 + 防抖）
                    if (_ioManager.Ctx.Edge.R(x => x.下相机报警信号))
                    {
                        var beltReleaseMessage = BeltConveyorControlMessage.Release(
                            BeltConveyorControlSource.MaterialLoading,
                            "装料完成，释放皮带");
                        MessageHub.Current.Post(beltReleaseMessage);
                        SwitchIndex = "移动到料仓";
                        return;
                    }
                    // 检测是否到达扫码位置
                    if (_ioManager?.Ctx?.R.移至扫码区到位信号 == true)
                    {
                        SwitchIndex = "到达扫码位置";
                        return;
                    }

                    // 超时检查
                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    {
                        //MessageHub.Current.Post(new AlarmMessage(
                        //    key: "Loading.Timeout",
                        //    content: "等待到达扫码位置超时",
                        //    level: AlarmLevel.M,
                        //    needReset: false,
                        //    unit: "Loading"));
                        ForceCleanup("取料超时");
                        SwitchIndex = "清理状态";
                        return;
                    }
                    break;
                #endregion

                #region 到达扫码位置
                case "到达扫码位置":
                    // 禁止继续取料
                    _ioManager?.Ctx?.Off(x => x.触发机械手皮带线允许取料);
                    _ioManager?.Ctx?.On(x => x.定位夹持);//定位气缸夹料

                    var beltReleaseMessage1 = BeltConveyorControlMessage.Release(
                        BeltConveyorControlSource.MaterialLoading,
                        "装料完成，释放皮带");
                    MessageHub.Current.Post(beltReleaseMessage1);

                    SwitchIndex = "等待夹料完成";
                    _scanRetryCount = 0;
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 定位气缸与扫码优化
                case "等待夹料完成":
                    if (_ioManager?.Ctx?.R.夹持夹紧 == true) // 等待夹料完成信号
                    {
                        // 1. 通知机械手取料，同时立刻开始执行扫码
                        _ioManager?.Ctx?.On(x => x.发送扫码完成信号);
                        //Thread.Sleep(500);
                        //_ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
                        b_超时报警 = false;
                        _scanRetryCount = 0;
                        SwitchIndex = "信号交互";
                        Tw.StartWatch(SwitchIndex);
                        return;
                    }
                    if (Tw.StartCheckIsTimeout(SwitchIndex, 15000) && !b_超时报警)
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Loading.Timeout",
                            content: "等待夹料完成完成超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Loading"));
                        ForceCleanup("等待夹料完成完成超时");
                        b_超时报警 = true;
                    }
                    // 超时检查
                    //if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    //{
                    //    ForceCleanup("夹料超时");
                    //    SwitchIndex = "清理状态";
                    //    return;
                    //}
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
                                _scannedCode = (ScannerManager.Instance().TriggerScan() ?? string.Empty).Trim();

                            if (!string.IsNullOrWhiteSpace(_scannedCode) && _scannedCode != "")
                            {
                                _uiLogger.InfoRaw("扫码内容: {0}", _scannedCode);
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
                                _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
                                //Thread.Sleep(100);
                                _ioManager?.Ctx?.Off(x => x.定位夹持);
                            }
                            else
                            {
                                break;
                            }


                            // 2. 扫码完成，松开定位气缸夹料
                            //_ioManager?.Ctx?.On(x => x.发送扫码完成信号); // 3. 通知扫码完成

                            if (!isScanSuccess)
                            {
                                // MES启用但扫码失败，指定NG进入料仓3
                                _targetBin = 3;
                                _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                            }
                            else if (!mesEnabled)
                            {
                                // MES未启用获取默认料仓配置
                                _targetBin = GetConfiguredBinNumber();
                                SetBinSelectSignal(_targetBin);
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
                        // 确认气缸松开到位后，才通知扫码完成
                        _ioManager?.Ctx?.On(x => x.发送扫码完成信号);

                        SwitchIndex = "等待机械手取料到位";
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
                case "等待机械手取料到位":
                    //if (_ioManager?.Ctx?.R.机械臂定位完成信号 == true)
                    {
                        //_ioManager?.Ctx?.Off(x => x.发送扫码完成信号); // 停止发通知取料信号

                        var scanParameters = _parametersManager?.Parameters;
                        bool mesEnabled = scanParameters?.MesEnabled ?? false;

                        if (_scannedCode == "")
                        {
                            _uiLogger.Info("扫码失败");
                            ClearBinSelectSignals();
                            _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                            SwitchIndex = "移动到料仓";
                            return;
                        }
                        //else
                        {
                            if (mesEnabled && !string.IsNullOrWhiteSpace(_scannedCode))
                            {
                                SwitchIndex = "发送MES上料请求";
                            }
                            else
                            {
                                if ((_parametersManager.Parameters._ringLineRisingEdge || (_parametersManager.Parameters._ringLineIsLoading && _parametersManager.Parameters._ringLineArmed)))
                                {
                                    if (_parametersManager?.Parameters?.MesEnabled != true)
                                    {
                                        if ((SystemParametersManager.Instance.Parameters.UnloadingBinSelection == SystemParametersManager.Instance.Parameters.LoadingBinSelection))
                                        {
                                            SwitchIndex = "检查空车数量";
                                        }
                                        else
                                        {
                                            SwitchIndex = "移动到料仓";
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        SwitchIndex = "检查空车数量";
                                    }

                                    _parametersManager.Parameters._ringLineRisingEdge |= _parametersManager.Parameters._ringLineIsLoading && _parametersManager.Parameters._ringLineArmed;
                                    _parametersManager.Parameters._ringLineArmed = false;    
                                }
                                else
                                {
                                    SwitchIndex = "移动到料仓";
                                }
                            }
                        }
                    }

                    // 超时检查
                    //if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    //{
                    //    ForceCleanup("获取机械臂取料完成信号超时");
                    //    SwitchIndex = "清理状态";
                    //    return;
                    //}
                    break;

                #endregion

                #region 发送MES上料请求
                case "发送MES上料请求":
                    if (_parametersManager?.Parameters?.MesEnabled != true)
                    {
                        _mesRetryCount = 0;
                        ClearMesFeedingRequest();
                        SwitchIndex = "移动到料仓";
                        return;
                    }
                   
                    if (_mesFeedingTask == null)
                    {
                        var mesManager = MesManager.Instance();
                        if (!mesManager.IsConnected || !mesManager.IsRingLineInitialized)
                        {
                            _uiLogger.WarnRaw("MES未连接或未初始化，跳过上料请求");
                            _mesRetryCount = 0;
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
                        int _selectedBin = GetConfiguredBinNumber();
                        var request = new MesRingLineRequest
                        {
                            Action = MesRingLineAction.FeedingQianLiaocang,
                            PlateCode = _scannedCode,
                            BillNoWip = string.Empty,
                            //FeedingLiaokuangCode = GetLiaokuangCode(_selectedBin),
                            TimeoutMs = requestTimeoutMs
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
                            _uiLogger.WarnRaw("MES上料请求已取消");
                            _mesRetryCount = 0;
                            SwitchIndex = "移动到料仓";
                        }
                        else if (_mesFeedingTask.IsFaulted)
                        {
                            var error = _mesFeedingTask.Exception?.GetBaseException()?.Message ?? "未知错误";
                            if (TryRetryMesFeedingRequest($"MES上料请求异常: {error}"))
                            {
                                return;
                            }
                            SwitchIndex = "移动到料仓";
                        }
                        else
                        {
                            var feedback = _mesFeedingTask.Result;
                            if (feedback.Success)
                            {
                                var responseData = feedback.Data as FeedingQianLiaocangResponseData;
                                _billNoA = responseData?.BillNoA ?? string.Empty;
                                _billNoB = responseData?.BillNoB ?? string.Empty;
                                //INK
                                int binNo = 3; // 默认3号料仓

                                // 清理历史绑定，只保留当前A/B单号
                                var keysToRemove = _parametersManager?.Parameters?.dic_料仓单号.Keys
                                    .Where(k => k != _billNoA && k != _billNoB)
                                    .ToList();
                                foreach (var key in keysToRemove)
                                {
                                    _parametersManager?.Parameters?.dic_料仓单号.Remove(key);
                                }

                                int aBin = _parametersManager.Parameters.dic_料仓单号.ContainsKey(_billNoA) ? _parametersManager.Parameters.dic_料仓单号[_billNoA] : 1;
                                int bBin = aBin == 1 ? 2 : 1;
                                // 优先查找历史绑定
                                if (_parametersManager.Parameters.dic_料仓单号.TryGetValue(_scannedCode.Substring(0, 10), out binNo))
                                {
                                    SetBinSelectSignal(binNo);
                                    _parametersManager.Parameters.dic_有无料[binNo] = true;
                                }
                                else if (string.IsNullOrEmpty(_billNoA) || _scannedCode.Contains(_billNoA))
                                {
                                    if (string.IsNullOrEmpty(_billNoA))
                                    {
                                        _billNoA = _scannedCode.Substring(0, 10);
                                    }

                                    _parametersManager.Parameters.dic_料仓单号[_billNoA] = aBin;
                                    _parametersManager.Parameters.dic_料仓单号[_billNoB] = bBin;
                                    SetBinSelectSignal(aBin);
                                    _parametersManager.Parameters.dic_有无料[aBin] = true;
                                }
                                else if (string.IsNullOrEmpty(_billNoB) || _scannedCode.Contains(_billNoB))
                                {
                                    if (string.IsNullOrEmpty(_billNoB))
                                    {
                                        _billNoB = _scannedCode.Substring(0, 10);
                                    }
                                    _parametersManager.Parameters.dic_料仓单号[_billNoA] = aBin;
                                    _parametersManager.Parameters.dic_料仓单号[_billNoB] = bBin;
                                    SetBinSelectSignal(bBin);
                                    _parametersManager.Parameters.dic_有无料[bBin] = true;
                                }
                                else
                                {
                                    SetBinSelectSignal(3);
                                }
                                _parametersManager.Parameters.str_当前工单号 = responseData?.BillNoA;
                                _uiLogger.InfoRaw("MES上料响应: A单={0}, B单={1}", _billNoA, _billNoB);
                                _mesRetryCount = 0;
                                if (_scannedCode.Contains(_billNoA) && (_parametersManager.Parameters._ringLineRisingEdge || (_parametersManager.Parameters._ringLineIsLoading && _parametersManager.Parameters._ringLineArmed)))
                                {
                                    _parametersManager.Parameters._ringLineRisingEdge |= _parametersManager.Parameters._ringLineIsLoading && _parametersManager.Parameters._ringLineArmed;
                                    _parametersManager.Parameters._ringLineArmed = false;
                                    ClearBinSelectSignals();
                                    SwitchIndex = "检查空车数量";
                                }
                                else
                                {
                                    SwitchIndex = "移动到料仓";
                                }
                            }
                            else
                            {
                                if (TryRetryMesFeedingRequest($"MES上料请求失败: {feedback.Message}"))
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
                        if (TryRetryMesFeedingRequest($"MES上料请求异常: {ex.Message}"))
                        {
                            return;
                        }
                        _ioManager?.Ctx?.On(x => x.料仓3选择信号);
                        SwitchIndex = "移动到料仓";
                    }
                    finally
                    {
                        if (SwitchIndex != "发送MES上料请求")
                        {
                            ClearMesFeedingRequest();
                        }
                    }
                    break;
                #endregion


                #region 移动到料仓
                case "移动到料仓":
                    // 触发放入料仓信号
                    _ioManager?.Ctx?.On(x => x.触发机械手放置料仓);

                    SwitchIndex = "等待装载完成";
                    Tw.StartWatch(SwitchIndex);
                    break;
                #endregion

                #region 等待装载完成
                case "等待装载完成":
                    if (_ioManager?.Ctx?.R.机械臂放置完成信号 == true)
                    {
                        MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.LoadingCompleted(
                            _targetBin,
                            nameof(MaterialLoadingLogic)));
                        MessageHub.Current.Post(LoadingUnloadingStateMessage.LoadingCompleted(_targetBin, nameof(MaterialLoadingLogic)));
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

                #region 清理状态
                case "清理状态":
                    _ioManager?.Ctx?.Off(x => x.触发机械手放置料仓);
                    ClearBinSelectSignals();
                    _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);

                    if (_parametersManager?.Parameters?.MesEnabled == true && !string.IsNullOrWhiteSpace(_scannedCode))
                    {
                        var successRequest = new MesRingLineRequest
                        {
                            Action = MesRingLineAction.FeedingQianLiaocangSuccess,
                            PlateCode = _scannedCode,
                            FeedingLiaokuangCode = GetLiaokuangCode(_targetBin)
                        };
                        MessageHub.Current.Post(successRequest);
                    }

                    if (_beltStopRequested)
                    {
                        _beltStopRequested = false;
                        var beltReleaseMessage = BeltConveyorControlMessage.Release(
                            BeltConveyorControlSource.MaterialLoading,
                            "装料完成，释放皮带");
                        MessageHub.Current.Post(beltReleaseMessage);
                    }

                    Complete();
                    break;
                #endregion

                #region 发送放入小车指令

                case "检查空车数量":
                    {
                        var cartParameters = _parametersManager?.Parameters;
                        var cartCheckMode = cartParameters?.CartCheckMode ?? CartCheckMode.EmptyCart;
                        bool needSendEmptyCar = false;

                        if (cartCheckMode == CartCheckMode.EmptyCart)
                        {
                            var reserveCount = Math.Max(0, cartParameters?.EmptyCartReserveCount ?? 0);
                            if (cartParameters._emptyCount <= reserveCount)
                            {
                                needSendEmptyCar = true;
                            }
                        }
                        else
                        {
                            var reserveCount = Math.Max(0, cartParameters?.CuttingBridgeCarReserveCount ?? 0);
                            if (cartParameters._cuttingBridgeCarCount > reserveCount)
                            {
                                needSendEmptyCar = true;
                            }
                        }

                        if (needSendEmptyCar)
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
                        SwitchIndex = "发送MES下料信号";
                    }
                    break;
                case "发送MES下料信号":
                    if (_parametersManager?.Parameters?.MesEnabled != true)
                    {
                        _mesRetryCount = 0;
                        ClearMesFeedingRequest();
                        SwitchIndex = "发送放入小车指令";
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
                            PlateCode = _scannedCode,
                            FeedingLiaokuangCode = "皮带线"
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
                case "发送放入小车指令":
                    // 发送扫码完成信号给机械臂
                    _ioManager?.Ctx?.On(x => x.发送扫码完成信号);
                    // 发送放入小车信号
                    _ioManager?.Ctx?.On(x => x.发送放入小车指令);

                    SwitchIndex = "等待机械手到位";
                    Tw.StartWatch(SwitchIndex);
                    break;
                case "等待机械手到位"://判断小车是否到位
                    if (_ioManager?.Ctx?.Edge.R(x => x.DI5) == true)
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
                    if (_ioManager?.Ctx?.Edge.R(x => x.放入小车完成信号) == true)
                    {
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
                case "发送Modbus完成":
                    Thread.Sleep(500);
                    SendCartCompletionToModbus(true);
                    MessageHub.Current.Post(LoadingUnloadingStateMessage.UnloadingCompleted(_targetBin, nameof(MaterialLoadingLogic)));
                    SwitchIndex = "下料清理状态";
                    break;
                case "下料清理状态":
                    // 重置标志
                    if (_ioManager?.Ctx?.Edge.R(x => x.放入小车完成信号) == true)
                    {
                        break;
                    }
                    _scannedCode = string.Empty;
                    _ioManager?.Ctx?.Off(x => x.触发机械手放置料仓);
                    ClearBinSelectSignals();
                    _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);
                    Complete();
                    break;
                case "释放空车":
                    _scannedCode = string.Empty;
                    ClearBinSelectSignals();
                    SendCartCompletionToModbus(false);
                    if (_parametersManager.Parameters.MesEnabled && !string.IsNullOrWhiteSpace(_scannedCode))
                    {
                        SwitchIndex = "发送MES上料请求";
                    }
                    else
                    {
                        SwitchIndex = "移动到料仓";
                    }
                    break;
                #endregion

                #region 结束状态
                case "结束状态":
                    // 完成，等待下一个周期
                    IsFinish = true;//INK,结束默认下一个步骤
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
            _scanRetryCount = 0;
            _scannedCode = string.Empty;
            _targetBin = 1;
            _billNoA = string.Empty;
            _billNoB = string.Empty;
            _mesRetryCount = 0;
            ClearMesFeedingRequest();
            base.Rset();
        }
        // Modbus寄存器地址
        private const string CART_COMPLETION_REGISTER = "153";
        private const string MATERIAL_STATUS_REGISTER = "178";
        private void SendCartCompletionToModbus(bool hasMaterial)
        {
            try
            {
                ModbusRTUManager.Instance()?.WriteAny(CART_COMPLETION_REGISTER, (ushort)1);

                ushort statusValue = hasMaterial ? (ushort)1 : (ushort)0;
                ModbusRTUManager.Instance()?.WriteAny(MATERIAL_STATUS_REGISTER, statusValue);
                _parametersManager.Parameters._ringLineRisingEdge = false;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "发送完成信号到Modbus失败", ex.Message);
            }
        }
        #endregion
        #region 辅助方法

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
                    _ioManager.Ctx.Off(x => x.发送取料指令);
                    _ioManager.Ctx.Off(x => x.触发机械手放置料仓);
                    _ioManager.Ctx.Off(x => x.发送扫码完成信号);
                    ClearBinSelectSignals();
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("强制清理IO异常: {0}", ex.Message);
            }


            if (_beltStopRequested)
            {
                _beltStopRequested = false;
                var message = BeltConveyorControlMessage.Release(
                    BeltConveyorControlSource.MaterialLoading,
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
            var selected = _parametersManager?.Parameters?.LoadingBinSelection ?? BinSelection.Bin1;
            switch (selected)
            {
                case BinSelection.Bin2: return 2;
                case BinSelection.Bin3: return 3;
                default: return 1;
            }
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

        private bool TryRetryMesFeedingRequest(string message)
        {
            if (_mesRetryCount < MAX_MES_RETRY_COUNT)
            {
                _uiLogger.Info("{0}，重试次数: {1}/{2}", message, _mesRetryCount, MAX_MES_RETRY_COUNT);
                ClearMesFeedingRequest();
                return true;
            }

            _uiLogger.Info("{0}，已达到最大重试次数", message);
            _mesRetryCount = 0;
            ClearMesFeedingRequest();
            return false;
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
            _targetBin = binNumber;
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
