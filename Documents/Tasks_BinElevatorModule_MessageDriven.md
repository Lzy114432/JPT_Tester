# BinElevatorModule 消息驱动改造 - 任务分解

## 项目概述

**目标**: 将 `BinElevatorModule` 从轮询模式改造为消息驱动模式，解决 `RaiseToSensor()` 同步阻塞问题

**预估总工时**: 16-20 小时

---

## Epic: BinElevatorModule 消息驱动改造

### Story 1: 消息类型扩展 (预估: 2-3小时)

#### Task 1.1: 扩展 BinElevatorCommandMessage 实现消息接口
- **文件**: `Ewan.Model\Production\BinElevatorCommand.cs`
- **描述**: 让 BinElevatorCommandMessage 实现 IMessage, ICorrelatedMessage<Guid> 接口
- **验收标准**:
  - [ ] 添加 `using EwanCore.Messaging;` 引用
  - [ ] 实现 `IMessage` 接口 (DateTimeOffset Timestamp)
  - [ ] 实现 `ICorrelatedMessage<Guid>` 接口 (Guid CorrelationId)
  - [ ] 保持现有属性兼容
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 1小时
- **依赖**: 无

#### Task 1.2: 扩展 BinElevatorStatusMessage 实现消息接口
- **文件**: `Ewan.Model\Production\BinElevatorCommand.cs`
- **描述**: 让 BinElevatorStatusMessage 实现 IMessage, ICorrelatedMessage<Guid> 接口
- **验收标准**:
  - [ ] 实现 `IMessage` 接口
  - [ ] 实现 `ICorrelatedMessage<Guid>` 接口
  - [ ] 添加 `BinOperationResult` 枚举 (Success, HasMaterial, NoMaterial, Timeout, Error)
  - [ ] 添加 `OperationResult` 属性
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 1小时
- **依赖**: Task 1.1

#### Task 1.3: 添加消息工厂方法
- **文件**: `Ewan.Model\Production\BinElevatorCommand.cs`
- **描述**: 为常用命令添加静态工厂方法
- **验收标准**:
  - [ ] `BinElevatorCommandMessage.RaiseToSensor(int binNumber, string source)` 工厂方法
  - [ ] `BinElevatorCommandMessage.InitializeAll(string source)` 工厂方法
  - [ ] `BinElevatorCommandMessage.ForceStopAll(string source)` 工厂方法
  - [ ] `BinElevatorStatusMessage.MaterialCheckResult(...)` 工厂方法
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 1.2

---

### Story 2: BinElevatorModule 核心改造 (预估: 6-8小时)

#### Task 2.1: 添加消息驱动相关字段
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 添加消息订阅和异步处理所需的字段
- **验收标准**:
  - [ ] 添加 `private IDisposable _commandSubscription;`
  - [ ] 添加 `private IDisposable _raiseToSensorResponder;`
  - [ ] 添加 `private Guid _materialDetectionCorrelationId = Guid.Empty;`
  - [ ] 添加 `ConcurrentDictionary<Guid, TaskCompletionSource<BinElevatorStatusMessage>> _pendingMaterialChecks`
  - [ ] 添加必要的 using 语句
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 1.3

#### Task 2.2: 在 OnInit 中添加消息订阅
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 在 OnInit 方法中订阅命令消息和注册 Request/Reply 响应器
- **验收标准**:
  - [ ] 订阅 `BinElevatorCommandMessage` 消息
  - [ ] 注册 `RespondAsync<BinElevatorCommandMessage, BinElevatorStatusMessage>` 响应器
  - [ ] 日志记录订阅成功
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 1小时
- **依赖**: Task 2.1

#### Task 2.3: 实现 OnCommandReceived 消息处理器
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 实现处理 Initialize、ForceStopAll 等命令的消息处理器
- **验收标准**:
  - [ ] 处理 `Initialize` 命令 -> 调用 PerformHardwareInitialization()
  - [ ] 处理 `ForceStopAll` 命令 -> 调用 ForceStopAllBins()
  - [ ] 处理 `Stop` 命令 -> 调用 StopBinAxis()
  - [ ] 异常处理和日志记录
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 1.5小时
- **依赖**: Task 2.2

#### Task 2.4: 实现 HandleRaiseToSensorRequestAsync 异步响应器
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 实现 RaiseToSensor 的 Request/Reply 异步响应器
- **验收标准**:
  - [ ] 验证 binNumber 有效性
  - [ ] 如果已有感应器信号，直接返回结果
  - [ ] 创建 TaskCompletionSource 并存入 _pendingMaterialChecks
  - [ ] 设置状态机为 Unloading 模式
  - [ ] 等待结果（带超时）
  - [ ] 清理 _pendingMaterialChecks
  - [ ] 异常处理
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 2小时
- **依赖**: Task 2.3

