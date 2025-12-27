# MessageBus 系统重构工作表

> 目标：将 `Ewan.Core\Msg` 旧消息系统完全迁移到 `EwanCommon\EwanCore\Messaging` 强类型消息总线

## 重构概览

### 当前系统 (旧)

```
Ewan.Core\Msg\
├── MsgSubject.cs        # 枚举定义消息主题 (魔法字符串)
├── MsgListener.cs       # 监听器类 (手动注册/注销)
├── MessageModel.cs      # 消息模型 (object Data, 需要强转)
├── MsgManager.cs        # 消息管理器 (单例, 队列分发)
└── MesMessages.cs       # 已迁移到 IMessage 模式
```

### 目标系统 (新)

```
EwanCommon\EwanCore\Messaging\
├── Abstractions\        # IMessage, IMessageBus 等接口
├── Contracts\           # MessageBase, EventArgs
├── Core\                # MessageBus, MessageHub 实现
├── Extensions\          # 扩展方法
└── Messages\            # 强类型消息定义
```

---

## 阶段总览

| 阶段 | 描述 | 状态 | 预计文件数 |
|------|------|------|------------|
| 1 | 定义强类型消息 | ✅ 已完成 | 8 |
| 2 | 迁移发布端 | ✅ 已完成 | 5 |
| 3 | 迁移订阅端 | ✅ 已完成 | 6 |
| 4 | 删除旧代码 | ✅ 已完成 | 5 |
| 5 | 验证与测试 | ⬜ 待测试 | - |

---

## 阶段 1: 定义强类型消息

### 1.1 消息类型映射表

| MsgSubject 枚举值 | 新消息类型 | 命名空间 | 状态 |
|-------------------|------------|----------|------|
| UILog | `UILogMessage` | `EwanCore.Messaging.Messages` | ✅ 已存在 |
| StatusIndicator | `StatusIndicatorCommand` | `Ewan.Model.System` | ✅ 已存在 |
| SafetyAlert | `SafetyAlertMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| SystemStatus | `SystemStatusMessage` | `Ewan.Model.System` | ✅ 已更新 (IMessage) |
| SystemControl | `SystemControlMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| BinElevatorCommand | `BinElevatorCommandMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| BinElevatorStatus | `BinElevatorStatusMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| LoadingandunloadingState | `LoadingUnloadingStateMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| RingLineData | `RingLineDataMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| RingLineheartData | `RingLineHeartbeatMessage` | `Ewan.Model.Messages` | ✅ 已创建 |
| BeltConveyorControl | `BeltConveyorControlMessage` | `Ewan.Model.Messages` | ✅ 已创建 |

### 1.2 任务清单 - 创建消息类型

