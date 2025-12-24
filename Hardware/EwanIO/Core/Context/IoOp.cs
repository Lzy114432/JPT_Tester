using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IO 操作结果 - 支持 await 和轮询两种使用方式
    /// </summary>
    public class IoOp<TResult>
    {
        private readonly TaskCompletionSource<TResult> _tcs;
        private TResult _result;
        private bool _isCompleted;

        public bool IsCompleted => _isCompleted;

        internal IoOp()
        {
            _tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _result = default!;
            _isCompleted = false;
        }

        /// <summary>
        /// 尝试获取结果（轮询方式）
        /// </summary>
        public bool TryGetResult(out TResult result)
        {
            if (_isCompleted)
            {
                result = _result;
                return true;
            }
            result = default!;
            return false;
        }

        /// <summary>
        /// 设置结果（内部使用）
        /// </summary>
        internal void SetResult(TResult result)
        {
            _result = result;
            _isCompleted = true;
            _tcs.TrySetResult(result);
        }

        /// <summary>
        /// 设置取消（内部使用）
        /// </summary>
        internal void SetCanceled()
        {
            _isCompleted = true;
            _tcs.TrySetCanceled();
        }

        /// <summary>
        /// 获取 awaiter（支持 await）
        /// </summary>
        public TaskAwaiter<TResult> GetAwaiter()
        {
            return _tcs.Task.GetAwaiter();
        }

        /// <summary>
        /// 转换为 Task
        /// </summary>
        public Task<TResult> AsTask() => _tcs.Task;
    }

    /// <summary>
    /// 等待操作的内部状态
    /// </summary>
    internal class WaitOperation
    {
        public IoOp<bool> Op { get; }
        public Func<bool> Condition { get; }
        public bool Expected { get; }
        public DateTime Deadline { get; }
        public CancellationTokenRegistration? CancellationRegistration { get; set; }

        public WaitOperation(IoOp<bool> op, Func<bool> condition, bool expected, TimeSpan timeout)
        {
            Op = op;
            Condition = condition;
            Expected = expected;
            Deadline = timeout == Timeout.InfiniteTimeSpan
                ? DateTime.MaxValue
                : DateTime.UtcNow + timeout;
        }

        public bool IsTimedOut => DateTime.UtcNow >= Deadline;

        public bool CheckCondition()
        {
            try
            {
                return Condition() == Expected;
            }
            catch
            {
                return false;
            }
        }

        public void Complete(bool result)
        {
            CancellationRegistration?.Dispose();
            Op.SetResult(result);
        }

        public void Cancel()
        {
            CancellationRegistration?.Dispose();
            Op.SetCanceled();
        }
    }
}