#### Task 2.5: 实现 CheckMaterialDetectionCompletion 方法
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 在 OnRun 中检查异步物料检测完成并通知等待者
- **验收标准**:
  - [ ] 检查感应器状态
  - [ ] 检查超时
  - [ ] 停止轴运动
  - [ ] 通过 TrySetResult 通知等待的 TaskCompletionSource
  - [ ] 发布状态消息
  - [ ] 重置内部状态
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 1.5小时
- **依赖**: Task 2.4

#### Task 2.6: 修改 OnRun 添加完成检查调用
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 在 OnRun 方法中调用 CheckMaterialDetectionCompletion
- **验收标准**:
  - [ ] 在状态机处理后调用 CheckMaterialDetectionCompletion()
  - [ ] 保持现有轮询逻辑不变
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 2.5

#### Task 2.7: 修改 OnDestroy 清理订阅
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 在 OnDestroy 方法中清理消息订阅
- **验收标准**:
  - [ ] Dispose _commandSubscription
  - [ ] Dispose _raiseToSensorResponder
  - [ ] 取消所有待处理的 TaskCompletionSource
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 2.6

---

### Story 3: 接口更新 (预估: 1小时)

#### Task 3.1: 更新 IBinElevator 接口
- **文件**: `Ewan.Core\Module\Interface\IBinElevator.cs`
- **描述**: 添加异步版本的 RaiseToSensor 方法声明
- **验收标准**:
  - [ ] 添加 `Task<BinMaterialCheckResult> RaiseToSensorAsync(int binNumber, CancellationToken ct = default);`
  - [ ] 添加必要的 using 语句
  - [ ] 保持现有接口不变
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 1.3

#### Task 3.2: 实现异步接口 RaiseToSensorAsync
- **文件**: `Ewan.Core\Module\BinElevatorModule.cs`
- **描述**: 在 BinElevatorModule 中实现异步接口
- **验收标准**:
  - [ ] 实现 `RaiseToSensorAsync` 方法
  - [ ] 使用 `MessageHub.Current.RequestAsync` 发送请求
  - [ ] 处理超时和取消
  - [ ] 转换结果为 BinMaterialCheckResult
  - [ ] 编译通过
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 3.1, Task 2.4

---

### Story 4: 调用者改造 (预估: 4-5小时)

#### Task 4.1: 改造 MaterialUnloadingLogic - 添加字段
- **文件**: `Ewan.Core\Logic\MaterialUnloadingLogic.cs`
- **描述**: 添加消息驱动所需的字段
- **验收标准**:
  - [ ] 添加 `private Guid _currentMaterialCheckRequestId;`
  - [ ] 添加 `private TaskCompletionSource<BinMaterialCheckResult> _materialCheckTcs;`
  - [ ] 添加必要的 using 语句
  - [ ] 编译通过
- **优先级**: P1
- **预估**: 0.5小时
- **依赖**: Task 3.2

#### Task 4.2: 改造 MaterialUnloadingLogic - 修改物料检测逻辑
- **文件**: `Ewan.Core\Logic\MaterialUnloadingLogic.cs`
- **描述**: 将 ProcessCheckBinMaterial 改为非阻塞的 Request/Reply 模式
- **验收标准**:
  - [ ] 检查是否有正在等待的请求
  - [ ] 发送 RaiseToSensor 请求消息
  - [ ] 使用 Task.Run 异步等待结果
  - [ ] 处理超时
  - [ ] 保持状态机流程正确
  - [ ] 编译通过
- **优先级**: P1
- **预估**: 2小时
- **依赖**: Task 4.1

#### Task 4.3: 改造 HomeLogic - 使用消息触发初始化
- **文件**: `Ewan.Core\Logic\HomeLogic.cs`
- **描述**: 将 ProcessBinInit 改为使用消息触发
- **验收标准**:
  - [ ] 使用 `MessageHub.Current.Post(BinElevatorCommandMessage.InitializeAll(...))` 替代直接调用
  - [ ] 添加必要的 using 语句
  - [ ] 保持状态机流程正确
  - [ ] 编译通过
- **优先级**: P1
- **预估**: 1小时
- **依赖**: Task 2.3

