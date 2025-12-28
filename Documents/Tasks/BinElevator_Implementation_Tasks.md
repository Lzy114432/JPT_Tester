# BinElevator 消息驱动重构 - 任务分解

> **Epic**: 将 BinElevatorModule 改为纯消息驱动模式
> **参考文档**: `Documents/Tasks/BinElevator_MessageDriven_Refactoring.md`
> **预计总工时**: 12-16 小时

---

## 任务总览

```
Epic: BinElevator 消息驱动重构
├── Phase 1: 消息定义调整 (2-3h)
│   ├── Task 1.1: 清理 BinCommand 枚举
│   ├── Task 1.2: 修改 LoadingCompleted 工厂方法
│   └── Task 1.3: 添加 InitializeResult 工厂方法
├── Phase 2: 模块重构 (6-8h) [核心]
│   ├── Task 2.1: 添加任务数据结构
│   ├── Task 2.2: 实现 Fire & Forget 处理器
│   ├── Task 2.3: 实现 Request/Reply 处理器
│   ├── Task 2.4: 重写 OnRun 任务处理循环
│   └── Task 2.5: 更新 OnInit/OnDestroy
├── Phase 3: 调用方适配 (3-4h)
│   ├── Task 3.1: 适配 HomeLogic
│   ├── Task 3.2: 适配 MaterialLoadingLogic
│   ├── Task 3.3: 适配 MaterialUnloadingLogic
│   └── Task 3.4: 适配 LogicManager
└── Phase 4: 清理 (1-2h)
    ├── Task 4.1: 移除 BinElevatorMode
    ├── Task 4.2: 清理未使用代码
    └── Task 4.3: 验证编译和测试
```

---

## Phase 1: 消息定义调整

### Task 1.1: 清理 BinCommand 枚举

**文件**: `Ewan.Model/Production/BinElevatorCommand.cs`

**描述**: 移除不再需要的旧命令，保留方案定义的4个核心命令。

**验收标准**:
- [ ] 移除 `Stop` 命令
- [ ] 移除 `FeedPosition` 命令
- [ ] 移除 `DownPosition` 命令
- [ ] 移除 `UnloadingCompleted` 命令
- [ ] 保留: `Initialize`, `RaiseToSensor`, `LoadingCompleted`, `ForceStopAll`
- [ ] 编译通过，无引用错误

**当前代码**:
```csharp
public enum BinCommand
{
    Stop,              // ❌ 移除
    FeedPosition,      // ❌ 移除
    DownPosition,      // ❌ 移除
    Initialize,        // ✅ 保留
    ForceStopAll,      // ✅ 保留
    RaiseToSensor,     // ✅ 保留
    LoadingCompleted,  // ✅ 保留
    UnloadingCompleted // ❌ 移除
}
```

**目标代码**:
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

**预计工时**: 0.5h

---

### Task 1.2: 修改 LoadingCompleted 工厂方法

**文件**: `Ewan.Model/Production/BinElevatorCommand.cs`

**描述**: 修改 `LoadingCompleted` 工厂方法，添加 `binNumber` 参数支持指定料仓。

**验收标准**:
- [ ] `LoadingCompleted(int binNumber, string source)` 方法签名
- [ ] 旧方法标记为 `[Obsolete]` 或直接替换
- [ ] 所有调用点已更新

**当前代码**:
```csharp
public static BinElevatorCommandMessage LoadingCompleted(string source)
    => new BinElevatorCommandMessage(0, BinCommand.LoadingCompleted, string.Empty, source);
```

**目标代码**:
```csharp
/// <summary>
/// 装料完成，指定料仓下降一格
/// </summary>
/// <param name="binNumber">料仓编号 (1-3)</param>
/// <param name="source">消息来源</param>
public static BinElevatorCommandMessage LoadingCompleted(int binNumber, string source)
    => new BinElevatorCommandMessage(binNumber, BinCommand.LoadingCompleted, string.Empty, source);
```

**预计工时**: 0.5h

---

### Task 1.3: 添加 InitializeResult 工厂方法

**文件**: `Ewan.Model/Production/BinElevatorCommand.cs`

**描述**: 为 `BinElevatorStatusMessage` 添加 `InitializeResult` 工厂方法。

**验收标准**:
- [ ] `InitializeResult(BinOperationResult result, string description, string errorMessage)` 方法
- [ ] 返回 `BinNumber = 0` 表示全部料仓
- [ ] 单元测试验证

