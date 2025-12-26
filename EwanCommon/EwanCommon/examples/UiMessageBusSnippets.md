# MessageBus 用法示例（强类型 Publish/Post）

`EwanCore.Messaging.MessageBus` 是一个“强类型消息总线”：

- **同步通知**：`Publish<T>(message)`（类似“事件”，在当前线程调用订阅者）
- **异步队列**：`Post<T>(message)`（入队后由后台线程按序分发）
- **订阅可回收**：`Subscribe<T>` 返回 `IDisposable`，`Dispose()` 即可取消订阅
- **基类/接口订阅**：`Subscribe<IBase>` 能收到派生/实现消息
- **弱引用订阅**：`SubscribeWeak(...)` 防止 UI/VM 忘解绑泄漏
- **内置 Request/Reply**：`RequestAsync/Respond`（基于 CorrelationId）

> 框架内（例如 `LogicBase` 的 `StepChanged` 广播）默认走 `MessageHub.Current`；建议项目启动时显式设置 `MessageHub.Current = bus`。

## 1) 定义消息（不再需要 topic 常量）

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

## 2) 发布（Publish vs Post）

```csharp
using EwanCore.Messaging;

// 同步：立刻在当前线程执行订阅者
MessageHub.Current.Publish(new RecipeRead("R001", "B202501"));

// 异步：入队后由后台线程分发（不会阻塞当前线程）
MessageHub.Current.Post(new RecipeRead("R001", "B202501"));
```

建议：

- UI 刷新/状态变化通知：优先 `Publish`
- 高频数据/后台解耦/请求响应：优先 `Post`

## 3) 订阅 + 取消订阅（避免内存泄漏）

```csharp
using EwanCore.Messaging;

private IDisposable _sub;

public void Start()
{
    _sub = MessageHub.Current.Subscribe<RecipeRead>(m =>
    {
        // do something
    });
}

public void Stop()
{
    _sub?.Dispose();
    _sub = null;
}
```

如果你担心 UI/VM 忘记解绑导致泄漏，可以用弱引用订阅（目标对象被 GC 回收后订阅会自动失效并被清理）：

```csharp
using EwanCore.Messaging;

public sealed class MyForm
{
    public MyForm()
    {
        // 推荐：lambda 不捕获 this，用第一个参数 me 访问实例即可
        MessageHub.Current.SubscribeWeak(this, (me, RecipeRead msg) => me.OnRecipeRead(msg));
    }

    private void OnRecipeRead(RecipeRead msg)
    {
        // ...
    }
}
```

## 4) WinForms：自动切回 UI 线程（推荐）

关键点：

- 在 UI 线程创建订阅（确保 `SynchronizationContext.Current` 可用）
- 用 `SubscribeOnCurrentContext` 自动切回 UI 线程
- 在 `OnFormClosing/Dispose` 里 `Dispose()` 取消订阅

```csharp
using EwanCore.Messaging;
using System;
using System.Windows.Forms;

public partial class MainForm : Form
{
    private IDisposable _sub;

    public MainForm()
    {
        InitializeComponent();

        _sub = MessageHub.Current.SubscribeOnCurrentContext<RecipeRead>(m =>
        {
            lblRecipe.Text = m.RecipeName;
            lblBatch.Text = m.Batch;
        });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _sub?.Dispose();
        _sub = null;
        base.OnFormClosing(e);
    }
}
```

如果你希望“订阅自动跟随窗体销毁解绑”（不用手工保存/Dispose），可以在 WinForms 项目里加一个小扩展方法。

完整例子程序见：`EwanCommon/examples/WinFormsAutoDisposeSubscriptionExample.cs`

## 5) WPF：同样适用（SynchronizationContext）

在 WPF UI 线程创建订阅即可（`DispatcherSynchronizationContext` 会自动接管）。

```csharp
using EwanCore.Messaging;
using System;

public sealed class MainVm : IDisposable
{
    private IDisposable _sub;

    public string RecipeName { get; private set; } = string.Empty;

    public MainVm()
    {
        _sub = MessageHub.Current.SubscribeOnCurrentContext<RecipeRead>(m =>
        {
            RecipeName = m.RecipeName;
            // RaisePropertyChanged(nameof(RecipeName));
        });
    }

    public void Dispose()
    {
        _sub?.Dispose();
        _sub = null;
    }
}
```

如果你希望“订阅自动跟随 Window 关闭解绑”，例子见：`EwanCommon/examples/WpfAutoDisposeSubscriptionExample.md`

## 6) 队列溢出（MessageDropped）与处理器异常（HandlerException）

`MessageBus` 提供两个可观测事件：

- `MessageDropped`：异步队列满/溢出策略触发时
- `HandlerException`：单个订阅者抛异常时（默认不会影响其它订阅者）

```csharp
using EwanCore.Messaging;

var bus = new MessageBus();
MessageHub.Current = bus;

bus.MessageDropped += (_, e) =>
{
    // e.Message / e.Strategy
    Console.WriteLine($"Dropped: {e.Message.GetType().Name}, strategy={e.Strategy}");
};

bus.HandlerException += (_, e) =>
{
    Console.WriteLine($"Handler error: {e.Exception}");
};
```
