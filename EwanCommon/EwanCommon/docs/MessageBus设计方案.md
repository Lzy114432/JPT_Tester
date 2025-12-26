# MessageBus（强类型消息总线）设计方案

> 本方案为全新设计，不考虑向前兼容；`EwanCommon` 已内置实现：`EwanCore.Messaging.MessageBus`。

## 1. 设计目标

- **强类型**：消息=类型，编译期检查，避免 `object` 强转与“魔法字符串 topic”。
- **一套总线两种语义**：
  - `Publish<T>`：同步通知（适合状态变化/轻量事件）。
  - `Post<T>`：异步队列分发（适合跨线程解耦/限流/请求响应）。
- **易用可回收**：`Subscribe<T>` 返回 `IDisposable`，销毁时 `Dispose()` 即可取消订阅。
- **基类/接口订阅**：按**消息运行时类型**分发，订阅基类/接口时，发布派生类型也会收到。
- **弱引用订阅**：`SubscribeWeak(...)` 防止 UI/VM 忘记解绑导致的对象泄漏。
- **内置 Request/Reply**：`RequestAsync/Respond` 简化请求响应配合，避免手工桥接等待表。
- **时间戳规范化**：所有消息都带 `Timestamp`（`DateTimeOffset`，默认由总线在 Publish/Post 时补齐）。
- **可观测**：处理器异常隔离（默认不影响其它订阅者），队列溢出可通过事件感知。
- **UI 友好**：不依赖 WinForms/WPF；通过 `SynchronizationContext` 扩展实现 UI 线程调度。

## 2. 核心类型

- `IMessage`：消息接口（包含 `Timestamp`）。
- `IMessageBus`：总线接口（`Subscribe/Publish/Post`）。
- `MessageBus`：默认实现（支持 `HandlerException/MessageDropped` 事件）。
- `MessageHub`：进程级入口（`MessageHub.Current`，可由项目替换为自己的 bus 实例）。
- `MessageBusOptions` / `MessageOverflowStrategy`：异步队列容量与溢出策略。
- `ICorrelatedMessage<TKey>`：带关联 ID 的消息契约（Request/Reply 用）。
- `RequestAsync/Respond`：内置 Request/Reply（基于 CorrelationId）。
- `RequestAwaiter<TKey,TResult>`：底层等待表（框架内部使用；需要更细控制时也可直接用）。

## 3. 线程模型与语义

### 3.1 Publish（同步）

- 在**调用线程**中执行订阅者回调。
- 默认对每个 handler 做 try/catch：
  - 单个 handler 抛异常不会影响其它 handler。
  - 异常通过 `MessageBus.HandlerException` 上报（可选记录日志/报警）。

### 3.2 Post（异步队列）

- `Post` 把消息放入**有界队列**，由后台线程按序消费并分发（内部调用 `Publish`）。
- 默认溢出策略：`DropOldest`（丢最旧的一条，尽量保证“最新消息”能进队列）。
- 溢出事件：`MessageBus.MessageDropped`（可用于报警/指标/死信）。

## 4. 使用方式

### 4.1 创建并注册全局 bus（推荐）

```csharp
using EwanCore.Messaging;

var bus = new MessageBus(new MessageBusOptions
{
    AsyncQueueCapacity = 1024,
    OverflowStrategy = MessageOverflowStrategy.DropOldest,
});

MessageHub.Current = bus; // 让框架内（如 LogicBase.StepChanged 广播）也使用这一份 bus
```

也可以直接用默认实例：`MessageBus.Default`（但建议项目还是显式设置 `MessageHub.Current`，避免多实例混用）。

### 4.2 定义消息

```csharp
using EwanCore.Messaging;
using System;

public sealed class RecipeRead : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public string RecipeName { get; }
    public string Batch { get; }

    public RecipeRead(string recipeName, string batch)
    {
        RecipeName = recipeName ?? string.Empty;
        Batch = batch ?? string.Empty;
    }
}
```

### 4.3 订阅 / 取消订阅

```csharp
using var sub = MessageHub.Current.Subscribe<RecipeRead>(m =>
{
    Console.WriteLine($"recipe={m.RecipeName}, batch={m.Batch}");
});
```

支持订阅基类/接口（发布派生类型时也会收到）：

```csharp
public interface IRecipeEvent : IMessage { }

public sealed class RecipeRead : IRecipeEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public string RecipeName { get; set; }
}

using var sub = MessageHub.Current.Subscribe<IRecipeEvent>(evt =>
{
    // 会收到 RecipeRead（以及其它实现 IRecipeEvent 的消息）
});
```