**目标代码**:
```csharp
/// <summary>
/// 初始化完成结果
/// </summary>
public static BinElevatorStatusMessage InitializeResult(
    BinOperationResult result,
    string description = "",
    string errorMessage = "")
{
    var state = result == BinOperationResult.Error
        ? BinExecuteState.Error
        : BinExecuteState.Completed;

    return new BinElevatorStatusMessage(0, state, BinCommand.Initialize, string.Empty, description)
    {
        OperationResult = result,
        ErrorMessage = errorMessage
    };
}
```

**预计工时**: 1h

---

## Phase 2: 模块重构 [核心]

### Task 2.1: 添加任务数据结构

**文件**: `Ewan.Core/Module/BinElevatorModule.cs`

**描述**: 添加任务管理所需的内部类型和数据结构。

**验收标准**:
- [ ] 添加 `BinTaskType` 枚举 (Loading, Unloading)
- [ ] 添加 `InitPhase` 枚举 (RaisingToSensor, LoweringToNoSensor)
- [ ] 添加 `BinTask` 类
- [ ] 添加 `InitializeTask` 类
- [ ] 添加 `_activeTasks` 字典
- [ ] 添加 `_initializeTask` 字段

**目标代码**:
```csharp
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

#region 私有字段

// 活跃任务
private readonly Dictionary<int, BinTask> _activeTasks = new Dictionary<int, BinTask>();
private InitializeTask _initializeTask;

#endregion
```

**预计工时**: 1h

---

### Task 2.2: 实现 Fire & Forget 处理器

**文件**: `Ewan.Core/Module/BinElevatorModule.cs`

**描述**: 实现 `OnForceStopAll` 和 `OnLoadingCompleted` 消息处理器（无需回复）。

**验收标准**:
- [ ] `OnForceStopAll`: 停止所有轴，清空任务队列
- [ ] `OnLoadingCompleted`: 创建 Loading 任务，触发下降
- [ ] 在 `OnInit` 中注册订阅
- [ ] 在 `OnDestroy` 中清理订阅

**目标代码**:
```csharp
// OnInit 中注册
_forceStopSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(
    OnForceStopAll,
    msg => msg.Command == BinCommand.ForceStopAll);

_loadingCompletedSubscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(
    OnLoadingCompleted,
    msg => msg.Command == BinCommand.LoadingCompleted);

// 处理器实现
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
```

**预计工时**: 2h

---

### Task 2.3: 实现 Request/Reply 处理器

**文件**: `Ewan.Core/Module/BinElevatorModule.cs`

**描述**: 实现 `HandleInitializeAsync` 和 `HandleRaiseToSensorAsync` 请求处理器（需要回复）。

**验收标准**:
- [ ] `HandleInitializeAsync`: 异步初始化所有料仓，返回结果
- [ ] `HandleRaiseToSensorAsync`: 异步上升检测物料，返回结果
- [ ] 超时处理（Initialize: 30s, RaiseToSensor: 10s）
- [ ] 取消处理
- [ ] 使用 `RespondAsync` 注册

**关键实现**:
```csharp
// OnInit 中注册
_initializeResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
    HandleInitializeAsync,
    msg => msg.Command == BinCommand.Initialize,
    postReply: true);

_raiseToSensorResponder = MessageHub.Current.RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>(
    HandleRaiseToSensorAsync,
    msg => msg.Command == BinCommand.RaiseToSensor,
    postReply: true);
```

**预计工时**: 2h

---

### Task 2.4: 重写 OnRun 任务处理循环

**文件**: `Ewan.Core/Module/BinElevatorModule.cs`

**描述**: 重写 `OnRun` 方法，从状态机模式改为任务处理模式。

**验收标准**:
- [ ] 移除 `BinElevatorMode` 状态机逻辑
- [ ] 处理 `_initializeTask`（两阶段：上升→下降）
- [ ] 处理 `_activeTasks`（Loading/Unloading）
- [ ] 保持 1ms 循环间隔

**目标代码**:
```csharp
protected override bool OnRun()
{
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

    Thread.Sleep(1);
    return true;
}
```

**预计工时**: 2h

---

### Task 2.5: 更新 OnInit/OnDestroy

**文件**: `Ewan.Core/Module/BinElevatorModule.cs`

**描述**: 更新生命周期方法，移除旧订阅，添加新订阅。

**验收标准**:
- [ ] `OnInit`: 注册4个消息处理器
- [ ] `OnDestroy`: 清理4个订阅 + 停止所有轴 + 取消所有任务
- [ ] 移除 `_systemStatusSubscription`（如不再需要）

