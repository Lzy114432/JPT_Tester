# MessageBus 使用指南

> 本文档描述 `EwanCore.Messaging` 消息总线系统的使用方法。

## 概述

`MessageHub` 是一个静态单例消息总线，**无需手动初始化**，可直接使用：

```csharp
using EwanCore.Messaging;

// 直接使用，无需初始化
MessageHub.Current.Post(new MyMessage());
```

---

## 消息类型一览

所有消息类型统一位于 `Ewan.Model.Messages` 命名空间：

| 消息类型 | 用途 | 说明 |
|---------|------|------|
| `SystemControlMessage` | 系统控制 | Start/Stop/Pause/Resume/EmergencyStop |
| `StatusIndicatorCommand` | 状态指示器 | 三色灯/蜂鸣器控制 |
| `SafetyAlertMessage` | 安全报警 | 报警级别和类型 |
| `BeltConveyorControlMessage` | 皮带控制 | 皮带启停请求 |
| `RingLineDataMessage` | 环线数据 | 环线要料信号 |
| `RingLineHeartbeatMessage` | 环线心跳 | 心跳状态 |
| `BinElevatorCommandMessage` | 料仓升降命令 | 升降控制指令 |
| `BinElevatorStatusMessage` | 料仓升降状态 | 升降状态反馈 |
| `LoadingUnloadingStateMessage` | 装卸状态 | 装料/卸料状态 |
| `MesRingLineRequest` | MES环线请求 | MES上行请求 |
| `MesRingLineFeedback` | MES环线反馈 | MES下行响应 |
| `UILogMessage` | UI日志 | 日志显示消息 |

---

## 发布消息

### Publish vs Post 区别

| 方法 | 执行方式 | 返回值 | 使用场景 |
|------|---------|--------|---------|
| `Publish<T>()` | **同步** - 在调用线程执行 | void | 轻量通知、同线程事件 |
| `Post<T>()` | **异步** - 入队后台线程执行 | bool | 跨线程解耦、避免阻塞 |

### 发布示例

```csharp
using EwanCore.Messaging;
using Ewan.Model.Messages;

// 同步发布 - 立即执行所有订阅者
MessageHub.Current.Publish(new SystemControlMessage(SystemControlCommand.Start));

// 异步发布 - 入队后台线程执行（推荐）
MessageHub.Current.Post(new SystemControlMessage(SystemControlCommand.Stop));

// 使用静态工厂方法
MessageHub.Current.Post(SystemControlMessage.Pause("SafetyModule", "急停按钮按下"));
MessageHub.Current.Post(SystemControlMessage.EmergencyStop("SafetyModule", "机械手报警"));
```

---

## 订阅消息

### 订阅模式

```csharp
using EwanCore.Messaging;
using Ewan.Model.Messages;
using System;

public class MyModule : BaseModule<MyModule>
{
    private IDisposable _subscription;  // 保存订阅句柄

    protected override void OnInit()
    {
        // 订阅消息，返回 IDisposable
        _subscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControl);
    }

    protected override void OnDestroy()
    {
        // 销毁时取消订阅（重要！防止内存泄漏）
        _subscription?.Dispose();
    }

    private void OnSystemControl(SystemControlMessage message)
    {
        // 直接使用强类型消息，无需类型转换
        switch (message.Command)
        {
            case SystemControlCommand.Start:
                // 处理启动
                break;
            case SystemControlCommand.Stop:
                // 处理停止
                break;
            case SystemControlCommand.Pause:
                // 处理暂停
                break;
            case SystemControlCommand.EmergencyStop:
                // 处理急停
                break;
        }
    }
}
```

### 多消息订阅

```csharp
private IDisposable _systemControlSubscription;
private IDisposable _beltControlSubscription;

protected override void OnInit()
{
    _systemControlSubscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControl);
    _beltControlSubscription = MessageHub.Current.Subscribe<BeltConveyorControlMessage>(OnBeltControl);
}

protected override void OnDestroy()
{
    _systemControlSubscription?.Dispose();
    _beltControlSubscription?.Dispose();
}
```

---

## Request/Reply 模式

用于需要等待响应的场景（如 MES 通信）：

