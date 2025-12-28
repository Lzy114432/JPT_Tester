using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Model.Production;
using EwanCore.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降模块（消息驱动模式）：
    /// - OnInit：注册 MessageHub 消息处理器
    /// - OnRun：处理活跃任务，保持模块存活
    /// - OnDestroy：清理订阅
    /// </summary>
    public class BinElevatorModule : BaseModule<BinElevatorModule>
    {
        private const int RunLoopIntervalMs = 1;
        private const int InitializeTimeoutMs = 30000;
        private const int RaiseToSensorTimeoutMs = 10000;

        #region 私有字段

        private readonly object _stateLock = new object();
        private AxisManager _axisManager;
        private LayeredIOManager _ioManager;

        // 消息订阅
        private IDisposable _forceStopSubscription;
        private IDisposable _loadingCompletedSubscription;
        private IDisposable _initializeResponder;
        private IDisposable _raiseToSensorResponder;

        // 料仓状态
        private readonly BinState[] _bins = new BinState[]
        {
            new BinState(1, 0),
            new BinState(2, 1),
            new BinState(3, 2)
        };

        // 活跃任务
        private readonly Dictionary<int, BinTask> _activeTasks = new Dictionary<int, BinTask>();
        private InitializeTask _initializeTask;

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            _axisManager = AxisManager.Instance();
            _ioManager = LayeredIOManager.Instance();

            _forceStopSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(
                msg => msg.Command == BinCommand.ForceStopAll,
                OnForceStopAll);

            _loadingCompletedSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(
                msg => msg.Command == BinCommand.LoadingCompleted,
                OnLoadingCompleted);

            _initializeResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                HandleInitializeAsync,
                postReply: true);

            _raiseToSensorResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                HandleRaiseToSensorAsync,
                postReply: true);

            _uiLogger.InfoRaw("模块初始化成功: {0}", "BinElevatorModule (MessageHub)");
        }

        protected override bool OnRun()
        {
            try
            {
                lock (_stateLock)
                {
                    if (_initializeTask != null)
                    {
                        ProcessInitializeTask();
                    }

                    foreach (var task in _activeTasks.Values.ToList())
                    {
                        ProcessBinTask(task);
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("BinElevator 处理异常: {0}", ex.Message);
            }

            Thread.Sleep(RunLoopIntervalMs);
            return true;
        }

        protected override void OnDestroy()
        {
            try
            {
                lock (_stateLock)
                {
                    foreach (var bin in _bins)
                    {
                        StopBinAxis(bin);
                        bin.Reset();
                    }

                    foreach (var task in _activeTasks.Values)
                    {
                        task.Completion?.TrySetCanceled();
                    }
                    _activeTasks.Clear();

                    _initializeTask?.Completion?.TrySetCanceled();
                    _initializeTask = null;
                }
            }
            catch
            {
            }

            _forceStopSubscription?.Dispose();
            _loadingCompletedSubscription?.Dispose();
            _initializeResponder?.Dispose();
            _raiseToSensorResponder?.Dispose();

            _uiLogger.InfoRaw("模块已销毁: {0}", "BinElevatorModule");
        }

        #endregion

        #region 消息处理器

        private void OnForceStopAll(BinElevatorCommandMessage message)
        {
            lock (_stateLock)
            {
                foreach (var bin in _bins)
                {
                    StopBinAxis(bin);
                    bin.Reset();
                }

                foreach (var task in _activeTasks.Values)
                {
                    task.Completion?.TrySetCanceled();
                }
                _activeTasks.Clear();

                _initializeTask?.Completion?.TrySetCanceled();
                _initializeTask = null;

                _uiLogger.InfoRaw("所有料仓已停止");
            }
        }

        private void OnLoadingCompleted(BinElevatorCommandMessage message)
        {
            int binNumber = message.BinNumber;
            if (binNumber < 1 || binNumber > 3)
            {
                _uiLogger.WarnRaw("无效的料仓编号: {0}", binNumber);
                return;
            }

            lock (_stateLock)
            {
                if (_activeTasks.TryGetValue(binNumber, out var existingTask))
                {
                    existingTask.Completion?.TrySetCanceled();
                }

                var bin = GetBin(binNumber);
                if (bin != null)
                {
                    bin.CurrentState = BinElevatorState.Unknown;
                }

                _activeTasks[binNumber] = new BinTask
                {
                    BinNumber = binNumber,
                    TaskType = BinTaskType.Loading,
                    StartTime = DateTime.UtcNow
                };

                _uiLogger.InfoRaw("料仓{0}开始下降", binNumber);
            }
        }

        private async Task<BinElevatorStatusMessage> HandleInitializeAsync(BinElevatorCommandMessage request)
        {
            if (request == null || request.Command != BinCommand.Initialize)
            {
                return null;
            }

            var tcs = new TaskCompletionSource<BinElevatorStatusMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_stateLock)
            {
                _initializeTask?.Completion?.TrySetCanceled();

                foreach (var bin in _bins)
                {
                    bin.Reset();
                }

                _initializeTask = new InitializeTask
                {
                    Completion = tcs,
                    StartTime = DateTime.UtcNow,
                    Phase = InitPhase.RaisingToSensor
                };

                foreach (var bin in _bins)
                {
                    if (!ReadBinSensor(bin.BinNumber))
                    {
                        StartBinJogUp(bin);
                        bin.CurrentState = BinElevatorState.Moving;
                    }
                    else
                    {
                        bin.ReachedSensor = true;
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                }

                _uiLogger.InfoRaw("料仓初始化开始");
            }

            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(InitializeTimeoutMs)).ConfigureAwait(false);
                if (completed != tcs.Task)
                {
                    lock (_stateLock)
                    {
                        foreach (var bin in _bins)
                        {
                            StopBinAxis(bin);
                            bin.Reset();
                        }
                        _initializeTask = null;
                    }
                    return BinElevatorStatusMessage.InitializeResult(
                        BinOperationResult.Timeout, "初始化超时");
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return BinElevatorStatusMessage.InitializeResult(
                    BinOperationResult.Error, "初始化被取消");
            }
            catch (Exception ex)
            {
                return BinElevatorStatusMessage.InitializeResult(
                    BinOperationResult.Error, "初始化异常", ex.Message);
            }
        }

        private async Task<BinElevatorStatusMessage> HandleRaiseToSensorAsync(BinElevatorCommandMessage request)
        {
            if (request == null || request.Command != BinCommand.RaiseToSensor)
            {
                return null;
            }

            int binNumber = request.BinNumber;
            if (binNumber < 1 || binNumber > 3)
            {
                return BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber, BinOperationResult.Error, "无效料仓编号");
            }

            if (ReadBinSensor(binNumber))
            {
                return BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber, BinOperationResult.HasMaterial, "已有感应信号");
            }

            var tcs = new TaskCompletionSource<BinElevatorStatusMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_stateLock)
            {
                if (_activeTasks.TryGetValue(binNumber, out var existingTask))
                {
                    existingTask.Completion?.TrySetCanceled();
                }

                var bin = GetBin(binNumber);
                if (bin != null)
                {
                    bin.CurrentState = BinElevatorState.Unknown;
                }

                _activeTasks[binNumber] = new BinTask
                {
                    BinNumber = binNumber,
                    TaskType = BinTaskType.Unloading,
                    StartTime = DateTime.UtcNow,
                    Completion = tcs
                };

                _uiLogger.InfoRaw("料仓{0}开始上升检测物料", binNumber);
            }

            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(RaiseToSensorTimeoutMs))
                    .ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    lock (_stateLock)
                    {
                        var bin = GetBin(binNumber);
                        if (bin != null)
                        {
                            StopBinAxis(bin);
                            bin.CurrentState = BinElevatorState.Stopped;
                        }
                        _activeTasks.Remove(binNumber);
                    }
                    return BinElevatorStatusMessage.MaterialCheckResult(
                        binNumber, BinOperationResult.Timeout, "超时无料");
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber, BinOperationResult.Error, "检测被取消");
            }
            catch (Exception ex)
            {
                return BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber, BinOperationResult.Error, "检测异常", ex.Message);
            }
        }

        #endregion

        #region 任务处理（OnRun 调用）

        private void ProcessInitializeTask()
        {
            switch (_initializeTask.Phase)
            {
                case InitPhase.RaisingToSensor:
                    ProcessInitRaisingPhase();
                    break;
                case InitPhase.LoweringToNoSensor:
                    ProcessInitLoweringPhase();
                    break;
            }
        }

        private void ProcessInitRaisingPhase()
        {
            foreach (var bin in _bins)
            {
                if (bin.ReachedSensor)
                {
                    continue;
                }

                if (bin.CurrentState == BinElevatorState.Moving)
                {
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        bin.ReachedSensor = true;
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                }
            }

            if (_bins.All(b => b.ReachedSensor))
            {
                _initializeTask.Phase = InitPhase.LoweringToNoSensor;
                foreach (var bin in _bins)
                {
                    bin.ReachedSensor = false;
                    StartBinJogDown(bin);
                    bin.CurrentState = BinElevatorState.Moving;
                }
            }
        }

        private void ProcessInitLoweringPhase()
        {
            foreach (var bin in _bins)
            {
                if (bin.ReachedSensor)
                {
                    continue;
                }

                if (bin.CurrentState == BinElevatorState.Moving)
                {
                    if (!ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        bin.ReachedSensor = true;
                        bin.CurrentState = BinElevatorState.Stopped;
                    }
                }
            }

            if (_bins.All(b => b.ReachedSensor))
            {
                var tcs = _initializeTask.Completion;
                _initializeTask = null;

                foreach (var bin in _bins)
                {
                    bin.Reset();
                }

                tcs?.TrySetResult(BinElevatorStatusMessage.InitializeResult(
                    BinOperationResult.Success, "初始化完成"));

                _uiLogger.InfoRaw("所有料仓初始化完成");
            }
        }

        private void ProcessBinTask(BinTask task)
        {
            var bin = GetBin(task.BinNumber);
            if (bin == null)
            {
                return;
            }

            switch (task.TaskType)
            {
                case BinTaskType.Loading:
                    ProcessLoadingTask(bin, task);
                    break;
                case BinTaskType.Unloading:
                    ProcessUnloadingTask(bin, task);
                    break;
            }
        }

        private void ProcessLoadingTask(BinState bin, BinTask task)
        {
            switch (bin.CurrentState)
            {
                case BinElevatorState.Unknown:
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        StartBinJogDown(bin);
                        bin.CurrentState = BinElevatorState.Moving;
                    }
                    else
                    {
                        CompleteTask(bin.BinNumber, null);
                    }
                    break;

                case BinElevatorState.Moving:
                    if (!ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        CompleteTask(bin.BinNumber, null);
                        _uiLogger.InfoRaw("料仓{0}下降完成", bin.BinNumber);
                    }
                    break;
            }
        }

        private void ProcessUnloadingTask(BinState bin, BinTask task)
        {
            switch (bin.CurrentState)
            {
                case BinElevatorState.Unknown:
                    StartBinJogUp(bin);
                    bin.CurrentState = BinElevatorState.Moving;
                    break;

                case BinElevatorState.Moving:
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        var result = BinElevatorStatusMessage.MaterialCheckResult(
                            bin.BinNumber, BinOperationResult.HasMaterial, "检测到物料");
                        CompleteTask(bin.BinNumber, result);
                        _uiLogger.InfoRaw("料仓{0}上升完成，检测到物料", bin.BinNumber);
                    }
                    break;
            }
        }

        private void CompleteTask(int binNumber, BinElevatorStatusMessage result)
        {
            if (_activeTasks.TryGetValue(binNumber, out var task))
            {
                _activeTasks.Remove(binNumber);
                var bin = GetBin(binNumber);
                if (bin != null)
                {
                    bin.CurrentState = BinElevatorState.Stopped;
                }
                task.Completion?.TrySetResult(result);
            }
        }

        #endregion

        #region 辅助方法

        private BinState GetBin(int binNumber)
        {
            if (binNumber < 1 || binNumber > 3)
            {
                return null;
            }
            return _bins[binNumber - 1];
        }

        private bool ReadBinSensor(int binNumber)
        {
            try
            {
                if (_ioManager?.Ctx == null)
                {
                    return false;
                }

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
            catch
            {
                return false;
            }
        }

        private void StartBinJogUp(BinState bin)
        {
            try
            {
                var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
                if (axisConfig != null)
                {
                    _axisManager.JogUp(axisConfig);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "料仓Jog上升", ex.Message);
            }
        }

        private void StartBinJogDown(BinState bin)
        {
            try
            {
                var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
                if (axisConfig != null)
                {
                    _axisManager.JogDown(axisConfig);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "料仓Jog下降", ex.Message);
            }
        }

        private void StopBinAxis(BinState bin)
        {
            try
            {
                var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
                if (axisConfig != null)
                {
                    _axisManager.JogStop(axisConfig);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "料仓停止", ex.Message);
            }
        }

        #endregion

        #region 内部类型

        private enum BinTaskType { Loading, Unloading }

        private enum InitPhase { RaisingToSensor, LoweringToNoSensor }

        private class BinTask
        {
            public int BinNumber { get; set; }
            public BinTaskType TaskType { get; set; }
            public DateTime StartTime { get; set; }
            public TaskCompletionSource<BinElevatorStatusMessage> Completion { get; set; }
        }

        private class InitializeTask
        {
            public InitPhase Phase { get; set; }
            public DateTime StartTime { get; set; }
            public TaskCompletionSource<BinElevatorStatusMessage> Completion { get; set; }
        }

        #endregion
    }
}
