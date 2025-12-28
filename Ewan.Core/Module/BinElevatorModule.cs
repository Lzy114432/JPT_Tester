using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Model.Production;
using EwanCore.Messaging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降控制模块
    /// 负责在自动模式下根据感应器状态自动控制三个料仓的升降
    /// </summary>
    public class BinElevatorModule : BaseModule<BinElevatorModule>, IBinElevator
    {
        #region 私有字段

        private int _scanInterval = 1; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();

        // 共享状态（用于与其他模块通信）
        private ProductionLineSharedState _sharedState;

        private BinElevatorMode _binElevatorMode = BinElevatorMode.Init;
        private int _activeUnloadingBin = 0;

        // 料仓轴配置
        private const int BIN1_AXIS_ID = 0;
        private const int BIN2_AXIS_ID = 1;
        private const int BIN3_AXIS_ID = 2;

        // 料仓状态数组（替代原有的分散变量）
        private readonly BinState[] _bins = new BinState[]
        {
            new BinState(1, BIN1_AXIS_ID),
            new BinState(2, BIN2_AXIS_ID),
            new BinState(3, BIN3_AXIS_ID)
        };

        // 轴控制器、IO管理器
        private AxisManager _axisManager;
        private LayeredIOManager _ioManager;
        private IDisposable _systemStatusSubscription;
        private IDisposable _commandSubscription;
        private IDisposable _raiseToSensorResponder;

        // 下料物料检测相关
        private bool _materialDetectionInProgress = false;
        private int _materialDetectionBin = 0;
        private DateTime _materialDetectionStartTime = DateTime.MinValue;
        private readonly int _materialDetectionTimeoutMs = 5000;
        private Guid _materialDetectionCorrelationId = Guid.Empty;
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<BinElevatorStatusMessage>> _pendingMaterialChecks =
            new ConcurrentDictionary<Guid, TaskCompletionSource<BinElevatorStatusMessage>>();

        #endregion

        #region 辅助方法 - 料仓访问

        /// <summary>
        /// 获取指定编号的料仓状态对象
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        /// <returns>料仓状态对象，无效编号返回 null</returns>
        private BinState GetBin(int binNumber)
        {
            if (binNumber < 1 || binNumber > 3) return null;
            return _bins[binNumber - 1];
        }

        /// <summary>
        /// 检查所有料仓是否都已到达感应位置
        /// </summary>
        private bool AllBinsReachedSensor()
        {
            return _bins[0].ReachedSensor && _bins[1].ReachedSensor && _bins[2].ReachedSensor;
        }

        /// <summary>
        /// 重置所有料仓的 ReachedSensor 标志
        /// </summary>
        private void ResetAllBinsReachedSensor()
        {
            foreach (var bin in _bins)
            {
                bin.ReachedSensor = false;
            }
        }

        #endregion

        #region 构造函数

        
        /// <summary>
        /// 带共享状态的构造函数
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        public BinElevatorModule(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState;
        }

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.InfoRaw("模块初始化成功: {0}", "BinElevatorModule");

                // 初始化轴管理器、IO管理器
                _axisManager = AxisManager.Instance();
                _ioManager = LayeredIOManager.Instance();

                // 系统状态消息订阅预留（目前不处理任何消息）
                // _systemStatusSubscription = MessageHub.Current.Subscribe<SystemStatusMessage>(OnSystemStatusChanged);

                _commandSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(OnCommandReceived);
                _raiseToSensorResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                    HandleRaiseToSensorRequestAsync,
                    postReply: true);
                _uiLogger.InfoRaw("消息订阅已完成: {0}", "BinElevatorCommandMessage");

                _uiLogger.InfoRaw("初始化已完成: {0}", "料仓升降控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块初始化失败: {0} - {1}", "BinElevatorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                lock (_stateLock)
                {
                    // 检查暂停状态
                    if (_sharedState?.IsSystemPaused() == true)
                    {
                        // 暂停状态：停止所有轴运动但不退出循环
                        if (_binElevatorMode != BinElevatorMode.Stopped)
                        {
                            StopAllBinMovements();
                            _binElevatorMode = BinElevatorMode.Stopped;
                            _uiLogger.InfoRaw("处理已完成: {0}", "料仓升降模块已暂停");
                        }
                        Thread.Sleep(_scanInterval);
                        return true;
                    }
                    
                    // 检查是否需要重新初始化
                    if (_sharedState?.RequireReinit() == true)
                    {
                        _sharedState.SetRequireReinit(false);

                        // 重置到初始化状态
                        _binElevatorMode = BinElevatorMode.Init;
                        foreach (var bin in _bins)
                        {
                            bin.Reset();
                        }

                        _uiLogger.InfoRaw("处理已开始: {0}", "料仓重新初始化开始");
                    }

                    // 检查并处理三个料仓的感应器状态
                    foreach (var bin in _bins)
                    {
                        ProcessBinElevator(bin);
                    }

                    CheckMaterialDetectionCompletion();
                }
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("模块运行错误: {0} - {1}", "BinElevatorModule", ex.Message);
                Thread.Sleep(1000); // 错误时等待更长时间
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                // 停止所有料仓升降动作
                StopAllBinMovements();

                _commandSubscription?.Dispose();
                _commandSubscription = null;

                _raiseToSensorResponder?.Dispose();
                _raiseToSensorResponder = null;

                _systemStatusSubscription?.Dispose();
                _systemStatusSubscription = null;

                foreach (var pending in _pendingMaterialChecks)
                {
                    pending.Value?.TrySetCanceled();
                }
                _pendingMaterialChecks.Clear();
                
                _uiLogger.InfoRaw("模块已销毁: {0}", "BinElevatorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "BinElevatorModule销毁", ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 执行料仓升降硬件初始化（公共方法，供外部调用）
        /// 将模式设置为Init，触发料仓初始化流程
        /// </summary>
        public void PerformHardwareInitialization()
        {
            lock (_stateLock)
            {
                _binElevatorMode = BinElevatorMode.Init;
                foreach (var bin in _bins)
                {
                    bin.Reset();
                }

                _uiLogger.InfoRaw("处理已开始: {0}", "料仓升降硬件初始化开始");
            }
        }

        /// <summary>
        /// 强制停止所有料仓并将状态机置为停止
        /// </summary>
        public void ForceStopAllBins()
        {
            lock (_stateLock)
            {
                StopAllBinMovements();
                _binElevatorMode = BinElevatorMode.Stopped;
                foreach (var bin in _bins)
                {
                    bin.Stop();
                }
                _activeUnloadingBin = 0;
            }
        }

        /// <summary>
        /// 将指定料仓上升到有料感应位置，并返回物料检测结果（异步）
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        /// <param name="ct">取消令牌</param>
        public async Task<BinElevatorStatusMessage> RaiseToSensorAsync(int binNumber, CancellationToken ct = default)
        {
            if (binNumber < 1 || binNumber > 3)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}", "RaiseToSensorAsync", $"无效的料仓编号 {binNumber}");
                return BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Error, "无效料仓编号");
            }

            if (ReadBinSensor(binNumber))
            {
                return BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.HasMaterial, "已有感应信号");
            }

            var request = BinElevatorCommandMessage.RaiseToSensor(binNumber, nameof(BinElevatorModule));

            try
            {
                var reply = await MessageHub.Current.RequestAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                        request,
                        _materialDetectionTimeoutMs,
                        ct)
                    .ConfigureAwait(false);

                return reply ?? BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Error, "响应为空");
            }
            catch (TimeoutException)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}", $"料仓{binNumber}检测超时", "等待响应超时");
                return BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Timeout, "超时", "等待响应超时");
            }
            catch (OperationCanceledException)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}", $"料仓{binNumber}检测取消", "等待响应取消");
                return BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Error, "操作取消");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"料仓{binNumber}检测失败", ex.Message);
                return BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Error, "检测失败", ex.Message);
            }
        }

        #endregion

        #region 核心升降控制逻辑

        /// <summary>
        /// 处理单个料仓的升降控制 - 料仓状态机
        /// </summary>
        /// <param name="bin">料仓状态对象</param>
        private void ProcessBinElevator(BinState bin)
        {
            try
            {
                // 料仓状态机逻辑 - 使用switch优化
                switch (_binElevatorMode)
                {
                    case BinElevatorMode.Init:
                        // 状态机0: 初始化模式 - 先上升到感应位置，再下降到无感应，保持停止
                        ProcessInitMode(bin);
                        break;

                    case BinElevatorMode.Loading:
                        // 状态机1: 上料模式 - 下降到感应器有信号就停止
                        ProcessLoadingMode(bin);
                        break;

                    case BinElevatorMode.Unloading:
                        // 状态机2: 下料模式 - 上升到感应位置后，下降到无感应就停止
                        ProcessUnloadingMode(bin);
                        break;

                    case BinElevatorMode.Stopped:
                        // 停止模式 - 不执行任何升降控制
                        break;

                    default:
                        // 未知模式 - 记录警告
                        _uiLogger.WarnRaw("处理错误: {0} - {1}",
                            "料仓" + bin.BinNumber + "未知的料仓状态机模式", _binElevatorMode.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "料仓" + bin.BinNumber + "升降处理", ex.Message);
            }
        }


        #region 状态机实现


        /// <summary>
        /// 状态机0: 初始化模式 - 先上升到感应位置，再下降到无感应，最后保持停止
        /// </summary>
        private void ProcessInitMode(BinState bin)
        {
            // 根据当前状态执行不同的初始化步骤
            switch (bin.CurrentState)
            {
                case BinElevatorState.Stopped:
                    // 已经初始化完成，保持静止
                    break;
                case BinElevatorState.Unknown:
                    // 第一步：开始上升到感应位置
                    if (!ReadBinSensor(bin.BinNumber)) // 料仓感应为false 就是上升
                    {
                        StartBinJogUp(bin);
                        bin.CurrentState = BinElevatorState.Moving;
                    }
                    else
                    {
                        // 如果已经有感应，直接进入下降阶段
                        bin.CurrentState = BinElevatorState.Elevated;
                    }
                    break;

                case BinElevatorState.Moving:
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                    }
                    break;

                case BinElevatorState.Elevated:
                    // 第二步：从感应位置下降
                    StartBinJogDown(bin);
                    bin.CurrentState = BinElevatorState.Lowered; // 标记为正在下降
                    break;

                case BinElevatorState.Lowered:
                    // 正在下降中，检查是否脱离感应位置
                    if (!ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);

                        // 标记该料仓初始化完成
                        bin.ReachedSensor = true;

                        // 检查是否所有料仓都初始化完成
                        if (AllBinsReachedSensor())
                        {
                            _uiLogger.InfoRaw("处理已完成: {0}",
                                "所有料仓初始化完成，切换到停止模式");

                            // 重置标志
                            ResetAllBinsReachedSensor();
                        }

                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                    break;
            }
        }


        /// <summary>
        /// 状态机1: 上料模式 - 下降到感应器有信号就停止
        /// </summary>
        private void ProcessLoadingMode(BinState bin)
        {
            switch (bin.CurrentState)
            {
                case BinElevatorState.Unknown:
                    // 检查当前感应器状态
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        // 开始下降动作
                        StartBinJogDown(bin);
                        bin.CurrentState = BinElevatorState.Moving;
                    }
                    else
                    {
                        // 感应器已经为false，直接完成
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                    break;

                case BinElevatorState.Moving:
                    // 正在下降中，检查是否到达感应位置
                    if (!ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                    break;
                case BinElevatorState.Stopped:
                    break;
            }
        }

        /// <summary>
        /// 状态机4: 下料模式 - 指定料仓上升至有料感应后停止
        /// </summary>
        private void ProcessUnloadingMode(BinState bin)
        {
            bool isActiveBin = _activeUnloadingBin != 0 && _activeUnloadingBin == bin.BinNumber;
            if (!isActiveBin)
            {
                if (bin.CurrentState == BinElevatorState.Moving)
                {
                    StopBinAxis(bin);
                    bin.CurrentState = BinElevatorState.Stopped;
                }
                return;
            }

            bool detectionActive = _materialDetectionInProgress && _materialDetectionBin == bin.BinNumber;
            double elapsedMs = detectionActive
                ? (DateTime.UtcNow - _materialDetectionStartTime).TotalMilliseconds
                : 0;

            switch (bin.CurrentState)
            {
                case BinElevatorState.Unknown:
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        HandleUnloadingReached(bin.BinNumber, detectionActive, false);
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                    else
                    {
                        StartBinJogUp(bin);
                        bin.CurrentState = BinElevatorState.Moving;
                    }
                    break;

                case BinElevatorState.Moving:
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        HandleUnloadingReached(bin.BinNumber, detectionActive, false);
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                    else if (detectionActive && elapsedMs >= _materialDetectionTimeoutMs)
                    {
                        StopBinAxis(bin);
                        HandleUnloadingReached(bin.BinNumber, true, true);
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                    break;

                case BinElevatorState.Stopped:
                    if (detectionActive)
                    {
                        if (ReadBinSensor(bin.BinNumber))
                        {
                            HandleUnloadingReached(bin.BinNumber, true, false);
                        }
                        else if (elapsedMs >= _materialDetectionTimeoutMs)
                        {
                            HandleUnloadingReached(bin.BinNumber, true, true);
                        }
                    }
                    break;

                default:
                    bin.CurrentState = BinElevatorState.Unknown;
                    break;
            }
        }

        private void HandleUnloadingReached(int binNumber, bool detectionActive, bool timedOut)
        {
            if (detectionActive)
            {
                return;
            }
            else
            {
                CompleteUnloadingRaise(binNumber);
            }
        }

        private void CompleteUnloadingRaise(int binNumber)
        {
            var bin = GetBin(binNumber);
            if (bin != null)
            {
                bin.CurrentState = BinElevatorState.Stopped;
            }

            _activeUnloadingBin = 0;
            _binElevatorMode = BinElevatorMode.Stopped;

            _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}已上升至有料感应位置");
        }

        private void CompleteMaterialCheck(int binNumber, bool hasMaterial, bool timedOut)
        {
            if (!_materialDetectionInProgress || _materialDetectionBin != binNumber)
            {
                return;
            }

            var operationResult = hasMaterial
                ? BinOperationResult.HasMaterial
                : (timedOut ? BinOperationResult.Timeout : BinOperationResult.NoMaterial);

            var statusMessage = BinElevatorStatusMessage.MaterialCheckResult(
                binNumber,
                operationResult,
                hasMaterial ? "检测到物料" : "无料",
                timedOut ? "超时判空" : string.Empty);

            TaskCompletionSource<BinElevatorStatusMessage> tcs = null;
            if (_materialDetectionCorrelationId != Guid.Empty)
            {
                _pendingMaterialChecks.TryRemove(_materialDetectionCorrelationId, out tcs);
            }

            tcs?.TrySetResult(statusMessage);

            ResetMaterialDetectionState(binNumber);

            if (hasMaterial)
            {
                _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}检测到物料");
            }
            else
            {
                string reason = timedOut ? "超时判空" : "检测判空";
                _uiLogger.WarnRaw("处理错误: {0} - {1}", $"料仓{binNumber}无料", reason);
            }
        }

        private void CompleteMaterialCheckWithError(int binNumber, string reason)
        {
            if (!_materialDetectionInProgress || _materialDetectionBin != binNumber)
            {
                return;
            }

            var statusMessage = BinElevatorStatusMessage.MaterialCheckResult(
                binNumber,
                BinOperationResult.Error,
                "物料检测失败",
                reason);

            TaskCompletionSource<BinElevatorStatusMessage> tcs = null;
            if (_materialDetectionCorrelationId != Guid.Empty)
            {
                _pendingMaterialChecks.TryRemove(_materialDetectionCorrelationId, out tcs);
            }

            tcs?.TrySetResult(statusMessage);

            ResetMaterialDetectionState(binNumber);
        }

        private void CompleteMaterialCheckTimeout(int binNumber)
        {
            var bin = GetBin(binNumber);
            if (bin != null)
            {
                StopBinAxis(bin);
            }

            CompleteMaterialCheck(binNumber, false, true);
        }

        private void CancelMaterialDetection(string reason)
        {
            if (!_materialDetectionInProgress)
            {
                return;
            }

            int binNumber = _materialDetectionBin;
            var bin = GetBin(binNumber);
            if (bin != null)
            {
                StopBinAxis(bin);
            }

            CompleteMaterialCheckWithError(binNumber, reason);
            _uiLogger.WarnRaw("处理错误: {0} - {1}", $"料仓{binNumber}物料检测被中断", reason);
        }

        private void CheckMaterialDetectionCompletion()
        {
            if (!_materialDetectionInProgress || _materialDetectionBin <= 0)
            {
                return;
            }

            bool hasMaterial = ReadBinSensor(_materialDetectionBin);
            bool timedOut = (DateTime.UtcNow - _materialDetectionStartTime).TotalMilliseconds >= _materialDetectionTimeoutMs;

            if (!hasMaterial && !timedOut)
            {
                return;
            }

            var bin = GetBin(_materialDetectionBin);
            if (bin != null)
            {
                StopBinAxis(bin);
            }

            CompleteMaterialCheck(_materialDetectionBin, hasMaterial, timedOut);
        }

        private void ResetMaterialDetectionState(int binNumber)
        {
            _materialDetectionInProgress = false;
            _materialDetectionBin = 0;
            _materialDetectionStartTime = DateTime.MinValue;
            _materialDetectionCorrelationId = Guid.Empty;
            _activeUnloadingBin = 0;
            _binElevatorMode = BinElevatorMode.Stopped;

            SetBinStateStopped(binNumber);
        }

        private void SetBinStateStopped(int binNumber)
        {
            var bin = GetBin(binNumber);
            if (bin != null)
            {
                bin.CurrentState = BinElevatorState.Stopped;
            }
        }

        private void SetBinStateUnknown(int binNumber)
        {
            var bin = GetBin(binNumber);
            if (bin != null)
            {
                bin.CurrentState = BinElevatorState.Unknown;
            }
        }

        private void HandleStopCommand(int binNumber)
        {
            var bin = GetBin(binNumber);
            if (bin == null)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}", "StopBinAxis", $"无效的料仓编号 {binNumber}");
                return;
            }

            lock (_stateLock)
            {
                StopBinAxis(bin);
                bin.CurrentState = BinElevatorState.Stopped;
            }
        }

        private int GetAxisIdByBin(int binNumber)
        {
            switch (binNumber)
            {
                case 1:
                    return BIN1_AXIS_ID;
                case 2:
                    return BIN2_AXIS_ID;
                case 3:
                    return BIN3_AXIS_ID;
                default:
                    return -1;
            }
        }


        #endregion


        #endregion

        #region 轴控制操作

        /// <summary>
        /// 开始料仓Jog下降运动
        /// </summary>
        /// <param name="bin">料仓状态对象</param>
        private void StartBinJogDown(BinState bin)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(bin.AxisId);
                    if (axisConfig != null)
                    {
                        _axisManager.JogDown(axisConfig);
                        _uiLogger.InfoRaw("处理已开始: {0}",
                            "料仓" + bin.BinNumber + "开始Jog下降，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                            "料仓" + bin.BinNumber + "轴配置未找到", "轴ID:" + bin.AxisId);
                    }
                }
                else
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                        "AxisManager未初始化", "");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "料仓" + bin.BinNumber + "Jog下降", ex.Message);
            }
        }

        /// <summary>
        /// 开始料仓Jog上升运动
        /// </summary>
        /// <param name="bin">料仓状态对象</param>
        private void StartBinJogUp(BinState bin)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(bin.AxisId);
                    if (axisConfig != null)
                    {
                        _axisManager.JogUp(axisConfig);
                        _uiLogger.InfoRaw("处理已开始: {0}",
                            "料仓" + bin.BinNumber + "开始Jog上升，速度:" + axisConfig.Speed);
                    }
                    else
                    {
                        _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                            "料仓" + bin.BinNumber + "轴配置未找到", "轴ID:" + bin.AxisId);
                    }
                }
                else
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                        "AxisManager未初始化", "");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "料仓" + bin.BinNumber + "Jog上升", ex.Message);
            }
        }

        /// <summary>
        /// 停止料仓轴运动
        /// </summary>
        /// <param name="bin">料仓状态对象</param>
        private void StopBinAxis(BinState bin)
        {
            try
            {
                if (_axisManager != null)
                {
                    var axisConfig = _axisManager.GetAxisConfig(bin.AxisId);
                    if (axisConfig != null)
                    {
                        _axisManager.JogStop(axisConfig);
                    }
                    else
                    {
                        _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                            "料仓" + bin.BinNumber + "轴配置未找到", "轴ID:" + bin.AxisId);
                    }
                }
                else
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                        "AxisManager未初始化", "");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "料仓" + bin.BinNumber + "停止", ex.Message);
            }
        }


        #endregion

        #region 料仓状态重置

        /// <summary>
        /// 根据Y11/Y12/Y13信号重置选中料仓的状态并切换模式
        /// </summary>
        /// <param name="mode">要切换到的料仓模式</param>
        private void ResetSelectedBinStates(BinElevatorMode mode)
        {
            // 设置料仓模式
            if (mode == BinElevatorMode.Unloading)
            {
                _binElevatorMode = BinElevatorMode.Stopped;
                _activeUnloadingBin = 0;
            }
            else
            {
                _binElevatorMode = mode;
            }

            // 根据Y11/Y12/Y13信号选择性重置料仓状态
            var targetState = mode == BinElevatorMode.Unloading ? BinElevatorState.Stopped : BinElevatorState.Unknown;

            if (_ioManager.Ctx.R.料仓1选择信号)
            {
                _bins[0].CurrentState = targetState;
            }
            if (_ioManager.Ctx.R.料仓2选择信号)
            {
                _bins[1].CurrentState = targetState;
            }
            if (_ioManager.Ctx.R.料仓3选择信号)
            {
                _bins[2].CurrentState = targetState;
            }
        }

        #endregion

        #region 感应器检测

        /// <summary>
        /// 读取料仓感应器状态
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <returns>感应器状态（true=有料，false=无料）</returns>
        private bool ReadBinSensor(int binNumber)
        {
            try
            {
                switch (binNumber)
                {
                    case 1:
                        return _ioManager.Ctx.R.料仓1有料感应;
                    case 2:
                        return _ioManager.Ctx.R.料仓2有料感应;
                    case 3:
                        return _ioManager.Ctx.R.料仓3有料感应;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("读取 {0} 错误: {1}", 
                    "料仓" + binNumber + "感应器", ex.Message);
                return false;
            }
        }

        #endregion

        #region 初始化和配置


        /// <summary>
        /// 停止所有料仓移动
        /// </summary>
        private void StopAllBinMovements()
        {
            try
            {
                _uiLogger.InfoRaw("处理已开始: {0}", "停止所有料仓Jog运动");

                // 停止所有轴的Jog移动并重置状态
                foreach (var bin in _bins)
                {
                    StopBinAxis(bin);
                    bin.Reset();
                }

                _activeUnloadingBin = 0;
                CancelMaterialDetection("停止所有料仓运动");

                _uiLogger.InfoRaw("处理已完成: {0}", "所有料仓Jog运动已停止");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "停止料仓Jog运动", ex.Message);
            }
        }

        #endregion


        #region 消息处理

        private void OnCommandReceived(BinElevatorCommandMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (message.Command == BinCommand.RaiseToSensor)
            {
                return;
            }

            try
            {
                switch (message.Command)
                {
                    case BinCommand.Initialize:
                        PerformHardwareInitialization();
                        break;
                    case BinCommand.ForceStopAll:
                        ForceStopAllBins();
                        break;
                    case BinCommand.Stop:
                        HandleStopCommand(message.BinNumber);
                        break;
                    case BinCommand.LoadingCompleted:
                        lock (_stateLock)
                        {
                            ResetSelectedBinStates(BinElevatorMode.Loading);
                        }
                        break;
                    case BinCommand.UnloadingCompleted:
                        lock (_stateLock)
                        {
                            ResetSelectedBinStates(BinElevatorMode.Unloading);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "BinElevatorModule消息处理", ex.Message);
            }
        }

        private async Task<BinElevatorStatusMessage> HandleRaiseToSensorRequestAsync(BinElevatorCommandMessage request)
        {
            if (request == null)
            {
                return BinElevatorStatusMessage.MaterialCheckResult(0, BinOperationResult.Error, "请求为空", "请求为空");
            }

            if (request.Command != BinCommand.RaiseToSensor)
            {
                return null;
            }

            int binNumber = request.BinNumber;
            if (binNumber < 1 || binNumber > 3)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}", "RaiseToSensor", $"无效的料仓编号 {binNumber}");
                var invalidReply = BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber,
                    BinOperationResult.Error,
                    "无效料仓编号",
                    $"无效的料仓编号 {binNumber}");
                invalidReply.CommandId = request.CommandId;
                invalidReply.CurrentCommand = request.Command;
                return invalidReply;
            }

            if (ReadBinSensor(binNumber))
            {
                _uiLogger.InfoRaw("处理已完成: {0}", $"料仓{binNumber}已检测到物料，无需上升");
                var immediateReply = BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber,
                    BinOperationResult.HasMaterial,
                    "已有感应信号");
                immediateReply.CommandId = request.CommandId;
                immediateReply.CurrentCommand = request.Command;
                return immediateReply;
            }

            Guid correlationId = request.CorrelationId;
            if (correlationId == Guid.Empty)
            {
                correlationId = Guid.NewGuid();
                request.CorrelationId = correlationId;
            }

            TaskCompletionSource<BinElevatorStatusMessage> tcs =
                new TaskCompletionSource<BinElevatorStatusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_stateLock)
            {
                if (_materialDetectionInProgress)
                {
                    var busyReply = BinElevatorStatusMessage.MaterialCheckResult(
                        binNumber,
                        BinOperationResult.Error,
                        "检测中",
                        "已有检测正在执行");
                    busyReply.CommandId = request.CommandId;
                    busyReply.CurrentCommand = request.Command;
                    return busyReply;
                }

                if (!_pendingMaterialChecks.TryAdd(correlationId, tcs))
                {
                    var duplicateReply = BinElevatorStatusMessage.MaterialCheckResult(
                        binNumber,
                        BinOperationResult.Error,
                        "检测中",
                        "重复的检测请求");
                    duplicateReply.CommandId = request.CommandId;
                    duplicateReply.CurrentCommand = request.Command;
                    return duplicateReply;
                }

                _materialDetectionCorrelationId = correlationId;
                _materialDetectionInProgress = true;
                _materialDetectionBin = binNumber;
                _materialDetectionStartTime = DateTime.UtcNow;

                _activeUnloadingBin = binNumber;
                _binElevatorMode = BinElevatorMode.Unloading;
                SetBinStateUnknown(binNumber);
            }

            try
            {
                var timeoutTask = Task.Delay(_materialDetectionTimeoutMs);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);
                if (completedTask != tcs.Task)
                {
                    lock (_stateLock)
                    {
                        CompleteMaterialCheckTimeout(binNumber);
                    }
                }

                var reply = await tcs.Task.ConfigureAwait(false);
                if (reply != null)
                {
                    reply.CommandId = request.CommandId;
                    reply.CurrentCommand = request.Command;
                }
                return reply;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", $"料仓{binNumber}检测失败", ex.Message);
                lock (_stateLock)
                {
                    CompleteMaterialCheckWithError(binNumber, ex.Message);
                }
                var errorReply = BinElevatorStatusMessage.MaterialCheckResult(binNumber, BinOperationResult.Error, "检测异常", ex.Message);
                errorReply.CommandId = request.CommandId;
                errorReply.CurrentCommand = request.Command;
                return errorReply;
            }
        }

        /// <summary>
        /// 处理系统状态变化消息
        /// </summary>
        /// <param name="msg">系统状态消息</param>
        // 预留系统状态消息处理（目前不处理任何消息，保留接口以备将来扩展）
        // private void OnSystemStatusChanged(SystemStatusMessage msg)
        // {
        //     // 后续通过msg进行系统启动/停止和模式切换
        // }

        #endregion

    }
}