#### 任务 1.2.1: 创建 Ewan.Model\Messages 目录结构
- [ ] 创建 `Ewan.Model\Messages\` 目录
- [ ] 更新 `Ewan.Model.csproj` 添加文件引用

#### 任务 1.2.2: SafetyAlertMessage
```csharp
// 文件: Ewan.Model\Messages\SafetyAlertMessage.cs
// 用途: 安全报警消息
// 数据: AlertType, Message, IsActive, Severity
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.3: SystemStatusMessage
```csharp
// 文件: Ewan.Model\Messages\SystemStatusMessage.cs
// 用途: 系统状态变化消息
// 数据: Status (enum), PreviousStatus, Description
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.4: SystemControlMessage
```csharp
// 文件: Ewan.Model\Messages\SystemControlMessage.cs
// 用途: 系统控制命令 (启动/停止/暂停/急停)
// 数据: Command (enum), Source, Reason
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.5: BinElevatorCommandMessage
```csharp
// 文件: Ewan.Model\Messages\BinElevatorCommandMessage.cs
// 用途: 料仓升降控制指令
// 数据: Command, TargetPosition, Parameters
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.6: BinElevatorStatusMessage
```csharp
// 文件: Ewan.Model\Messages\BinElevatorStatusMessage.cs
// 用途: 料仓升降状态反馈
// 数据: CurrentPosition, IsMoving, Error
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.7: LoadingUnloadingStateMessage
```csharp
// 文件: Ewan.Model\Messages\LoadingUnloadingStateMessage.cs
// 用途: 装卸状态消息
// 数据: State, Location, Material
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.8: RingLineDataMessage
```csharp
// 文件: Ewan.Model\Messages\RingLineDataMessage.cs
// 用途: 环线数据消息
// 数据: (需要查看 RingLineModel 确定)
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.9: RingLineHeartbeatMessage
```csharp
// 文件: Ewan.Model\Messages\RingLineHeartbeatMessage.cs
// 用途: 环线心跳消息
// 数据: Timestamp, Status
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

#### 任务 1.2.10: BeltConveyorControlMessage
```csharp
// 文件: Ewan.Model\Messages\BeltConveyorControlMessage.cs
// 用途: 皮带传送控制消息
// 数据: Command, Speed, Direction
```
- [ ] 创建文件
- [ ] 定义属性
- [ ] 添加构造函数

---

## 阶段 2: 迁移发布端

### 2.1 发布端迁移映射表

| 文件 | 旧代码 | 新代码 | 状态 |
|------|--------|--------|------|
| SafetyModule.cs:338 | `PushMsg(SystemControl, Pause)` | `MessageHub.Current.Post(SystemControlMessage)` | ⬜ |
| SafetyModule.cs:367 | `PushMsg(SystemControl, EmergencyStop)` | `MessageHub.Current.Post(SystemControlMessage)` | ⬜ |
| SafetyModule.cs:401 | `PushMsg(SafetyAlert, ...)` | `MessageHub.Current.Post(SafetyAlertMessage)` | ⬜ |
| RingLineModule.cs:136 | `PushMsg(RingLineData, ...)` | `MessageHub.Current.Post(RingLineDataMessage)` | ⬜ |
| MaterialLoadingModule.cs:514 | `PushMsg(BeltConveyorControl, ...)` | `MessageHub.Current.Post(BeltConveyorControlMessage)` | ⬜ |
| MaterialLoadingModule.cs:537 | `PushMsg(BeltConveyorControl, ...)` | `MessageHub.Current.Post(BeltConveyorControlMessage)` | ⬜ |
| MaterialUnloadingModule.cs:663 | `PushMsg(BeltConveyorControl, ...)` | `MessageHub.Current.Post(BeltConveyorControlMessage)` | ⬜ |
| MaterialUnloadingModule.cs:680 | `PushMsg(BeltConveyorControl, ...)` | `MessageHub.Current.Post(BeltConveyorControlMessage)` | ⬜ |
| SystemControlService.cs:266-267 | `PushMsg(SystemControl, ...)` | `MessageHub.Current.Post(SystemControlMessage)` | ⬜ |

### 2.2 任务清单 - 迁移发布端

#### 任务 2.2.1: SafetyModule.cs 发布端迁移
- [ ] 添加 `using EwanCore.Messaging;`
- [ ] 添加 `using Ewan.Model.Messages;`
- [ ] 替换 Line 338: Pause 命令
- [ ] 替换 Line 367: EmergencyStop 命令
- [ ] 替换 Line 401: SafetyAlert 消息
- [ ] 移除对 `_msgManager.PushMsg()` 的调用

#### 任务 2.2.2: RingLineModule.cs 发布端迁移
- [ ] 添加必要的 using
- [ ] 替换 Line 136: RingLineData 发布
- [ ] 移除 `MsgManager.Instance()` 调用

#### 任务 2.2.3: MaterialLoadingModule.cs 发布端迁移
- [ ] 添加必要的 using
- [ ] 替换 Line 514: BeltConveyorControl
- [ ] 替换 Line 537: BeltConveyorControl
- [ ] 移除 `MsgManager.Instance()` 调用

#### 任务 2.2.4: MaterialUnloadingModule.cs 发布端迁移
- [ ] 添加必要的 using
- [ ] 替换 Line 663: BeltConveyorControl
- [ ] 替换 Line 680: BeltConveyorControl
- [ ] 移除 `MsgManager.Instance()` 调用

#### 任务 2.2.5: SystemControlService.cs 发布端迁移
- [ ] 添加必要的 using
- [ ] 替换 Line 266-267: SystemControl 发布
- [ ] 移除 `MsgManager.Instance()` 调用

---

## 阶段 3: 迁移订阅端

### 3.1 订阅端迁移映射表

| 文件 | 旧代码 | 新代码 | 状态 |
|------|--------|--------|------|
| SafetyModule.cs | `MsgListener(SystemControl, ...)` | `MessageHub.Current.Subscribe<SystemControlMessage>()` | ⬜ |
| ProductionLineModule.cs | `MsgListener(SystemControl, ...)` | `MessageHub.Current.Subscribe<SystemControlMessage>()` | ⬜ |
| MaterialUnloadingModule.cs | `MsgListener(RingLineData, ...)` | `MessageHub.Current.Subscribe<RingLineDataMessage>()` | ⬜ |
| BinElevatorModule.cs | `MsgListener(SystemStatus, ...)` | `MessageHub.Current.Subscribe<SystemStatusMessage>()` | ⬜ |
| BeltConveyorModule.cs | `MsgListener(SystemControl, ...)` | `MessageHub.Current.Subscribe<SystemControlMessage>()` | ⬜ |
| BeltConveyorModule.cs | `MsgListener(BeltConveyorControl, ...)` | `MessageHub.Current.Subscribe<BeltConveyorControlMessage>()` | ⬜ |

### 3.2 迁移模式

**旧模式:**
```csharp
private MsgListener _systemControlListener;
private MsgManager _msgManager;

