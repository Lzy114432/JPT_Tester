using System;
using System.Threading;
using System.Threading.Tasks;

namespace EwanCore.Messaging
{
    public static class MessageBusExtensions
    {
        /// <summary>
        /// 带过滤条件的订阅。
        /// </summary>
        public static IDisposable Subscribe<TMessage>(
            this ISubscribeBus bus,
            Func<TMessage, bool> predicate,
            Action<TMessage> handler) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return bus.Subscribe<TMessage>(msg =>
            {
                if (predicate(msg))
                {
                    handler(msg);
                }
            });
        }

        /// <summary>
        /// 在指定 <see cref="SynchronizationContext"/> 上处理消息（适合 UI 线程调度）。
        /// </summary>
        public static IDisposable SubscribeOnContext<TMessage>(
            this ISubscribeBus bus,
            SynchronizationContext context,
            Action<TMessage> handler) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return bus.Subscribe<TMessage>(msg => context.Post(_ => handler(msg), null));
        }

        /// <summary>
        /// 在当前线程的 <see cref="SynchronizationContext"/> 上处理消息。
        /// </summary>
        public static IDisposable SubscribeOnCurrentContext<TMessage>(
            this ISubscribeBus bus,
            Action<TMessage> handler) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));

            var context = SynchronizationContext.Current;
            if (context == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current is null. Call this method from UI thread or provide a context explicitly.");
            }

            return bus.SubscribeOnContext(context, handler);
        }

        /// <summary>
        /// 异步订阅处理器：处理器在后台线程执行，避免阻塞发布线程。
        /// </summary>
        public static IDisposable SubscribeAsync<TMessage>(
            this ISubscribeBus bus,
            Func<TMessage, Task> handler) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return bus.Subscribe<TMessage>(msg =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await handler(msg).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                    catch (Exception ex)
                    {
                        if (bus is IMessageBusExceptionReporter reporter)
                        {
                            reporter.ReportHandlerException(ex, msg, handler);
                        }
                    }
                });
            });
        }

        /// <summary>
        /// 带 <see cref="CancellationToken"/> 的异步订阅处理器。
        /// </summary>
        public static IDisposable SubscribeAsync<TMessage>(
            this ISubscribeBus bus,
            Func<TMessage, CancellationToken, Task> handler,
            CancellationToken cancellationToken = default) where TMessage : IMessage
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return bus.Subscribe<TMessage>(msg =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await handler(msg, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // ignore
                    }
                    catch (Exception ex)
                    {
                        if (bus is IMessageBusExceptionReporter reporter)
                        {
                            reporter.ReportHandlerException(ex, msg, handler);
                        }
                    }
                }, cancellationToken);
            });
        }
    }
}
