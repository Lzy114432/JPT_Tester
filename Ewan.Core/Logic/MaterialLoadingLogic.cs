using Ewan.Core.IO;
using Ewan.Core.Mes;
using Ewan.Core.ScanCode;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Model.Messages;
using Ewan.Model.Production;
using Ewan.Model.System;
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;
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
        private int _mesRetryCount = 0;

        // 超时配置（毫秒）
        private const int WAIT_SCAN_POSITION_TIMEOUT = 100000;
        private const int WAIT_LOADING_COMPLETE_TIMEOUT = 150000;
        private const int MES_REQUEST_TIMEOUT_BUFFER_MS = 5000;
        private const int MAX_MES_RETRY_COUNT = 3;

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
        /// </summary>
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

                    // 检测是否到达扫码位置
                    if (_ioManager?.Ctx?.R.移至扫码区到位信号 == true)
                    {
                        SwitchIndex = "到达扫码位置";
                        return;
                    }

                    // 超时检查
                    if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
                    {
                        MessageHub.Current.Post(new AlarmMessage(
                            key: "Loading.Timeout",
                            content: "等待到达扫码位置超时",
                            level: AlarmLevel.M,
                            needReset: false,
                            unit: "Loading"));
                        ForceCleanup("取料超时");
                    }
                    break;
                #endregion

                #region 到达扫码位置
                case "到达扫码位置":
                    // 禁止继续取料
                    _ioManager?.Ctx?.Off(x => x.触发机械手皮带线允许取料);

                    SwitchIndex = "执行扫码";
                    _scanRetryCount = 0;
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
                            _scannedCode = (ScannerManager.Instance().TriggerScan() ?? string.Empty).Trim();

                            if (!string.IsNullOrWhiteSpace(_scannedCode))
                            {
                                _uiLogger.InfoRaw("扫码内容: {0}", _scannedCode);
                            }
                            else if (_scanRetryCount < maxRetry)
                            {
                                return;
                            }
                            else
                            {
                                //MessageHub.Current.Post(new AlarmMessage(
                                //    key: "Scan.Failed",
                                //    content: $"装料扫码失败：连续扫码{maxRetry}次无结果",
                                //    level: AlarmLevel.L,
                                //    needReset: false,
                                //    unit: "Scanner"));
                            }
                        }

                        // 发送扫码完成信号
                        _ioManager?.Ctx?.On(x => x.发送扫码完成信号);

                        // 获取目标料仓
                        _targetBin = GetConfiguredBinNumber();
                        SetBinSelectSignal(_targetBin);

                        if (mesEnabled)
                        {
                            SwitchIndex = "发送MES上料请求";
                        }
                        else
                        {
                            SwitchIndex = "移动到料仓";
                        }
                    }
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
                        var request = new MesRingLineRequest
                        {
                            Action = MesRingLineAction.FeedingQianLiaocang,
                            PlateCode = _scannedCode,
                            BillNoWip = string.Empty,
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
                                _uiLogger.InfoRaw("MES上料响应: A单={0}, B单={1}", _billNoA, _billNoB);
                                _mesRetryCount = 0;
                                SwitchIndex = "移动到料仓";
                            }
                            else
                            {
                                if (TryRetryMesFeedingRequest($"MES上料请求失败: {feedback.Message}"))
                                {
                                    return;
                                }

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
                    if (_ioManager?.Ctx?.Edge.F(x => x.机械臂放置完成信号) == true)
                    {
                        MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.LoadingCompleted(
                            _targetBin,
                            nameof(MaterialLoadingLogic)));
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
            _scanRetryCount = 0;
            _scannedCode = string.Empty;
            _targetBin = 1;
            _billNoA = string.Empty;
            _billNoB = string.Empty;
            _mesRetryCount = 0;
            ClearMesFeedingRequest();
            base.Rset();
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
                _uiLogger.WarnRaw("{0}，重试次数: {1}/{2}", message, _mesRetryCount, MAX_MES_RETRY_COUNT);
                ClearMesFeedingRequest();
                return true;
            }

            _uiLogger.ErrorRaw("{0}，已达到最大重试次数", message);
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

        #endregion
    }
}