protected override void OnInit()
{
    _msgManager = MsgManager.Instance();
    _systemControlListener = new MsgListener(MsgSubject.SystemControl, OnSystemControlMessage);
    _msgManager.RegisterListener(_systemControlListener);
}

protected override void OnDestroy()
{
    _msgManager.UnRegisterListener(_systemControlListener);
}

private void OnSystemControlMessage(MessageModel msg)
{
    var command = msg.GetData<SystemControlCommand>();
    // 处理逻辑
}
```

**新模式:**
```csharp
private IDisposable _systemControlSubscription;

protected override void OnInit()
{
    _systemControlSubscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControlMessage);
}

protected override void OnDestroy()
{
    _systemControlSubscription?.Dispose();
}

private void OnSystemControlMessage(SystemControlMessage message)
{
    // 直接使用强类型消息，无需强转
    var command = message.Command;
    // 处理逻辑
}
```

### 3.3 任务清单 - 迁移订阅端

#### 任务 3.3.1: SafetyModule.cs 订阅端迁移
- [ ] 将 `MsgListener _systemControlListener` 改为 `IDisposable _systemControlSubscription`
- [ ] 移除 `MsgManager _msgManager` 字段
- [ ] 更新 OnInit(): 使用 `MessageHub.Current.Subscribe<SystemControlMessage>()`
- [ ] 更新 OnDestroy(): 使用 `_systemControlSubscription?.Dispose()`
- [ ] 更新 `OnSystemControlMessage` 方法签名为 `(SystemControlMessage message)`
- [ ] 移除 `msg.GetData<T>()` 调用

#### 任务 3.3.2: ProductionLineModule.cs 订阅端迁移
- [ ] 将 `MsgListener` 改为 `IDisposable`
- [ ] 更新 OnInit()
- [ ] 更新 OnDestroy()
- [ ] 更新消息处理方法签名

#### 任务 3.3.3: MaterialUnloadingModule.cs 订阅端迁移
- [ ] 将 `MsgListener _msgManager2` 改为 `IDisposable _ringLineSubscription`
- [ ] 更新 OnInit()
- [ ] 更新 OnDestroy()
- [ ] 更新 `CallBackShow1` 方法

#### 任务 3.3.4: BinElevatorModule.cs 订阅端迁移
- [ ] 将 `MsgListener _systemStatusListener` 改为 `IDisposable`
- [ ] 移除 `MsgManager _msgManager` 字段
- [ ] 更新 OnInit()
- [ ] 更新 OnDestroy()
- [ ] 更新 `OnSystemStatusChanged` 方法

#### 任务 3.3.5: BeltConveyorModule.cs 订阅端迁移
- [ ] 将两个 `MsgListener` 改为 `IDisposable`
- [ ] 移除 `MsgManager _msgManager` 字段
- [ ] 更新 OnInit(): 两个订阅
- [ ] 更新 OnDestroy(): 两个 Dispose
- [ ] 更新两个消息处理方法

---

## 阶段 4: 删除旧代码

### 4.1 待删除文件列表

| 文件路径 | 用途 | 依赖检查 | 状态 |
|----------|------|----------|------|
| `Ewan.Core\Msg\MsgSubject.cs` | 消息主题枚举 | 确认无引用后删除 | ✅ 已删除 |
| `Ewan.Core\Msg\MsgListener.cs` | 监听器类 | 确认无引用后删除 | ✅ 已删除 |
| `Ewan.Core\Msg\MessageModel.cs` | 消息模型 | 确认无引用后删除 | ✅ 已删除 |
| `Ewan.Core\Msg\MsgManager.cs` | 消息管理器 | 迁移完成后删除 | ✅ 已删除 |

### 4.2 任务清单 - 删除旧代码

#### 任务 4.2.1: 验证无残留引用
- [x] 搜索 `MsgSubject` 引用
- [x] 搜索 `MsgListener` 引用
- [x] 搜索 `MessageModel` 引用 (注意不要误删 Ewan.Model)
- [x] 搜索 `MsgManager` 引用

#### 任务 4.2.2: 更新项目文件
- [x] 从 `Ewan.Core.csproj` 移除文件引用:
  - `<Compile Include="Msg\MsgSubject.cs" />`
  - `<Compile Include="Msg\MsgListener.cs" />`
  - `<Compile Include="Msg\MessageModel.cs" />`
  - `<Compile Include="Msg\MsgManager.cs" />`

#### 任务 4.2.3: 删除文件
- [x] 删除 `Ewan.Core\Msg\MsgSubject.cs`
- [x] 删除 `Ewan.Core\Msg\MsgListener.cs`
- [x] 删除 `Ewan.Core\Msg\MessageModel.cs`
- [x] 删除 `Ewan.Core\Msg\MsgManager.cs`
- [x] 保留 `Ewan.Core\Msg\MesMessages.cs` (已使用新模式)

#### 任务 4.2.4: 清理 using 语句
- [x] 移除所有文件中的 `using Ewan.Core.Msg;` (如果不再需要)

---

## 阶段 5: 验证与测试

### 5.1 编译验证
- [ ] 完整编译解决方案
- [ ] 修复所有编译错误
- [ ] 确认无警告

### 5.2 功能验证

| 功能点 | 测试方法 | 状态 |
|--------|----------|------|
| 系统控制消息 | 测试启动/停止/暂停功能 | ⬜ |
| 安全报警消息 | 触发安全条件，验证报警 | ⬜ |
| 皮带传送控制 | 测试皮带启停 | ⬜ |
| 料仓升降 | 测试升降控制 | ⬜ |
| 环线数据 | 验证数据传输 | ⬜ |

### 5.3 回归测试
- [ ] 运行现有单元测试
- [ ] 手动测试主要工作流程

---

## 附录 A: 代码模板

### 强类型消息模板
```csharp
using System;
using EwanCore.Messaging;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// [消息描述]
    /// </summary>
    public sealed class XxxMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// [属性描述]
        /// </summary>
        public string Property1 { get; set; }

        public XxxMessage()
        {
        }

        public XxxMessage(string property1)
        {
            Property1 = property1;
        }
    }
}
```

### 发布消息模板
```csharp
// 同步发布 (轻量通知，在调用线程执行)
MessageHub.Current.Publish(new SystemControlMessage(SystemControlCommand.Start));

