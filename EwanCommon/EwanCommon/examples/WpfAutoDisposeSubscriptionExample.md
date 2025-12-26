# WPF：订阅 + 自动随窗口销毁解绑（示例程序）

> 说明：这是示例文档文件，不参与 `EwanCommon.csproj` 编译。  
> 使用方式：在你的 WPF 项目（.NET Framework 4.8）里引用 `EwanCommon.dll`，按下面文件添加即可运行/改造。

## 1) 添加扩展：`MessageBusWpfLifetimeExtensions.cs`

```csharp
using EwanCore.Messaging;
using System;
using System.Threading;
using System.Windows;

namespace EwanCore.Messaging
{
    /// <summary>
    /// WPF：订阅会自动跟随 Window.Closed 解绑。
    /// </summary>
    public static class MessageBusWpfLifetimeExtensions
    {
        public static IDisposable SubscribeOnCurrentContext<TMessage>(
            this IMessageBus bus,
            Window owner,
            Action<TMessage> handler) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var subscription = bus.SubscribeOnCurrentContext<TMessage>(handler);

            EventHandler closedHandler = null;
            closedHandler = (_, __) =>
            {
                owner.Closed -= closedHandler;
                subscription.Dispose();
            };
            owner.Closed += closedHandler;

            return new CompositeDisposable(
                subscription,
                () => owner.Closed -= closedHandler);
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private Action _dispose;

            public CompositeDisposable(IDisposable inner, Action detach)
            {
                if (inner == null) throw new ArgumentNullException(nameof(inner));
                if (detach == null) throw new ArgumentNullException(nameof(detach));

                _dispose = () =>
                {
                    detach();
                    inner.Dispose();
                };
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _dispose, null)?.Invoke();
            }
        }
    }
}
```

## 2) 定义消息：`CounterTick.cs`

```csharp
using EwanCore.Messaging;
using System;

public sealed class CounterTick : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public int Value { get; }
    public CounterTick(int value) => Value = value;
}
```

## 3) App 启动时配置总线：`App.xaml.cs`

```csharp
using EwanCore.Messaging;
using System.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 项目启动时设置全局 bus（框架内 StepChanged 等也会用这一份）
        MessageHub.Current = new MessageBus();
    }
}
```

## 4) 窗口订阅（自动解绑）：`MainWindow.xaml.cs`

```csharp
using EwanCore.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

public partial class MainWindow : Window
{
    private int _count;

    public MainWindow()
    {
        InitializeComponent();

        // 关键：绑定 owner（this），窗口关闭后自动解绑
        MessageHub.Current.SubscribeOnCurrentContext(this, (CounterTick msg) =>
        {
            Title = $"Count = {msg.Value}";
            txtValue.Text = msg.Value.ToString();
        });

        // 模拟后台发布（Post 在后台线程分发；SubscribeOnCurrentContext 会切回 UI 线程）
        _ = Task.Run(async () =>
        {
            while (true)
            {
                MessageHub.Current.Post(new CounterTick(Interlocked.Increment(ref _count)));
                await Task.Delay(300).ConfigureAwait(false);
            }
        });
    }
}
```

## 5) 最小 XAML：`MainWindow.xaml`

```xml
<Window x:Class="YourApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MessageBus AutoDispose Subscription (WPF)" Height="200" Width="400">
    <Grid>
        <TextBlock x:Name="txtValue"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   FontSize="36"
                   Text="0" />
    </Grid>
</Window>
```

## 6) 什么时候还需要 `SubscribeWeak`？

- `SubscribeOnCurrentContext(this, ...)`：**确定性解绑**（窗口关闭立刻解除订阅），推荐 UI 场景默认用这个。
- `SubscribeWeak(...)`：兜底防忘解绑（靠 GC + 后续发布清理），适合一些“绑定不到生命周期事件”的对象。
