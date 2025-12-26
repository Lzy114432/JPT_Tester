using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 强类型消息总线：
    /// - Publish：同步调用订阅者（适合轻量“事件通知”）
    /// - Post：进入异步队列由后台线程分发（适合跨线程解耦、限流/背压）
    /// </summary>
    public sealed class MessageBus : IMessageBus, IMessageBusDiagnostics, IMessageBusExceptionReporter
    {
        /// <summary>
        /// 默认全局实例（适合小项目快速上手）。
        /// </summary>
        public static MessageBus Default { get; } = new MessageBus();

        private readonly MessageBusOptions _options;

        private readonly ConcurrentDictionary<Type, IHandlerSet> _typedHandlers =
            new ConcurrentDictionary<Type, IHandlerSet>();

        private static readonly ConcurrentDictionary<Type, Type[]> s_dispatchTypes =
            new ConcurrentDictionary<Type, Type[]>();

        private Action<IMessage>? _allHandlers;

        private readonly ConcurrentDictionary<Type, IReplyAwaiter> _replyAwaiters =
            new ConcurrentDictionary<Type, IReplyAwaiter>();

        private readonly BlockingCollection<QueuedMessage> _queue;
        private Task? _worker;
        private int _workerStarted;
        private int _disposed;
        private int _resourcesDisposed;

        private long _totalPublished;
        private long _totalDropped;
        private long _totalHandlerExceptions;

        public event EventHandler<MessageHandlerExceptionEventArgs>? HandlerException;
        public event EventHandler<MessageDroppedEventArgs>? MessageDropped;

        public MessageBus(MessageBusOptions? options = null)
        {
            _options = options ?? new MessageBusOptions();
            if (_options.AsyncQueueCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "AsyncQueueCapacity must be > 0.");
            }

            _queue = new BlockingCollection<QueuedMessage>(
                new ConcurrentQueue<QueuedMessage>(),
                boundedCapacity: _options.AsyncQueueCapacity);
        }

        public int QueueLength
        {
            get
            {
                if (Volatile.Read(ref _disposed) == 1)
                {
                    return 0;
                }

                try
                {
                    return _queue.Count;
                }
                catch (ObjectDisposedException)
                {
                    return 0;
                }
            }
        }

        public long TotalPublished => Volatile.Read(ref _totalPublished);

        public long TotalDropped => Volatile.Read(ref _totalDropped);

        public long TotalHandlerExceptions => Volatile.Read(ref _totalHandlerExceptions);

        public int GetSubscriberCount<TMessage>() where TMessage : IMessage => GetSubscriberCount(typeof(TMessage));

        public int GetSubscriberCount(Type messageType)
        {
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));

            if (_typedHandlers.TryGetValue(messageType, out var set))
            {
                return set.GetSubscriberCount();
            }

            return 0;
        }

        public IReadOnlyList<Type> SubscribedTypes
        {
            get
            {
                var list = new List<Type>();
                foreach (var type in _typedHandlers.Keys)
                {
                    if (GetSubscriberCount(type) > 0)
                    {
                        list.Add(type);
                    }
                }
                return list;
            }
        }

        public void ResetStatistics()
        {
            Interlocked.Exchange(ref _totalPublished, 0);
            Interlocked.Exchange(ref _totalDropped, 0);
            Interlocked.Exchange(ref _totalHandlerExceptions, 0);
        }

        public IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : IMessage
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var set = (HandlerSet<TMessage>)_typedHandlers.GetOrAdd(typeof(TMessage), _ => new HandlerSet<TMessage>());
            var entry = new StrongHandlerEntry<TMessage>(handler);
            set.Add(entry);

            return new Subscription(() => set.Remove(entry));
        }

        public IDisposable SubscribeWeak<TTarget, TMessage>(
            TTarget target,
            Action<TTarget, TMessage> handler)
            where TTarget : class
            where TMessage : IMessage
        {
            ThrowIfDisposed();
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var set = (HandlerSet<TMessage>)_typedHandlers.GetOrAdd(typeof(TMessage), _ => new HandlerSet<TMessage>());
            var entry = new WeakHandlerEntry<TTarget, TMessage>(target, handler);
            set.Add(entry);
            return new Subscription(() => set.Remove(entry));
        }

        public IDisposable SubscribeWeak<TTarget, TMessage>(
            TTarget target,
            Action<TMessage> handler)
            where TTarget : class
            where TMessage : IMessage
        {
            ThrowIfDisposed();
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (handler.Target == null || handler.Method.IsStatic)
            {
                return Subscribe(handler);
            }

            if (!ReferenceEquals(handler.Target, target))
            {
                throw new ArgumentException(
                    "The handler must be an instance method bound to the provided target. " +
                    "If you are using a lambda, use SubscribeWeak(target, (me, msg) => ...) instead.",
                    nameof(handler));
            }

            Action<TTarget, TMessage> openHandler;
            var method = handler.Method;
            try
            {
                openHandler = (Action<TTarget, TMessage>)Delegate.CreateDelegate(typeof(Action<TTarget, TMessage>), null, method);
            }
            catch (ArgumentException)
            {
                openHandler = (me, msg) => method.Invoke(me, new object[] { msg });
            }

            return SubscribeWeak(target, openHandler);
        }

        public IDisposable SubscribeAll(Action<IMessage> handler)
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            AddAllHandler(handler);
            return new Subscription(() => RemoveAllHandler(handler));
        }

        public Task<TReply> RequestAsync<TRequest, TReply>(
            TRequest request,
            int timeoutMs = 30000,
            CancellationToken cancellationToken = default,
            bool postRequest = true)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>
        {
            ThrowIfDisposed();
            if (request == null) throw new ArgumentNullException(nameof(request));

            var correlationId = request.CorrelationId;
            if (correlationId == Guid.Empty)
            {
                correlationId = Guid.NewGuid();
                request.CorrelationId = correlationId;
            }

            var replyAwaiter = (ReplyAwaiter<TReply>)_replyAwaiters.GetOrAdd(
                typeof(TReply),
                _ => new ReplyAwaiter<TReply>(this));

            var pending = replyAwaiter.Awaiter.Register(correlationId, timeoutMs, cancellationToken);

            try
            {
                if (postRequest)
                {
                    if (!Post(request))
                    {
                        replyAwaiter.Awaiter.TryCancel(correlationId);
                        throw new InvalidOperationException("Request enqueue failed (queue overflow).");
                    }
                }
                else
                {
                    Publish(request);
                }
            }
            catch
            {
                replyAwaiter.Awaiter.TryCancel(correlationId);
                throw;
            }

            return pending;
        }

        public IDisposable Respond<TRequest, TReply>(Func<TRequest, TReply> handler, bool postReply = true)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return Subscribe<TRequest>(req =>
            {
                try
                {
                    var reply = handler(req);
                    if (reply == null)
                    {
                        return;
                    }

                    reply.CorrelationId = req.CorrelationId;
                    if (postReply)
                    {
                        if (!Post(reply))
                        {
                            throw new InvalidOperationException("Reply enqueue failed (queue overflow).");
                        }
                    }
                    else
                    {
                        Publish(reply);
                    }
                }
                catch (Exception ex)
                {
                    TryFailPendingReply<TReply>(req.CorrelationId, ex);
                    OnHandlerException(ex, req, handler);

                    if (!_options.CatchHandlerExceptions)
                    {
                        throw;
                    }
                }
            });
        }

        public IDisposable RespondAsync<TRequest, TReply>(Func<TRequest, Task<TReply>> handler, bool postReply = true)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return Subscribe<TRequest>(req =>
            {
                var task = RespondAsyncCore(req, handler, postReply);
                _ = task.ContinueWith(
                    t => _ = t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            });
        }

        public void Publish<TMessage>(TMessage message) where TMessage : IMessage
        {
            ThrowIfDisposed();
            if (message == null) throw new ArgumentNullException(nameof(message));

            EnsureTimestamp(message);

            Interlocked.Increment(ref _totalPublished);
            PublishCore(message);
        }

        public bool Post<TMessage>(TMessage message) where TMessage : IMessage
        {
            ThrowIfDisposed();
            if (message == null) throw new ArgumentNullException(nameof(message));

            EnsureTimestamp(message);

            EnsureWorker();

            var item = new QueuedMessage(message, () => DispatchFromQueue(message));
            var enqueued = Enqueue(item);
            if (enqueued)
            {
                Interlocked.Increment(ref _totalPublished);
            }
            return enqueued;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            foreach (var awaiter in _replyAwaiters.Values)
            {
                try
                {
                    awaiter.Dispose();
                }
                catch
                {
                    // ignore
                }
            }

            try
            {
                _queue.CompleteAdding();
            }
            catch
            {
                // ignore
            }

            var worker = _worker;
            if (worker == null)
            {
                DisposeResources();
                return;
            }

            if (Task.CurrentId == worker.Id)
            {
                _ = worker.ContinueWith(
                    _ => DisposeResources(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return;
            }

            var finished = false;
            try
            {
                finished = worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                finished = worker.IsCompleted;
            }

            if (finished)
            {
                DisposeResources();
                return;
            }

            _ = worker.ContinueWith(
                _ => DisposeResources(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private bool Enqueue(QueuedMessage item)
        {
            switch (_options.OverflowStrategy)
            {
                case MessageOverflowStrategy.Block:
                    try
                    {
                        _queue.Add(item);
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                case MessageOverflowStrategy.DropNewest:
                    try
                    {
                        if (_queue.TryAdd(item))
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                    OnMessageDropped(item.Message);
                    return false;

                case MessageOverflowStrategy.DropOldest:
                    try
                    {
                        if (_queue.TryAdd(item))
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                    try
                    {
                        if (_queue.TryTake(out var dropped))
                        {
                            OnMessageDropped(dropped.Message);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                    try
                    {
                        if (_queue.TryAdd(item))
                        {
                            return true;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }

                    OnMessageDropped(item.Message);
                    return false;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DisposeResources()
        {
            if (Interlocked.Exchange(ref _resourcesDisposed, 1) == 1)
            {
                return;
            }

            try
            {
                _queue.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        private void EnsureWorker()
        {
            if (Interlocked.CompareExchange(ref _workerStarted, 1, 0) != 0)
            {
                return;
            }

            _worker = Task.Factory.StartNew(
                WorkerLoop,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void WorkerLoop()
        {
            foreach (var item in _queue.GetConsumingEnumerable())
            {
                try
                {
                    item.Dispatch();
                }
                catch
                {
                    // Publish 内部已对 handler 异常做隔离；这里仅兜底，避免线程退出。
                }
            }
        }

        private void DispatchFromQueue(IMessage message)
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                return;
            }

            PublishCore(message);
        }

        private void PublishTyped(IMessage message)
        {
            var runtimeType = message.GetType();
            var dispatchTypes = GetDispatchTypes(runtimeType);

            for (var i = 0; i < dispatchTypes.Length; i++)
            {
                if (_typedHandlers.TryGetValue(dispatchTypes[i], out var set))
                {
                    set.Invoke(this, message);
                }
            }
        }

        private void PublishAll(IMessage message)
        {
            var snapshot = Volatile.Read(ref _allHandlers);
            if (snapshot == null)
            {
                return;
            }

            if (!_options.CatchHandlerExceptions)
            {
                snapshot(message);
                return;
            }

            var delegates = snapshot.GetInvocationList();
            for (var i = 0; i < delegates.Length; i++)
            {
                var d = (Action<IMessage>)delegates[i];
                try
                {
                    d(message);
                }
                catch (Exception ex)
                {
                    OnHandlerException(ex, message, d);
                }
            }
        }

        private void PublishCore(IMessage message)
        {
            PublishTyped(message);
            PublishAll(message);
        }

        private void AddAllHandler(Action<IMessage> handler)
        {
            Action<IMessage>? prev;
            Action<IMessage>? next;
            do
            {
                prev = Volatile.Read(ref _allHandlers);
                next = (Action<IMessage>)Delegate.Combine(prev, handler);
            } while (Interlocked.CompareExchange(ref _allHandlers, next, prev) != prev);
        }

        private void RemoveAllHandler(Action<IMessage> handler)
        {
            Action<IMessage>? prev;
            Action<IMessage>? next;
            do
            {
                prev = Volatile.Read(ref _allHandlers);
                next = (Action<IMessage>)Delegate.Remove(prev, handler);
            } while (Interlocked.CompareExchange(ref _allHandlers, next, prev) != prev);
        }

        private void OnMessageDropped(IMessage message)
        {
            Interlocked.Increment(ref _totalDropped);
            MessageDropped?.Invoke(this, new MessageDroppedEventArgs(_options.OverflowStrategy, message));
        }

        private void OnHandlerException(Exception exception, IMessage message, Delegate handler)
        {
            Interlocked.Increment(ref _totalHandlerExceptions);
            HandlerException?.Invoke(this, new MessageHandlerExceptionEventArgs(exception, message, handler));
        }

        public void ReportHandlerException(Exception exception, IMessage message, Delegate handler) =>
            OnHandlerException(exception, message, handler);

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                throw new ObjectDisposedException(nameof(MessageBus));
            }
        }

        private static void EnsureTimestamp<TMessage>(TMessage message) where TMessage : IMessage
        {
            if (message.Timestamp == default)
            {
                message.Timestamp = DateTimeOffset.Now;
            }
        }

        private static Type[] GetDispatchTypes(Type runtimeType)
        {
            return s_dispatchTypes.GetOrAdd(runtimeType, t =>
            {
                var list = new List<Type>();
                var seen = new HashSet<Type>();

                void AddType(Type type)
                {
                    if (type == null)
                    {
                        return;
                    }

                    if (seen.Add(type))
                    {
                        list.Add(type);
                    }
                }

                AddType(t);

                var baseType = t.BaseType;
                while (baseType != null && baseType != typeof(object))
                {
                    AddType(baseType);
                    baseType = baseType.BaseType;
                }

                var interfaces = t.GetInterfaces();
                for (var i = 0; i < interfaces.Length; i++)
                {
                    AddType(interfaces[i]);
                }

                return list.ToArray();
            });
        }

        private async Task RespondAsyncCore<TRequest, TReply>(
            TRequest request,
            Func<TRequest, Task<TReply>> handler,
            bool postReply)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>
        {
            try
            {
                var reply = await handler(request).ConfigureAwait(false);
                if (reply == null)
                {
                    return;
                }

                reply.CorrelationId = request.CorrelationId;
                if (postReply)
                {
                    if (!Post(reply))
                    {
                        throw new InvalidOperationException("Reply enqueue failed (queue overflow).");
                    }
                }
                else
                {
                    Publish(reply);
                }
            }
            catch (Exception ex)
            {
                TryFailPendingReply<TReply>(request.CorrelationId, ex);
                OnHandlerException(ex, request, handler);

                if (!_options.CatchHandlerExceptions)
                {
                    throw;
                }
            }
        }

        private interface IHandlerSet
        {
            void Invoke(MessageBus bus, IMessage message);
            int GetSubscriberCount();
        }

        private interface IReplyAwaiter : IDisposable
        {
        }

        private bool TryFailPendingReply<TReply>(Guid correlationId, Exception exception)
            where TReply : IMessage, ICorrelatedMessage<Guid>
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            if (correlationId == Guid.Empty)
            {
                return false;
            }

            if (!_replyAwaiters.TryGetValue(typeof(TReply), out var boxed))
            {
                return false;
            }

            if (!(boxed is ReplyAwaiter<TReply> awaiter))
            {
                return false;
            }

            if (exception is OperationCanceledException)
            {
                return awaiter.Awaiter.TryCancel(correlationId);
            }

            return awaiter.Awaiter.TrySetException(correlationId, exception);
        }

        private sealed class ReplyAwaiter<TReply> : IReplyAwaiter
            where TReply : IMessage, ICorrelatedMessage<Guid>
        {
            public RequestAwaiter<Guid, TReply> Awaiter { get; }
            private readonly IDisposable _subscription;

            public ReplyAwaiter(MessageBus bus)
            {
                Awaiter = new RequestAwaiter<Guid, TReply>();
                _subscription = bus.Subscribe<TReply>(res => Awaiter.TrySetResult(res.CorrelationId, res));
            }

            public void Dispose()
            {
                _subscription.Dispose();
                Awaiter.Dispose();
            }
        }

        private abstract class HandlerEntry<TMessage> where TMessage : IMessage
        {
            public abstract Delegate Handler { get; }

            public virtual bool IsAlive => true;

            public abstract bool TryInvoke(TMessage message);
        }

        private sealed class StrongHandlerEntry<TMessage> : HandlerEntry<TMessage> where TMessage : IMessage
        {
            private readonly Action<TMessage> _handler;

            public StrongHandlerEntry(Action<TMessage> handler)
            {
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            public override Delegate Handler => _handler;

            public override bool TryInvoke(TMessage message)
            {
                _handler(message);
                return true;
            }
        }

        private sealed class WeakHandlerEntry<TTarget, TMessage> : HandlerEntry<TMessage>
            where TTarget : class
            where TMessage : IMessage
        {
            private readonly WeakReference<TTarget> _target;
            private readonly Action<TTarget, TMessage> _handler;

            public WeakHandlerEntry(TTarget target, Action<TTarget, TMessage> handler)
            {
                _target = new WeakReference<TTarget>(target ?? throw new ArgumentNullException(nameof(target)));
                _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            }

            public override Delegate Handler => _handler;

            public override bool IsAlive => _target.TryGetTarget(out _);

            public override bool TryInvoke(TMessage message)
            {
                if (!_target.TryGetTarget(out var target))
                {
                    return false;
                }

                _handler(target, message);
                return true;
            }
        }

        private sealed class HandlerSet<TMessage> : IHandlerSet where TMessage : IMessage
        {
            private ImmutableArray<HandlerEntry<TMessage>> _entries = ImmutableArray<HandlerEntry<TMessage>>.Empty;

            public ImmutableArray<HandlerEntry<TMessage>> Snapshot => _entries;

            public void Add(HandlerEntry<TMessage> entry)
            {
                if (entry == null) throw new ArgumentNullException(nameof(entry));
                ImmutableInterlocked.Update(ref _entries, current => current.Add(entry));
            }

            public void Remove(HandlerEntry<TMessage> entry)
            {
                if (entry == null) throw new ArgumentNullException(nameof(entry));
                ImmutableInterlocked.Update(ref _entries, current => current.Remove(entry));
            }

            void IHandlerSet.Invoke(MessageBus bus, IMessage message) => Invoke(bus, (TMessage)message);

            int IHandlerSet.GetSubscriberCount()
            {
                var snapshot = Snapshot;
                if (snapshot.IsEmpty)
                {
                    return 0;
                }

                var count = 0;
                for (var i = 0; i < snapshot.Length; i++)
                {
                    if (snapshot[i].IsAlive)
                    {
                        count++;
                    }
                }
                return count;
            }

            private void Invoke(MessageBus bus, TMessage message)
            {
                var snapshot = Snapshot;
                if (snapshot.IsEmpty)
                {
                    return;
                }

                if (!bus._options.CatchHandlerExceptions)
                {
                    var prune = false;
                    for (var i = 0; i < snapshot.Length; i++)
                    {
                        if (!snapshot[i].TryInvoke(message))
                        {
                            prune = true;
                        }
                    }

                    if (prune)
                    {
                        PruneDead(snapshot);
                    }
                    return;
                }

                var pruneCatch = false;
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var entry = snapshot[i];
                    try
                    {
                        if (!entry.TryInvoke(message))
                        {
                            pruneCatch = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        bus.OnHandlerException(ex, message, entry.Handler);
                    }
                }

                if (pruneCatch)
                {
                    PruneDead(snapshot);
                }
            }

            private void PruneDead(ImmutableArray<HandlerEntry<TMessage>> snapshot)
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    var current = attempt == 0 ? snapshot : Snapshot;
                    if (current.IsEmpty)
                    {
                        return;
                    }

                    var builder = ImmutableArray.CreateBuilder<HandlerEntry<TMessage>>(current.Length);
                    for (var i = 0; i < current.Length; i++)
                    {
                        var entry = current[i];
                        if (entry.IsAlive)
                        {
                            builder.Add(entry);
                        }
                    }

                    if (builder.Count == current.Length)
                    {
                        return;
                    }

                    var next = builder.Count == 0
                        ? ImmutableArray<HandlerEntry<TMessage>>.Empty
                        : builder.ToImmutable();

                    var previous = ImmutableInterlocked.InterlockedCompareExchange(ref _entries, next, current);
                    if (previous.Equals(current))
                    {
                        return;
                    }
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action? _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _dispose, null)?.Invoke();
            }
        }

        private sealed class QueuedMessage
        {
            public IMessage Message { get; }
            private readonly Action _dispatch;

            public QueuedMessage(IMessage message, Action dispatch)
            {
                Message = message ?? throw new ArgumentNullException(nameof(message));
                _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            }

            public void Dispatch()
            {
                _dispatch();
            }
        }
    }
}