// 异步发布 (入队后台线程执行，推荐用于跨线程)
MessageHub.Current.Post(new SystemControlMessage(SystemControlCommand.Stop));
```

### 订阅消息模板
```csharp
private IDisposable _subscription;

protected override void OnInit()
{
    _subscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControl);
}

protected override void OnDestroy()
{
    _subscription?.Dispose();
}

private void OnSystemControl(SystemControlMessage message)
{
    switch (message.Command)
    {
        case SystemControlCommand.Start:
            // 处理启动
            break;
        case SystemControlCommand.Stop:
            // 处理停止
            break;
    }
}
```

---

## 附录 B: 常见问题

### Q1: 如何处理需要强转的旧代码？
旧代码使用 `msg.GetData<T>()` 进行强转，新代码直接使用强类型消息，无需强转。

### Q2: UI 线程调度如何处理？
使用 `SubscribeOnCurrentContext<T>()` 扩展方法自动切回 UI 线程。

### Q3: 如何防止内存泄漏？
- 在 `OnDestroy()` 中调用 `subscription.Dispose()`
- 或使用 `SubscribeWeak()` 弱引用订阅

---

## 更新日志

| 日期 | 更新内容 | 作者 |
|------|----------|------|
| 2025-12-27 | 创建初始工作表 | Claude |