#### Task 4.4: 改造 LogicManager - 使用消息触发停止
- **文件**: `Ewan.Core\Manager\LogicManager.cs`
- **描述**: 将紧急停止和初始化改为使用消息触发
- **验收标准**:
  - [ ] 使用 `MessageHub.Current.Post(BinElevatorCommandMessage.ForceStopAll(...))` 替代直接调用
  - [ ] 使用 `MessageHub.Current.Post(BinElevatorCommandMessage.InitializeAll(...))` 替代直接调用
  - [ ] 添加必要的 using 语句
  - [ ] 编译通过
- **优先级**: P1
- **预估**: 1小时
- **依赖**: Task 2.3

---

### Story 5: 集成测试与验证 (预估: 3-4小时)

#### Task 5.1: 编译验证
- **描述**: 确保所有改动编译通过
- **验收标准**:
  - [ ] 解决方案完整编译无错误
  - [ ] 无新增警告
- **优先级**: P0
- **预估**: 0.5小时
- **依赖**: Task 4.4

#### Task 5.2: 功能测试 - 料仓初始化
- **描述**: 测试料仓初始化流程
- **验收标准**:
  - [ ] HomeLogic 发送初始化消息
  - [ ] BinElevatorModule 收到并执行初始化
  - [ ] 三个料仓正常完成初始化
  - [ ] 日志记录正确
- **优先级**: P0
- **预估**: 1小时
- **依赖**: Task 5.1

#### Task 5.3: 功能测试 - 物料检测
- **描述**: 测试异步物料检测流程
- **验收标准**:
  - [ ] MaterialUnloadingLogic 发送 RaiseToSensor 请求
  - [ ] BinElevatorModule 异步响应
  - [ ] 正确返回有料/无料/超时结果
  - [ ] 调用者不被阻塞
  - [ ] 日志记录正确
- **优先级**: P0
- **预估**: 1.5小时
- **依赖**: Task 5.2

#### Task 5.4: 功能测试 - 紧急停止
- **描述**: 测试紧急停止流程
- **验收标准**:
  - [ ] LogicManager 发送 ForceStopAll 消息
  - [ ] BinElevatorModule 收到并停止所有料仓
  - [ ] 状态机正确重置
  - [ ] 日志记录正确
- **优先级**: P0
- **预估**: 1小时
- **依赖**: Task 5.2

---

## 任务依赖关系图

```
Story 1: 消息类型扩展
    Task 1.1 ──→ Task 1.2 ──→ Task 1.3
                                  │
                                  ▼
Story 2: BinElevatorModule 核心改造
    Task 2.1 ──→ Task 2.2 ──→ Task 2.3 ──→ Task 2.4 ──→ Task 2.5 ──→ Task 2.6 ──→ Task 2.7
                                  │                                                  │
                                  ▼                                                  │
Story 3: 接口更新                                                                    │
    Task 3.1 ──────────────────────────────────────→ Task 3.2                       │
                                                          │                          │
                                                          ▼                          │
Story 4: 调用者改造                                                                  │
    Task 4.1 ──→ Task 4.2                                                           │
                     ▲                                                               │
    Task 4.3 ────────┼────────────────────────────────────┐                          │
    Task 4.4 ────────┘                                    │                          │
                                                          ▼                          ▼
Story 5: 集成测试与验证
    Task 5.1 ──→ Task 5.2 ──→ Task 5.3
                     └──────→ Task 5.4
```

---

## 关键文件路径

| 文件 | 相对路径 |
|------|----------|
| BinElevatorCommand.cs | `Ewan.Model\Production\BinElevatorCommand.cs` |
| BinElevatorModule.cs | `Ewan.Core\Module\BinElevatorModule.cs` |
| IBinElevator.cs | `Ewan.Core\Module\Interface\IBinElevator.cs` |
| MaterialUnloadingLogic.cs | `Ewan.Core\Logic\MaterialUnloadingLogic.cs` |
| HomeLogic.cs | `Ewan.Core\Logic\HomeLogic.cs` |
| LogicManager.cs | `Ewan.Core\Manager\LogicManager.cs` |

---

## 执行顺序建议

1. **第一阶段** (Day 1 上午): Task 1.1 → 1.2 → 1.3 + Task 3.1
2. **第二阶段** (Day 1 下午): Task 2.1 → 2.2 → 2.3 → 2.4
3. **第三阶段** (Day 2 上午): Task 2.5 → 2.6 → 2.7 + Task 3.2
4. **第四阶段** (Day 2 下午): Task 4.1 → 4.2 → 4.3 → 4.4
5. **第五阶段** (Day 3): Task 5.1 → 5.2 → 5.3 → 5.4
