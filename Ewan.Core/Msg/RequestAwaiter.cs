using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Msg
{
    /// <summary>
    /// 轻量“请求/响应等待器”（用于异步消息总线场景）：
    /// - 请求侧：Register(correlationId) 后发布请求消息；await Task 完成后推进状态机
    /// - 响应侧：收到响应消息后 TrySetResult(correlationId, result)
    /// </summary>
    /// <typeparam name="TKey">关联键（建议 Guid/string）</typeparam>
    /// <typeparam name="TResult">响应结果类型</typeparam>
    public sealed class RequestAwaiter<TKey, TResult> : IDisposable
    {
        private readonly ConcurrentDictionary<TKey, TaskCompletionSource<TResult>> _pending =
            new ConcurrentDictionary<TKey, TaskCompletionSource<TResult>>();

        private int _disposed;

        /// <summary>
        /// 注册一个等待中的请求并返回可等待的 Task。
        /// </summary>
        /// <param name="key">关联键（CorrelationId）</param>
        /// <param name="timeoutMs">超时毫秒；小于 0 表示不超时</param>
        /// <param name="cancellationToken">外部取消</param>
        public Task<TResult> Register(TKey key, int timeoutMs = 30000, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(key, tcs))
            {
                throw new InvalidOperationException($"Duplicate pending key: {key}");
            }

            if (Volatile.Read(ref _disposed) == 1)
            {
                TaskCompletionSource<TResult> removed;
                if (_pending.TryRemove(key, out removed))
                {
                    removed.TrySetCanceled();
                }
                throw new ObjectDisposedException(nameof(RequestAwaiter<TKey, TResult>));
            }

            if (timeoutMs < 0 && !cancellationToken.CanBeCanceled)
            {
                return tcs.Task;
            }

            CancellationTokenSource cts = null;
            if (timeoutMs >= 0)
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);
            }

            var token = cts != null ? cts.Token : cancellationToken;

            var disposed = 0;
            Action disposeCts = () =>
            {
                if (cts == null)
                {
                    return;
                }

                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    cts.Dispose();
                }
            };

            CancellationTokenRegistration registration = default(CancellationTokenRegistration);
            if (token.CanBeCanceled)
            {
                registration = token.Register(() =>
                {
                    TaskCompletionSource<TResult> canceledTcs;
                    if (_pending.TryRemove(key, out canceledTcs))
                    {
                        canceledTcs.TrySetCanceled();
                    }
                    disposeCts();
                });
            }

            tcs.Task.ContinueWith(_ =>
            {
                registration.Dispose();
                disposeCts();
            }, TaskScheduler.Default);

            return tcs.Task;
        }

        /// <summary>
        /// 以结果完成等待（收到响应时调用）。
        /// </summary>
        public bool TrySetResult(TKey key, TResult result)
        {
            TaskCompletionSource<TResult> tcs;
            if (!_pending.TryRemove(key, out tcs))
            {
                return false;
            }

            return tcs.TrySetResult(result);
        }

        /// <summary>
        /// 以异常完成等待（收到错误响应/发送失败时调用）。
        /// </summary>
        public bool TrySetException(TKey key, Exception exception)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            TaskCompletionSource<TResult> tcs;
            if (!_pending.TryRemove(key, out tcs))
            {
                return false;
            }

            return tcs.TrySetException(exception);
        }

        /// <summary>
        /// 主动取消一个等待。
        /// </summary>
        public bool TryCancel(TKey key)
        {
            TaskCompletionSource<TResult> tcs;
            if (!_pending.TryRemove(key, out tcs))
            {
                return false;
            }

            return tcs.TrySetCanceled();
        }

        /// <summary>
        /// 取消所有等待（退出时）。
        /// </summary>
        public void CancelAll()
        {
            while (!_pending.IsEmpty)
            {
                foreach (var key in _pending.Keys)
                {
                    TryCancel(key);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            CancelAll();
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                throw new ObjectDisposedException(nameof(RequestAwaiter<TKey, TResult>));
            }
        }
    }
}

