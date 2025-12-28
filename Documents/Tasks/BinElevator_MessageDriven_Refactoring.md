# BinElevator 消息驱动重构方案

> **目标**：将 BinElevatorModule 改为消息驱动模式，继承 BaseModule，参考 MesModule 实现。

---

## 1. 实现模式（参考 MesModule）

```csharp
/// <summary>
/// 料仓升降模块（消息驱动模式）：
/// - OnInit：注册 MessageHub 消息处理器
/// - OnRun：仅保持模块存活 + 处理活跃任务
/// - OnDestroy：清理订阅
/// </summary>
public class BinElevatorModule : BaseModule<BinElevatorModule>, IBinElevator
{
    protected override void OnInit()
    {
        // 注册消息处理器
    }

    protected override bool OnRun()
    {
        // 处理活跃任务，保持模块存活
        Thread.Sleep(1);
        return true;
    }

    protected override void OnDestroy()
    {
        // 清理订阅
    }
}
```

---

## 2. 四个操作点

| 操作 | 消息命令 | 需要轴号 | 需要回复 | 说明 |
|------|---------|---------|---------|------|
| **初始化** | `Initialize` | ❌ | ✅ | 所有料仓初始化 |
| **卸料上升** | `RaiseToSensor` | ✅ | ✅ | 指定料仓上升到感应位置 |
| **装料下降** | `LoadingCompleted` | ✅ | ❌ | 指定料仓下降一格 |
| **全部停止** | `ForceStopAll` | ❌ | ❌ | 立即停止所有料仓 |

---

## 3. 消息定义

### 3.1 命令枚举

```csharp
public enum BinCommand
{
    /// <summary>
    /// 初始化所有料仓（需要回复）
    /// </summary>
    Initialize,

    /// <summary>
    /// 上升到感应位置（需要轴号，需要回复）
    /// </summary>
    RaiseToSensor,

    /// <summary>
    /// 装料完成，下降一格（需要轴号，不需要回复）
    /// </summary>
    LoadingCompleted,

    /// <summary>
    /// 强制停止所有料仓（不需要回复）
    /// </summary>
    ForceStopAll
}
```

### 3.2 命令消息

```csharp
public class BinElevatorCommandMessage : IMessage, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// 料仓编号 (1-3)，仅 RaiseToSensor/LoadingCompleted 需要
    /// </summary>
    public int BinNumber { get; set; }

    public BinCommand Command { get; set; }
    public string Source { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // ============ 工厂方法 ============

    /// <summary>
    /// 初始化所有料仓
    /// </summary>
    public static BinElevatorCommandMessage Initialize(string source)
        => new BinElevatorCommandMessage(0, BinCommand.Initialize, source);

    /// <summary>
    /// 卸料：指定料仓上升到感应位置
    /// </summary>
    public static BinElevatorCommandMessage RaiseToSensor(int binNumber, string source)
        => new BinElevatorCommandMessage(binNumber, BinCommand.RaiseToSensor, source);

    /// <summary>
    /// 装料：指定料仓下降一格
    /// </summary>
    public static BinElevatorCommandMessage LoadingCompleted(int binNumber, string source)
        => new BinElevatorCommandMessage(binNumber, BinCommand.LoadingCompleted, source);

    /// <summary>
    /// 强制停止所有料仓
    /// </summary>
    public static BinElevatorCommandMessage ForceStopAll(string source)
        => new BinElevatorCommandMessage(0, BinCommand.ForceStopAll, source);
}
```

### 3.3 状态回复消息

```csharp
public class BinElevatorStatusMessage : IMessage, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// 料仓编号 (0=全部, 1-3=指定)
    /// </summary>
    public int BinNumber { get; set; }

    public BinCommand CurrentCommand { get; set; }
    public BinOperationResult OperationResult { get; set; }
    public bool HasMaterial { get; set; }
    public string Description { get; set; }
    public string ErrorMessage { get; set; }

    // ============ 工厂方法 ============

    /// <summary>
    /// 初始化完成结果
    /// </summary>
    public static BinElevatorStatusMessage InitializeResult(BinOperationResult result, string description = "", string errorMessage = "")
        => new BinElevatorStatusMessage
        {
            BinNumber = 0,
            CurrentCommand = BinCommand.Initialize,
            OperationResult = result,
            Description = description,
            ErrorMessage = errorMessage
        };

    /// <summary>
    /// 物料检测结果
    /// </summary>
    public static BinElevatorStatusMessage MaterialCheckResult(int binNumber, BinOperationResult result, string description = "", string errorMessage = "")
        => new BinElevatorStatusMessage
        {
            BinNumber = binNumber,
            CurrentCommand = BinCommand.RaiseToSensor,
            OperationResult = result,
            HasMaterial = result == BinOperationResult.HasMaterial,
            Description = description,
            ErrorMessage = errorMessage
        };
}
```

