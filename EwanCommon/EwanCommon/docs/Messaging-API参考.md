# EwanCore.Messaging API 参考文档

## 概述

`EwanCore.Messaging` 是一个强类型、线程安全的进程内消息总线，支持同步/异步发布、弱引用订阅、Request/Reply 模式等特性。

## 目录

- [快速开始](#快速开始)
- [消息定义](#消息定义)
- [发布消息](#发布消息)
- [订阅消息](#订阅消息)
- [Request/Reply 模式](#requestreply-模式)
- [扩展方法](#扩展方法)
- [诊断与监控](#诊断与监控)
- [配置选项](#配置选项)
- [接口参考](#接口参考)
- [线程安全](#线程安全)
- [最佳实践](#最佳实践)
  - [状态可查询 + 事件通知](#6-状态可查询--事件通知推荐架构)

---

## 快速开始

```csharp
using EwanCore.Messaging;

// 1. 定义消息
public record OrderCreated(int OrderId, decimal Amount) : MessageBase;

// 2. 订阅消息
var subscription = MessageHub.Current.Subscribe<OrderCreated>(msg =>
{
    Console.WriteLine($"订单创建: {msg.OrderId}, 金额: {msg.Amount}");
});

// 3. 发布消息
MessageHub.Current.Publish(new OrderCreated(1001, 99.99m));

// 4. 取消订阅
subscription.Dispose();
```

---

## 消息定义

### IMessage - 基础消息接口

所有消息必须实现 `IMessage` 接口：

```csharp
public interface IMessage
{
    DateTimeOffset Timestamp { get; set; }
}
```

- `Timestamp`：消息时间戳，发布时若为默认值会自动填充为当前时间

### MessageBase - 推荐基类

使用 C# 9 record 类型，简化消息定义：

```csharp
public abstract record MessageBase : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
}

// 使用示例
public record UserLoggedIn(string UserId, string UserName) : MessageBase;
public record OrderShipped(int OrderId, DateTime ShipDate) : MessageBase;
```

### ICorrelatedMessage<TKey> - 关联消息接口

用于 Request/Reply 场景：

```csharp
public interface ICorrelatedMessage<TKey> : IMessage
{
    TKey CorrelationId { get; set; }
}

// 使用示例
public record GetUserRequest(Guid UserId) : MessageBase, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
}

public record GetUserResponse(User User) : MessageBase, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
}
```

---

## 发布消息

### Publish - 同步发布

在调用线程中依次调用所有订阅者：

```csharp
MessageHub.Current.Publish(new OrderCreated(1001, 99.99m));
```

**特点：**
- 阻塞直到所有订阅者处理完成
- 适合轻量级事件通知
- 订阅者异常默认被捕获（可配置）

### Post - 异步发布

消息进入队列，由后台线程分发：

```csharp
bool success = MessageHub.Current.Post(new OrderCreated(1001, 99.99m));
if (!success)
{
    // 队列满，消息被丢弃
}
```

**特点：**
- 非阻塞，立即返回
- 适合跨线程解耦、限流场景
- 返回 `false` 表示队列已满

### Publish vs Post 对比

| 特性 | Publish | Post |
|------|---------|------|
| 执行线程 | 调用线程 | 后台线程 |
| 阻塞 | 是 | 否 |
| 返回值 | void | bool (是否入队成功) |
| 适用场景 | 轻量通知 | 跨线程解耦 |

---

## 订阅消息

### Subscribe - 强引用订阅

```csharp
IDisposable subscription = MessageHub.Current.Subscribe<OrderCreated>(msg =>
{
    Console.WriteLine($"收到订单: {msg.OrderId}");
});

// 取消订阅
subscription.Dispose();
```

### SubscribeWeak - 弱引用订阅

当目标对象被 GC 回收后，订阅自动失效：

```csharp
public class OrderHandler
{
    public OrderHandler()
    {
        // 推荐写法：lambda 不捕获 this，用参数 me 访问
        MessageHub.Current.SubscribeWeak(this, (me, msg) => me.HandleOrder(msg));
    }

    private void HandleOrder(OrderCreated msg)
    {
        Console.WriteLine($"处理订单: {msg.OrderId}");
    }
}

// 或直接传方法组
MessageHub.Current.SubscribeWeak(this, HandleOrder);
```

**注意：** lambda 不要捕获 `this`，否则弱引用无效。

### SubscribeAll - 订阅所有消息

用于日志、监控等场景：

```csharp
MessageHub.Current.SubscribeAll(msg =>
{
    Console.WriteLine($"[{msg.Timestamp}] {msg.GetType().Name}");
});
```

### 多态订阅

订阅基类/接口时，发布派生类也会收到：

```csharp
// 定义消息层次
public record UserEvent : MessageBase;
public record UserLoggedIn(string UserId) : UserEvent;
public record UserLoggedOut(string UserId) : UserEvent;

// 订阅基类，收到所有 UserEvent
MessageHub.Current.Subscribe<UserEvent>(msg =>
{
    Console.WriteLine($"用户事件: {msg.GetType().Name}");
});

// 发布派生类
MessageHub.Current.Publish(new UserLoggedIn("user1"));  // 触发上面的订阅
```

---

## Request/Reply 模式

### 定义请求/响应消息

```csharp
public record GetUserRequest(Guid UserId) : MessageBase, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
}

public record GetUserResponse(User? User, string? Error) : MessageBase, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
}
```

### 注册响应处理器

```csharp
// 同步处理器
MessageHub.Current.Respond<GetUserRequest, GetUserResponse>(req =>
{
    var user = _userService.GetById(req.UserId);
    return new GetUserResponse(user, null);
});

// 异步处理器
MessageHub.Current.RespondAsync<GetUserRequest, GetUserResponse>(async req =>
{
    var user = await _userService.GetByIdAsync(req.UserId);
    return new GetUserResponse(user, null);
});
```

### 发送请求

```csharp
try
{
    var response = await MessageHub.Current.RequestAsync<GetUserRequest, GetUserResponse>(
        new GetUserRequest(userId),
        timeoutMs: 5000,
        cancellationToken: cts.Token);

    Console.WriteLine($"用户: {response.User?.Name}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("请求超时或被取消");
}
```

### RequestAsync 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| request | TRequest | - | 请求消息 |
| timeoutMs | int | 30000 | 超时毫秒，<0 表示不超时 |
| cancellationToken | CancellationToken | default | 外部取消 |
| postRequest | bool | true | 是否使用 Post（false 则用 Publish） |

---

## 扩展方法

### Subscribe (带过滤条件)

```csharp
MessageHub.Current.Subscribe<OrderCreated>(
    predicate: msg => msg.Amount > 100,  // 过滤条件
    handler: msg => Console.WriteLine($"大额订单: {msg.OrderId}"));
```

### SubscribeOnContext (UI 线程调度)

```csharp
// 在指定 SynchronizationContext 处理
MessageHub.Current.SubscribeOnContext(
    SynchronizationContext.Current!,
    (OrderCreated msg) => label1.Text = $"订单: {msg.OrderId}");

// 在当前上下文处理（必须从 UI 线程调用）
MessageHub.Current.SubscribeOnCurrentContext<OrderCreated>(msg =>
{
    // 在 UI 线程执行
    listBox1.Items.Add(msg.OrderId);
});
```

### SubscribeAsync (异步处理器)

处理器在后台线程执行，不阻塞发布方：

```csharp
// 无取消令牌
MessageHub.Current.SubscribeAsync<HeavyTask>(async msg =>
{
    await ProcessAsync(msg);
});

// 带取消令牌
var cts = new CancellationTokenSource();
MessageHub.Current.SubscribeAsync<HeavyTask>(
    async (msg, ct) =>
    {
        await ProcessAsync(msg, ct);
    },
    cts.Token);
```

---

## 诊断与监控

### IMessageBusDiagnostics 接口

```csharp
var diag = MessageHub.Diagnostics;

// 队列状态
Console.WriteLine($"队列长度: {diag.QueueLength}");

// 统计计数
Console.WriteLine($"已发布: {diag.TotalPublished}");
Console.WriteLine($"已丢弃: {diag.TotalDropped}");
Console.WriteLine($"异常数: {diag.TotalHandlerExceptions}");

// 订阅信息
Console.WriteLine($"OrderCreated 订阅数: {diag.GetSubscriberCount<OrderCreated>()}");
Console.WriteLine($"已订阅类型: {string.Join(", ", diag.SubscribedTypes.Select(t => t.Name))}");

// 重置统计
diag.ResetStatistics();
```

### 事件监听

```csharp
var bus = (MessageBus)MessageHub.Current;

// 处理器异常事件
bus.HandlerException += (sender, e) =>
{
    Log.Error($"处理 {e.Message.GetType().Name} 时异常: {e.Exception.Message}");
};

// 消息丢弃事件
bus.MessageDropped += (sender, e) =>
{
    Log.Warn($"消息被丢弃 ({e.Strategy}): {e.Message.GetType().Name}");
};
```

---

## 配置选项

### MessageBusOptions

```csharp
var options = new MessageBusOptions
{
    AsyncQueueCapacity = 2048,                          // 异步队列容量
    OverflowStrategy = MessageOverflowStrategy.DropOldest,  // 溢出策略
    CatchHandlerExceptions = true                       // 捕获处理器异常
};

var bus = new MessageBus(options);
MessageHub.Current = bus;
```

### MessageOverflowStrategy 溢出策略

| 策略 | 说明 |
|------|------|
| `DropNewest` | 丢弃新消息（不入队） |
| `DropOldest` | 丢弃最旧消息，新消息入队 |
| `Block` | 阻塞等待队列空位（可能阻塞发布线程） |

---

## 接口参考

### 接口继承关系

```
IPublishBus
    ├── Publish<T>(msg)
    └── Post<T>(msg) : bool

ISubscribeBus
    ├── Subscribe<T>(handler) : IDisposable
    ├── SubscribeWeak<TTarget,T>(target, handler) : IDisposable
    └── SubscribeAll(handler) : IDisposable

IRequestReplyBus
    ├── RequestAsync<TReq,TReply>(...) : Task<TReply>
    ├── Respond<TReq,TReply>(handler) : IDisposable
    └── RespondAsync<TReq,TReply>(handler) : IDisposable

IMessageBus : IPublishBus, ISubscribeBus, IRequestReplyBus, IDisposable

IMessageBusDiagnostics
    ├── QueueLength : int
    ├── TotalPublished : long
    ├── TotalDropped : long
    ├── TotalHandlerExceptions : long
    ├── GetSubscriberCount<T>() : int
    ├── GetSubscriberCount(Type) : int
    ├── SubscribedTypes : IReadOnlyList<Type>
    └── ResetStatistics()

IMessageBusExceptionReporter
    └── ReportHandlerException(ex, msg, handler)
```

### MessageHub 静态入口

```csharp
public static class MessageHub
{
    // 完整接口（可替换）
    public static IMessageBus Current { get; set; }

    // 分离视图（限制调用者能力）
    public static IPublishBus PublishBus { get; }
    public static ISubscribeBus SubscribeBus { get; }
    public static IRequestReplyBus RequestReplyBus { get; }
    public static IMessageBusDiagnostics Diagnostics { get; }
}
```

---

## 线程安全

| 组件 | 机制 |
|------|------|
| HandlerSet | `ImmutableArray` + `ImmutableInterlocked` |
| AllHandlers | `Volatile.Read/Write` + `Interlocked.CompareExchange` |
| 异步队列 | `BlockingCollection<T>` |
| 统计计数 | `Interlocked.Increment/Read` |
| Disposed 标记 | `Volatile.Read` + `Interlocked.Exchange` |
| RequestAwaiter | `ConcurrentDictionary` + `TaskCompletionSource` |

所有公共 API 都是线程安全的，可从任意线程调用。

---

## 最佳实践

### 1. 消息设计

```csharp
// ✅ 推荐：使用 record，不可变
public record OrderCreated(int OrderId, decimal Amount) : MessageBase;

// ❌ 避免：可变消息
public class OrderCreated : IMessage
{
    public int OrderId { get; set; }  // 可变，不安全
}
```

### 2. 订阅管理

```csharp
// ✅ 推荐：保存订阅引用，适时取消
private readonly CompositeDisposable _subscriptions = new();

public void Initialize()
{
    _subscriptions.Add(MessageHub.Current.Subscribe<Msg1>(Handle1));
    _subscriptions.Add(MessageHub.Current.Subscribe<Msg2>(Handle2));
}

public void Dispose()
{
    _subscriptions.Dispose();
}

// ✅ 推荐：UI 组件使用弱引用
public class MyForm : Form
{
    public MyForm()
    {
        MessageHub.Current.SubscribeWeak(this, (me, msg) => me.OnMessage(msg));
    }
}
```

### 3. 异常处理

```csharp
// ✅ 推荐：监听异常事件
bus.HandlerException += (s, e) => Log.Error(e.Exception, "消息处理异常");

// ✅ 推荐：关键处理器内部 try-catch
MessageHub.Current.Subscribe<CriticalEvent>(msg =>
{
    try
    {
        ProcessCritical(msg);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "处理关键事件失败");
        // 可能需要重试或告警
    }
});
```

### 4. 性能优化

```csharp
// ✅ 推荐：高频消息使用 Post
MessageHub.Current.Post(new SensorData(value));  // 非阻塞

// ✅ 推荐：重型处理使用 SubscribeAsync
MessageHub.Current.SubscribeAsync<HeavyTask>(async msg =>
{
    await ProcessAsync(msg);  // 后台线程
});

// ❌ 避免：Publish 中执行耗时操作
MessageHub.Current.Subscribe<Msg>(msg =>
{
    Thread.Sleep(1000);  // 阻塞发布线程
});
```

### 5. DI 集成

```csharp
// 注册
services.AddSingleton<IMessageBus>(sp =>
{
    var bus = new MessageBus(new MessageBusOptions { ... });
    return bus;
});

// 替换全局入口
var bus = serviceProvider.GetRequiredService<IMessageBus>();
MessageHub.Current = bus;

// 或直接注入使用
public class OrderService
{
    private readonly IPublishBus _bus;

    public OrderService(IPublishBus bus)
    {
        _bus = bus;
    }

    public void CreateOrder(Order order)
    {
        // ...
        _bus.Publish(new OrderCreated(order.Id, order.Amount));
    }
}
```

### 6. 状态可查询 + 事件通知（推荐架构）

对于需要跟踪状态的组件，推荐使用"属性存储状态 + 消息通知变化"的模式：

```
┌─────────────────────────────────────────────────────┐
│  Manager / Service                                  │
│  ┌───────────────┐    ┌──────────────────────────┐  │
│  │ 状态（属性）   │    │ 通知（MessageHub）        │  │
│  │ .IsConnected  │    │ Publish(StateChanged)    │  │
│  │ .Status       │    │                          │  │
│  └───────────────┘    └──────────────────────────┘  │
│         ↑                        ↑                  │
│      主动查询                  被动订阅              │
└─────────────────────────────────────────────────────┘
```

**后台服务实现：**

```csharp
public class HardwareManager : BaseManager<HardwareManager>
{
    // ✅ 状态可查询（属性）
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    public DeviceStatus Status { get; private set; }

    // ✅ 状态变化时发布通知
    private void OnConnectionChanged(bool connected)
    {
        IsConnected = connected;
        MessageHub.Current.Publish(new HardwareConnectionChanged(connected));
    }

    private void OnStatusChanged(DeviceStatus status)
    {
        Status = status;
        MessageHub.Current.Publish(new HardwareStatusChanged(status));
    }
}
```

**UI 使用：**

```csharp
protected override void OnLoad(EventArgs e)
{
    base.OnLoad(e);

    // 1. 主动查状态（初始化/刷新时）
    var hw = HardwareManager.Instance();
    lblConnection.Text = hw.IsConnected ? "已连接" : "断开";
    lblStatus.Text = hw.Status.ToString();

    // 2. 订阅变化（运行时更新）
    MessageHub.Current.Subscribe<HardwareConnectionChanged>(msg =>
        lblConnection.Text = msg.IsConnected ? "已连接" : "断开");

    MessageHub.Current.Subscribe<HardwareStatusChanged>(msg =>
        lblStatus.Text = msg.Status.ToString());
}
```

**为什么推荐这种模式：**

| 对比项 | 纯事件驱动 | 状态 + 事件 |
|--------|-----------|-------------|
| 获取当前值 | 必须等下次事件 | 随时可查属性 |
| 订阅时机 | 必须在发布前 | 无限制 |
| 调试便利性 | 难追踪状态 | 断点直接看属性 |
| 初始化依赖 | 严格顺序要求 | 灵活 |

**职责分离：**

- **Manager 属性** → 存储当前状态（可查询）
- **MessageHub** → 广播状态变化（通知）

这种模式下，MessageHub 专注于"通知"职责，不负责"存储"。状态的真实来源（Source of Truth）始终是 Manager 的属性。

---

## 版本历史

| 版本 | 日期 | 变更 |
|------|------|------|
| 1.0 | 2024-12 | 初始版本：基础发布/订阅 |
| 1.1 | 2024-12 | 新增弱引用订阅、Request/Reply |
| 1.2 | 2024-12 | 接口分离（ISP）、诊断接口、稳定性增强 |