**预计工时**: 1h

---

## Phase 3: 调用方适配

### Task 3.1: 适配 HomeLogic

**文件**: `Ewan.Core/Logic/HomeLogic.cs`

**描述**: 更新初始化料仓的调用方式。

**验收标准**:
- [ ] 使用 `MessageHub.Current.RequestAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>`
- [ ] 处理 `BinOperationResult.Success` / `Error` / `Timeout`
- [ ] 正确设置超时（30s）

**目标代码**:
```csharp
case "初始化料仓":
    var initRequest = BinElevatorCommandMessage.InitializeAll(nameof(HomeLogic));
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

**预计工时**: 1h

---

### Task 3.2: 适配 MaterialLoadingLogic

**文件**: `Ewan.Core/Logic/MaterialLoadingLogic.cs`

**描述**: 更新装料完成的调用方式，添加 `binNumber` 参数。

**验收标准**:
- [ ] 使用 `MessageHub.Current.Post(BinElevatorCommandMessage.LoadingCompleted(binNumber, source))`
- [ ] 正确获取当前操作的料仓编号
- [ ] Fire & Forget，不等待回复

**目标代码**:
```csharp
case "等待装载完成":
    if (_ioManager?.Ctx?.Edge.F(x => x.机械臂放置完成信号) == true)
    {
        int binNumber = GetConfiguredBinNumber(); // 获取当前料仓编号

        // Fire & Forget
        MessageHub.Current.Post(
            BinElevatorCommandMessage.LoadingCompleted(binNumber, nameof(MaterialLoadingLogic)));

        SwitchIndex = "清理状态";
    }
    break;
```

**预计工时**: 1h

---

### Task 3.3: 适配 MaterialUnloadingLogic

**文件**: `Ewan.Core/Logic/MaterialUnloadingLogic.cs`

**描述**: 更新卸料物料检测的调用方式。

**验收标准**:
- [ ] 使用 `MessageHub.Current.RequestAsync` 替代直接调用
- [ ] 处理 `BinOperationResult.HasMaterial` / `NoMaterial` / `Timeout`
- [ ] 正确设置超时（10s）

**目标代码**:
```csharp
case "检查料仓有料":
    int binNumber = GetConfiguredBinNumber();
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

**预计工时**: 1h

---

### Task 3.4: 适配 LogicManager

**文件**: `Ewan.Core/Manager/LogicManager.cs`

**描述**: 更新停止所有料仓的调用方式。

**验收标准**:
- [ ] 使用 `MessageHub.Current.Post(BinElevatorCommandMessage.ForceStopAll(source))`
- [ ] 移除 `_binElevatorThread` 轮询线程（如存在）
- [ ] Fire & Forget

**目标代码**:
```csharp
private void StopInternal(bool publishSystemControl)
{
    // Fire & Forget
    MessageHub.Current.Post(
        BinElevatorCommandMessage.ForceStopAll(nameof(LogicManager)));

    // ... 其他停止逻辑 ...
}
```

**预计工时**: 0.5h

---

## Phase 4: 清理

### Task 4.1: 移除 BinElevatorMode

**文件**: `Ewan.Model/Production/BinElevatorCommand.cs` 或相关文件

**描述**: 移除不再使用的 `BinElevatorMode` 枚举。

**验收标准**:
- [ ] 删除 `BinElevatorMode` 枚举定义
- [ ] 删除 `BinElevatorModule` 中的 `_binElevatorMode` 字段
- [ ] 删除相关的状态机处理方法
- [ ] 编译通过

**预计工时**: 0.5h

---

### Task 4.2: 清理未使用代码

**文件**: 多个文件

**描述**: 清理重构后不再需要的代码。

**清理清单**:
- [ ] `BinElevatorModule`:
  - [ ] `_activeUnloadingBin` 字段
  - [ ] `_materialDetectionInProgress` 及相关字段
  - [ ] `ProcessInitMode`, `ProcessLoadingMode`, `ProcessUnloadingMode` 方法
  - [ ] `ResetSelectedBinStates` 方法
  - [ ] `_sharedState` 依赖（如不再需要）
- [ ] `IBinElevator`:
  - [ ] 更新接口定义（如需要）
- [ ] `Ewan.Model/Messages/BinElevatorCommandMessage.cs`:
  - [ ] 删除旧的消息定义文件（与 Production 目录重复）
- [ ] `Ewan.Model/Messages/BinElevatorStatusMessage.cs`:
  - [ ] 删除旧的消息定义文件（与 Production 目录重复）