弱引用订阅（防泄漏，目标对象 GC 后会自动失效并在后续发布中清理）：

```csharp
// 推荐：lambda 不捕获 target，用第一个参数 me 访问实例即可
using var sub = MessageHub.Current.SubscribeWeak(this, (me, RecipeRead msg) => me.OnRecipe(msg));

// 或：直接传实例方法组（method group）
using var sub2 = MessageHub.Current.SubscribeWeak(this, OnRecipe);
```

### 4.4 发布

- 同步（轻量通知）：

```csharp
MessageHub.Current.Publish(new RecipeRead("R001", "B202501"));
```

- 异步（入队分发）：

```csharp
MessageHub.Current.Post(new RecipeRead("R001", "B202501"));
```

### 4.5 UI 线程调度（WinForms/WPF 通用）

在 UI 线程创建订阅，使用扩展方法自动切回 UI 线程：

```csharp
using EwanCore.Messaging;

using var sub = MessageHub.Current.SubscribeOnCurrentContext<RecipeRead>(m =>
{
    // 这里已在 UI 线程
    // lblRecipe.Text = m.RecipeName;
});
```

### 4.6 请求/响应（内置 Request/Reply）

推荐做法：让请求/响应消息实现 `ICorrelatedMessage<Guid>`，使用 `RequestAsync/Respond` 完成配合（内部基于等待表实现）。

```csharp
using EwanCore.Messaging;
using System;

public sealed class MesRequest : IMessage, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Api { get; set; }
    public object Payload { get; set; }
}

public sealed class MesResult : IMessage, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
    public object Data { get; set; }
}

using var bus = (MessageBus)MessageHub.Current;

// 后台模块：收到请求后回响应（自动拷贝 CorrelationId）
using var responder = bus.Respond<MesRequest, MesResult>(req => new MesResult
{
    Success = true,
    Data = "OK"
});

// 流程侧：发送请求 -> await 结果（CorrelationId 若为空会自动生成）
var result = await bus.RequestAsync<MesRequest, MesResult>(
    new MesRequest { Api = "Confirm", Payload = new { Sn = "SN001" } },
    timeoutMs: 5000);
```

说明：

- `Respond/RespondAsync` 内部会把 `CorrelationId` 从 request 复制到 reply。
- 若 `Respond/RespondAsync` 处理过程中抛异常/入队失败，并且该 `CorrelationId` 存在等待中的 `RequestAsync`，会直接以异常完成等待（避免只剩超时）。

如果你需要更细粒度控制（例如：自定义关联键类型、一个请求对应多个响应、或跨总线桥接），仍可直接使用底层 `RequestAwaiter<TKey,TResult>`。

## 5. 约定建议

- **命名**：用类型/命名空间表达“域.动作”，例如：
  - `MyProject.Messages.Plc.PlcSnapshotUpdated`
  - `MyProject.Messages.Mes.MesRequest` / `MesResult`
- **轻量 handler**：`Publish` 是同步调用，handler 内只做“状态更新/切线程/投递后台任务”。
- **订阅粒度**：支持订阅基类/接口；若需要监控全部消息可用 `SubscribeAll`。
- **内存管理**：长期订阅建议显式 `Dispose()`；UI/VM 场景可用 `SubscribeWeak` 防止忘解绑泄漏。

## 6. 文件结构（EwanCommon 内置）

```
EwanCommon/EwanCore/Messaging/
  Abstractions/
    IMessage.cs
    ICorrelatedMessage.cs
    IMessageBus.cs
    IMessageBusDiagnostics.cs
    IMessageBusExceptionReporter.cs
    IPublishBus.cs
    ISubscribeBus.cs
    IRequestReplyBus.cs
  Contracts/
    MessageBase.cs
    MessageDroppedEventArgs.cs
    MessageHandlerExceptionEventArgs.cs
  Core/
    MessageBus.cs
    MessageBusOptions.cs
    MessageHub.cs
    MessageOverflowStrategy.cs
    RequestAwaiter.cs
  Extensions/
    MessageBusExtensions.cs
```

## 7. 注意事项

- 不要把 `MessageHub.Current` 指向已 `Dispose()` 的 bus。
- 队列策略 `Block` 可能阻塞发布线程；仅用于你明确希望“必须入队”的场景。
- 若异步队列发生溢出：订阅 `MessageDropped` 做报警/统计/降频。