---

## 4. BinElevatorModule 完整实现（参考 MesModule）

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ewan.Core.Axis;
using Ewan.Core.IO;
using Ewan.Model.Production;
using EwanCore.Messaging;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降模块（消息驱动模式）：
    /// - OnInit：注册 MessageHub 消息处理器
    /// - OnRun：处理活跃任务，保持模块存活
    /// - OnDestroy：清理订阅
    /// </summary>
    public class BinElevatorModule : BaseModule<BinElevatorModule>, IBinElevator
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

            // ============ Fire & Forget（不需要回复） ============

            _forceStopSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(
                OnForceStopAll,
                msg => msg.Command == BinCommand.ForceStopAll);

            _loadingCompletedSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(
                OnLoadingCompleted,
                msg => msg.Command == BinCommand.LoadingCompleted);

            // ============ Request/Reply（需要回复） ============

            _initializeResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                HandleInitializeAsync,
                msg => msg.Command == BinCommand.Initialize,
                postReply: true);

            _raiseToSensorResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
                HandleRaiseToSensorAsync,
                msg => msg.Command == BinCommand.RaiseToSensor,
                postReply: true);

            _uiLogger.InfoRaw("模块初始化成功: {0}", "BinElevatorModule (MessageHub)");
        }

        protected override bool OnRun()
        {
            // 消息驱动模式：消息回调触发任务，OnRun 处理活跃任务
            try
            {
                lock (_stateLock)
                {
                    // 处理初始化任务
                    if (_initializeTask != null)
                    {
                        ProcessInitializeTask();
                    }

                    // 处理各料仓任务
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
                // 停止所有轴
                lock (_stateLock)
                {
                    foreach (var bin in _bins)
                    {
                        StopBinAxis(bin);
                    }
                    _activeTasks.Clear();
                    _initializeTask?.Completion?.TrySetCanceled();
                    _initializeTask = null;
                }
            }
            catch { }

            // 清理订阅
            _forceStopSubscription?.Dispose();
            _loadingCompletedSubscription?.Dispose();
            _initializeResponder?.Dispose();
            _raiseToSensorResponder?.Dispose();

            _uiLogger.InfoRaw("模块已销毁: {0}", "BinElevatorModule");
        }

        #endregion

        #region 消息处理器

        /// <summary>
        /// 全部停止（Fire & Forget）
        /// </summary>
        private void OnForceStopAll(BinElevatorCommandMessage message)
        {
            lock (_stateLock)
            {
                foreach (var bin in _bins)
                {
                    StopBinAxis(bin);
                    bin.Reset();
                }

                // 取消所有等待中的任务
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

        /// <summary>
        /// 装料下降（Fire & Forget，需要轴号）
        /// </summary>
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
                var bin = GetBin(binNumber);
                bin.CurrentState = BinElevatorState.Unknown;

                _activeTasks[binNumber] = new BinTask
                {
                    BinNumber = binNumber,
                    TaskType = BinTaskType.Loading,
                    StartTime = DateTime.UtcNow
                };

                _uiLogger.InfoRaw("料仓{0}开始下降", binNumber);
            }
        }

        /// <summary>
        /// 初始化（Request/Reply）
        /// </summary>
        private async Task<BinElevatorStatusMessage> HandleInitializeAsync(BinElevatorCommandMessage request)
        {
            var tcs = new TaskCompletionSource<BinElevatorStatusMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_stateLock)
            {
                // 取消正在进行的初始化
                _initializeTask?.Completion?.TrySetCanceled();

                // 重置所有料仓
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

                // 所有料仓开始上升
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
                    }
                }

                _uiLogger.InfoRaw("料仓初始化开始");
            }

            // 等待完成或超时
            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(InitializeTimeoutMs))
                    .ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    lock (_stateLock)
                    {
                        foreach (var bin in _bins)
                        {
                            StopBinAxis(bin);
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

        /// <summary>
        /// 卸料上升（Request/Reply，需要轴号）
        /// </summary>
        private async Task<BinElevatorStatusMessage> HandleRaiseToSensorAsync(BinElevatorCommandMessage request)
        {
            int binNumber = request.BinNumber;

            if (binNumber < 1 || binNumber > 3)
            {
                return BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber, BinOperationResult.Error, "无效料仓编号");
            }

            // 已经有感应信号，直接返回有料
            if (ReadBinSensor(binNumber))
            {
                return BinElevatorStatusMessage.MaterialCheckResult(
                    binNumber, BinOperationResult.HasMaterial, "已有感应信号");
            }

            var tcs = new TaskCompletionSource<BinElevatorStatusMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_stateLock)
            {
                // 取消该料仓的现有任务
                if (_activeTasks.TryGetValue(binNumber, out var existingTask))
                {
                    existingTask.Completion?.TrySetCanceled();
                }

                var bin = GetBin(binNumber);
                bin.CurrentState = BinElevatorState.Unknown;

                _activeTasks[binNumber] = new BinTask
                {
                    BinNumber = binNumber,
                    TaskType = BinTaskType.Unloading,
                    StartTime = DateTime.UtcNow,
                    Completion = tcs
                };

                _uiLogger.InfoRaw("料仓{0}开始上升检测物料", binNumber);
            }

            // 等待完成或超时
            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(RaiseToSensorTimeoutMs))
                    .ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    lock (_stateLock)
                    {
                        StopBinAxis(GetBin(binNumber));
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
                if (bin.ReachedSensor) continue;

                if (bin.CurrentState == BinElevatorState.Moving)
                {
                    if (ReadBinSensor(bin.BinNumber))
                    {
                        StopBinAxis(bin);
                        bin.ReachedSensor = true;
                    }
                }
            }

            // 检查是否全部到达感应位置
            if (_bins.All(b => b.ReachedSensor))
            {
                // 进入第二阶段：下降
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
                if (bin.ReachedSensor) continue;

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

            // 检查是否全部完成
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
                GetBin(binNumber).CurrentState = BinElevatorState.Stopped;
                task.Completion?.TrySetResult(result);
            }
        }

        #endregion

        #region 辅助方法

        private BinState GetBin(int binNumber)
        {
            if (binNumber < 1 || binNumber > 3) return null;
            return _bins[binNumber - 1];
        }

        private bool ReadBinSensor(int binNumber)
        {
            try
            {
                switch (binNumber)
                {
                    case 1: return _ioManager.Ctx.R.料仓1有料感应;
                    case 2: return _ioManager.Ctx.R.料仓2有料感应;
                    case 3: return _ioManager.Ctx.R.料仓3有料感应;
                    default: return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private void StartBinJogUp(BinState bin)
        {
            var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
            if (axisConfig != null)
            {
                _axisManager.JogUp(axisConfig);
            }
        }

        private void StartBinJogDown(BinState bin)
        {
            var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
            if (axisConfig != null)
            {
                _axisManager.JogDown(axisConfig);
            }
        }

        private void StopBinAxis(BinState bin)
        {
            var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
            if (axisConfig != null)
            {
                _axisManager.JogStop(axisConfig);
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
```

---

## 5. 调用方示例

### 5.1 HomeLogic（初始化）

```csharp
case "初始化料仓":
    var initRequest = BinElevatorCommandMessage.Initialize(nameof(HomeLogic));
    try
    {
        var result = await MessageHub.Current.RequestAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
            initRequest,
            timeoutMs: 30000);

        if (result.OperationResult == BinOperationResult.Success)
        {
            SwitchIndex = "初始化完成";
        }
        else
        {
            _uiLogger.ErrorRaw("料仓初始化失败: {0}", result.ErrorMessage);
        }
    }
    catch (TimeoutException)
    {
        _uiLogger.ErrorRaw("料仓初始化超时");
    }
    break;
```

### 5.2 MaterialUnloadingLogic（卸料上升，指定轴）

```csharp
case "检查料仓有料":
    int binNumber = GetConfiguredBinNumber(); // 1, 2, 或 3
    var request = BinElevatorCommandMessage.RaiseToSensor(binNumber, nameof(MaterialUnloadingLogic));

    try
    {
        var result = await MessageHub.Current.RequestAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
            request,
            timeoutMs: 10000);

        if (result.OperationResult == BinOperationResult.HasMaterial)
        {
            SwitchIndex = "发送取料指令";
        }
        else
        {
            SwitchIndex = "释放空车";
        }
    }
    catch (TimeoutException)
    {
        SwitchIndex = "释放空车";
    }
    break;
```

### 5.3 MaterialLoadingLogic（装料下降，指定轴）

```csharp
case "等待装载完成":
    if (_ioManager?.Ctx?.Edge.F(x => x.机械臂放置完成信号) == true)
    {
        int binNumber = GetConfiguredBinNumber(); // 1, 2, 或 3

        // Fire & Forget，不等待
        MessageHub.Current.Post(
            BinElevatorCommandMessage.LoadingCompleted(binNumber, nameof(MaterialLoadingLogic)));

        SwitchIndex = "清理状态";
    }
    break;
```

### 5.4 LogicManager（全部停止）

```csharp
private void StopInternal(bool publishSystemControl)
{
    // Fire & Forget，不等待
    MessageHub.Current.Post(
        BinElevatorCommandMessage.ForceStopAll(nameof(LogicManager)));

    // ... 其他停止逻辑 ...
}
```

---

## 6. 流程图

### 6.1 消息处理总览

```
┌─────────────────────────────────────────────────────────────────┐
│                        MessageHub                                │
│                            │                                     │
│    ┌───────────┬───────────┼───────────┬───────────┐            │
│    ↓           ↓           ↓           ↓           │            │
│ Initialize  RaiseToSensor  LoadingCompleted  ForceStopAll       │
│ (bin=0)     (bin=1/2/3)    (bin=1/2/3)      (bin=0)            │
│    │           │           │           │                        │
│    ↓           ↓           ↓           ↓                        │
│ Request/   Request/     Subscribe    Subscribe                  │
│ Reply      Reply        (无回复)     (无回复)                    │
│    │           │           │           │                        │
│    └───────────┴───────────┴───────────┘                        │
│                            ↓                                     │
│                  BinElevatorModule                               │
│                  (后台 WorkerLoop)                               │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 初始化流程

```
HomeLogic              MessageHub           BinElevatorModule
    │                      │                        │
    │ RequestAsync         │                        │
    │ (Initialize)         │                        │
    │─────────────────────►│                        │
    │                      │ HandleInitializeAsync  │
    │                      │───────────────────────►│
    │                      │                        │ JogUp(all)
    │                      │                        │ ──────► Axis
    │                      │                        │ (等待全部感应)
    │                      │                        │ JogDown(all)
    │                      │                        │ ──────► Axis
    │                      │                        │ (等待全部无感应)
    │                      │                        │ Stop(all)
    │                      │  StatusMessage         │
    │                      │  (Success)             │
    │                      │◄───────────────────────│
    │ Result               │                        │
    │◄─────────────────────│                        │
```

### 6.3 卸料上升（指定轴）

```
UnloadingLogic         MessageHub           BinElevatorModule
    │                      │                        │
    │ RequestAsync         │                        │
    │ (RaiseToSensor,      │                        │
    │  binNumber=2)        │                        │
    │─────────────────────►│                        │
    │                      │ HandleRaiseToSensorAsync
    │                      │───────────────────────►│
    │                      │                        │ JogUp(bin=2)
    │                      │                        │ ──────► Axis
    │                      │                        │ (等待感应)
    │                      │                        │ Stop(bin=2)
    │                      │  StatusMessage         │
    │                      │  (HasMaterial, bin=2)  │
    │                      │◄───────────────────────│
    │ Result               │                        │
    │◄─────────────────────│                        │
```

### 6.4 装料下降（指定轴）

```
LoadingLogic           MessageHub           BinElevatorModule
    │                      │                        │
    │ Post                 │                        │
    │ (LoadingCompleted,   │                        │
    │  binNumber=1)        │                        │
    │─────────────────────►│                        │
    │ (继续执行，不等待)    │ OnLoadingCompleted     │
    │                      │───────────────────────►│
    │                      │                        │ JogDown(bin=1)
    │                      │                        │ ──────► Axis
    │                      │                        │ (等待无感应)
    │                      │                        │ Stop(bin=1)
    │                      │                        │
```

---

## 7. 实施步骤

### Phase 1: 消息定义调整
- [ ] 确保 `BinCommand` 包含四个命令
- [ ] 添加 `BinElevatorStatusMessage.InitializeResult()` 工厂方法

### Phase 2: 模块重构
- [ ] 添加 `_activeTasks` 和 `_initializeTask` 数据结构
- [ ] 实现 `OnForceStopAll` 处理器
- [ ] 实现 `OnLoadingCompleted` 处理器
- [ ] 实现 `HandleInitializeAsync` 处理器
- [ ] 实现 `HandleRaiseToSensorAsync` 处理器
- [ ] 重写 `WorkerLoop` 基于任务处理

### Phase 3: 调用方适配
- [ ] `HomeLogic`：使用 `RequestAsync(Initialize)`
- [ ] `MaterialLoadingLogic`：添加 `binNumber` 参数
- [ ] `MaterialUnloadingLogic`：使用 `RequestAsync(RaiseToSensor, binNumber)`
- [ ] `LogicManager`：使用 `Post(ForceStopAll)`

### Phase 4: 清理
- [ ] 移除 `BinElevatorMode` 枚举
- [ ] 移除 LogicManager 中的 `_binElevatorThread` 轮询线程
- [ ] 更新 `IBinElevator` 接口

---

## 8. 对比总结

| 操作 | 之前 | 之后 |
|------|------|------|
| **初始化** | `PerformHardwareInitialization()` | `RequestAsync(Initialize)` |
| **卸料上升** | `RaiseToSensorAsync(bin)` | `RequestAsync(RaiseToSensor, bin)` |
| **装料下降** | `Post(LoadingCompleted)` 无轴号 | `Post(LoadingCompleted, bin)` |
| **全部停止** | `ForceStopAllBins()` | `Post(ForceStopAll)` |
| **线程管理** | LogicManager 管理 | 模块自管理 |