**预计工时**: 0.5h

---

### Task 4.3: 验证编译和测试

**描述**: 完整验证重构结果。

**验收标准**:
- [ ] 解决方案编译通过（0 错误）
- [ ] 现有单元测试通过
- [ ] 手动测试：
  - [ ] 初始化所有料仓
  - [ ] 料仓1/2/3 分别上升检测物料
  - [ ] 料仓1/2/3 分别下降一格
  - [ ] 强制停止所有料仓
- [ ] 日志输出正确

**预计工时**: 1h

---

## 依赖关系图

```
Phase 1 ──────────────────────────────────────────────────┐
  │                                                        │
  ├─ Task 1.1 (清理枚举) ─────────────────┐                │
  │                                        │                │
  ├─ Task 1.2 (LoadingCompleted) ─────────┼───┐            │
  │                                        │   │            │
  └─ Task 1.3 (InitializeResult) ─────────┼───┼────────────┘
                                           │   │
                                           ▼   ▼
Phase 2 ──────────────────────────────────────────────────┐
  │                                                        │
  ├─ Task 2.1 (数据结构) ─────────────────┐                │
  │                                        │                │
  ├─ Task 2.2 (Fire&Forget) ──────────────┼────┐           │
  │         depends on 2.1                 │    │           │
  │                                        │    │           │
  ├─ Task 2.3 (Request/Reply) ────────────┼────┼───┐       │
  │         depends on 2.1, 1.3           │    │   │       │
  │                                        │    │   │       │
  ├─ Task 2.4 (OnRun) ────────────────────┼────┼───┼───┐   │
  │         depends on 2.1, 2.2, 2.3      │    │   │   │   │
  │                                        │    │   │   │   │
  └─ Task 2.5 (Init/Destroy) ─────────────┼────┼───┼───┼───┘
              depends on 2.2, 2.3         │    │   │   │
                                           ▼    ▼   ▼   ▼
Phase 3 ──────────────────────────────────────────────────┐
  │                                                        │
  ├─ Task 3.1 (HomeLogic) ────────────────┐                │
  │         depends on 2.3, 1.3           │                │
  │                                        │                │
  ├─ Task 3.2 (MaterialLoadingLogic) ─────┼────┐           │
  │         depends on 2.2, 1.2           │    │           │
  │                                        │    │           │
  ├─ Task 3.3 (MaterialUnloadingLogic) ───┼────┼───┐       │
  │         depends on 2.3                │    │   │       │
  │                                        │    │   │       │
  └─ Task 3.4 (LogicManager) ─────────────┼────┼───┼───────┘
              depends on 2.2              │    │   │
                                           ▼    ▼   ▼
Phase 4 ──────────────────────────────────────────────────┐
  │                                                        │
  ├─ Task 4.1 (移除BinElevatorMode) ──────┐                │
  │         depends on Phase 2 完成       │                │
  │                                        │                │
  ├─ Task 4.2 (清理代码) ─────────────────┼────┐           │
  │         depends on Phase 3 完成       │    │           │
  │                                        │    │           │
  └─ Task 4.3 (验证) ─────────────────────┴────┴───────────┘
              depends on 4.1, 4.2
```

---

## 风险与注意事项

### 高风险项
1. **并发问题**: `_activeTasks` 字典的线程安全访问
2. **超时处理**: 确保超时后正确清理状态和停止轴运动
3. **任务取消**: `TaskCompletionSource` 的正确使用

### 中风险项
1. **消息订阅泄漏**: 确保 `OnDestroy` 中清理所有订阅
2. **调用方兼容**: 确保所有调用点都已更新

### 建议措施
- Phase 2 完成后进行充分的单元测试
- Phase 3 每完成一个 Logic 适配，立即进行集成测试
- 保留旧代码的备份，以便快速回滚

---

## 验收检查清单

### 功能验收
- [ ] Initialize: 所有料仓先上升到感应位置，再下降到无感应停止
- [ ] RaiseToSensor: 指定料仓上升到感应位置，返回物料检测结果
- [ ] LoadingCompleted: 指定料仓下降一格
- [ ] ForceStopAll: 立即停止所有料仓运动

### 非功能验收
- [ ] 日志输出清晰，可追踪操作流程
- [ ] 超时正确触发，不会无限等待
- [ ] 内存无泄漏（订阅正确清理）
- [ ] CPU 使用正常（1ms 循环不会造成高占用）

---

*文档创建时间: 2025-12-28*
*最后更新: 2025-12-28*
