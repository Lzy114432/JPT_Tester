using EwanCore.Messaging;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

// 说明：
// - 这是“例子程序文件”，不参与 EwanCommon.csproj 编译。
// - 使用方式：在你的 WinForms 项目（.NET Framework 4.8）里引用 EwanCommon.dll，然后把本文件拷贝进去即可运行/改造。

namespace EwanCommon.Examples
{
    /// <summary>
    /// WinForms：订阅 + 自动随窗体销毁（Form.Dispose）解绑。
    /// </summary>
    internal static class WinFormsAutoDisposeSubscriptionExample
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var bus = new MessageBus();
            MessageHub.Current = bus;

            Application.Run(new MainForm(bus));
        }

        private sealed class CounterTick : IMessage
        {
            public DateTimeOffset Timestamp { get; set; }
            public int Value { get; }
            public CounterTick(int value) => Value = value;
        }

        private sealed class MainForm : Form
        {
            private readonly MessageBus _bus;
            private readonly Timer _timer;
            private int _count;

            public MainForm(MessageBus bus)
            {
                _bus = bus ?? throw new ArgumentNullException(nameof(bus));

                Text = "MessageBus AutoDispose Subscription (WinForms)";
                Width = 640;
                Height = 240;

                var label = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 20f, FontStyle.Regular),
                    Text = "0"
                };
                Controls.Add(label);

                // 关键：订阅时绑定 owner（this），窗体 Dispose 后会自动解绑，不需要手工保存 IDisposable。
                MessageHub.Current.SubscribeOnCurrentContext(this, (CounterTick msg) =>
                {
                    label.Text = msg.Value.ToString();
                });

                _timer = new Timer { Interval = 300 };
                _timer.Tick += (_, __) => _bus.Post(new CounterTick(Interlocked.Increment(ref _count)));
                _timer.Start();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _timer?.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }

    /// <summary>
    /// 放在 WinForms 项目里即可：订阅会自动跟随窗体/控件销毁解绑（避免忘记 Dispose）。
    /// </summary>
    public static class MessageBusWinFormsLifetimeExtensions
    {
        public static IDisposable SubscribeOnCurrentContext<TMessage>(
            this IMessageBus bus,
            Control owner,
            Action<TMessage> handler) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var subscription = bus.SubscribeOnCurrentContext<TMessage>(handler);
            if (owner.IsDisposed)
            {
                subscription.Dispose();
                return subscription;
            }

            EventHandler disposedHandler = null;
            disposedHandler = (_, __) =>
            {
                owner.Disposed -= disposedHandler;
                subscription.Dispose();
            };
            owner.Disposed += disposedHandler;

            return new CompositeDisposable(
                subscription,
                () => owner.Disposed -= disposedHandler);
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