```csharp
// 发送请求并等待响应
var request = new MesRingLineRequest
{
    CorrelationId = Guid.NewGuid(),
    Action = MesRingLineAction.FeedingQianLiaocang,
    PlateCode = "PLATE001",
    TimeoutMs = 30000
};

try
{
    var response = await MessageHub.Current.RequestAsync<MesRingLineRequest, MesRingLineFeedback>(
        request,
        request.TimeoutMs);

    if (response.Success)
    {
        // 处理成功响应
    }
}
catch (TimeoutException)
{
    // 处理超时
}
```

### 注册响应处理器

```csharp
protected override void OnInit()
{
    // 注册 Request/Reply 处理器
    MessageHub.Current.RespondAsync<MesRingLineRequest, MesRingLineFeedback>(HandleMesRequest);
}

private async Task<MesRingLineFeedback> HandleMesRequest(MesRingLineRequest request)
{
    // 处理请求并返回响应
    return new MesRingLineFeedback
    {
        CorrelationId = request.CorrelationId,
        Action = request.Action,
        Success = true,
        Message = "处理成功"
    };
}
```

---

## 线程模型

```
┌─────────────────────────────────────────────────────────────┐
│  调用线程                        后台线程                      │
│                                                              │
│  Post(msg) ───────────► BlockingCollection                   │
│                              ↓                               │
│                         WorkerLoop()                         │
│                              ↓                               │
│                    ┌─────────┴─────────┐                     │
│                    ↓                   ↓                     │
│              Handler1(msg)       Handler2(msg)               │
│              (订阅者1)            (订阅者2)                   │
│                                                              │
│  Publish(msg) ──► Handler1(msg) ──► Handler2(msg)           │
│  (同步，在调用线程依次执行所有订阅者)                           │
└─────────────────────────────────────────────────────────────┘
```

### 关键特性

1. **线程安全**：订阅/取消订阅/发布均为线程安全操作
2. **异常隔离**：单个订阅者异常不影响其他订阅者
3. **弱引用支持**：`SubscribeWeak()` 自动清理已GC对象
4. **队列限流**：Post 队列默认容量 1024，可配置溢出策略

---

## 创建新消息类型

### 步骤

1. 在 `Ewan.Model\Messages\` 目录创建新文件
2. 实现 `IMessage` 接口
3. 在 `Ewan.Model.csproj` 添加文件引用

### 消息模板

```csharp
using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// [消息描述]
    /// </summary>
    public sealed class XxxMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳（自动补齐）
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// [属性描述]
        /// </summary>
        public string Property1 { get; set; }

        /// <summary>
        /// [属性描述]
        /// </summary>
        public int Property2 { get; set; }

        public XxxMessage() { }

        public XxxMessage(string property1, int property2)
        {
            Property1 = property1;
            Property2 = property2;
        }
    }
}
```

---

## 与旧系统对比

| 特性 | 旧系统 (MsgManager) | 新系统 (MessageHub) |
|------|---------------------|---------------------|
| 初始化 | `MsgManager.Instance()` | 无需初始化 |
| 订阅 | `new MsgListener()` + `RegisterListener()` | `Subscribe<T>()` |
| 取消订阅 | `UnRegisterListener()` | `Dispose()` |
| 发布 | `PushMsg(new MessageModel(...))` | `Post<T>(msg)` |
| 类型安全 | ❌ 需要 `GetData<T>()` 强转 | ✅ 强类型 |
| 线程模型 | 单一队列 | 同步/异步可选 |
| Request/Reply | ❌ 不支持 | ✅ 支持 |

---

## 常见问题

### Q1: 订阅者没有收到消息？
- 检查是否正确调用了 `Subscribe<T>()`
- 确认消息类型匹配（泛型类型必须一致）
- 确认订阅者没有被提前 `Dispose()`

### Q2: 如何在 UI 线程处理消息？
```csharp
private void OnMessage(MyMessage msg)
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        // UI 操作
    });
}
```

### Q3: 如何防止内存泄漏？
- 在 `OnDestroy()` 中调用 `subscription.Dispose()`
- 或使用 `SubscribeWeak()` 弱引用订阅

---

## 文件位置

| 内容 | 路径 |
|------|------|
| MessageHub 实现 | `EwanCommon\EwanCore\Messaging\Core\MessageHub.cs` |
| MessageBus 实现 | `EwanCommon\EwanCore\Messaging\Core\MessageBus.cs` |
| IMessage 接口 | `EwanCommon\EwanCore\Messaging\Abstractions\IMessage.cs` |
| 消息类型定义 | `Ewan.Model\Messages\*.cs` |
