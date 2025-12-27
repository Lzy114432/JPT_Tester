using System;
using System.Diagnostics;
using System.Threading;

namespace EwanIO.Core.Data
{
    /// <summary>
    /// 脉冲操作 - 使用绝对时间计时
    /// 特点：
    /// - 使用 Stopwatch 实现精确计时，不受 Tick 周期影响
    /// - 支持毫秒和 TimeSpan 两种 API
    /// - 支持脉冲完成回调
    /// </summary>
    public class PulseOperation
    {
        #region 字段

        private readonly Stopwatch _stopwatch;
        private long _durationMs;
        private bool _endValue;
        private Action<int>? _onCompleted;
        private int _isActive;  // 0 = inactive, 1 = active (使用 int 支持 Interlocked)

        #endregion

        #region 属性

        /// <summary>
        /// 输出索引
        /// </summary>
        public int OutputIndex { get; }

        /// <summary>
        /// 脉冲持续时间（毫秒）
        /// </summary>
        public long DurationMs => Volatile.Read(ref _durationMs);

        /// <summary>
        /// 脉冲结束后的输出值
        /// </summary>
        public bool EndValue => _endValue;

        /// <summary>
        /// 是否活跃（正在执行脉冲）
        /// </summary>
        public bool IsActive => Volatile.Read(ref _isActive) == 1;

        /// <summary>
        /// 已经过的时间（毫秒）
        /// </summary>
        public long ElapsedMs => _stopwatch.ElapsedMilliseconds;

        /// <summary>
        /// 剩余时间（毫秒）
        /// </summary>
        public long RemainingMs => Math.Max(0, DurationMs - ElapsedMs);

        /// <summary>
        /// 是否已完成（时间已到期）
        /// </summary>
        public bool IsExpired => IsActive && ElapsedMs >= DurationMs;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建脉冲操作
        /// </summary>
        /// <param name="outputIndex">输出索引</param>
        public PulseOperation(int outputIndex)
        {
            OutputIndex = outputIndex;
            _stopwatch = new Stopwatch();
            _durationMs = 0;
            _endValue = false;
            _onCompleted = null;
            _isActive = 0;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 启动脉冲
        /// </summary>
        /// <param name="durationMs">持续时间（毫秒）</param>
        /// <param name="endValue">脉冲结束后的输出值</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        /// <returns>是否成功启动（如果已有脉冲在执行则返回 false）</returns>
        public bool Start(long durationMs, bool endValue, Action<int>? onCompleted = null)
        {
            // 原子检查并设置活跃状态
            if (Interlocked.CompareExchange(ref _isActive, 1, 0) != 0)
            {
                return false; // 已有脉冲在执行
            }

            _durationMs = durationMs;
            _endValue = endValue;
            _onCompleted = onCompleted;
            _stopwatch.Restart();

            return true;
        }

        /// <summary>
        /// 启动脉冲（TimeSpan 重载）
        /// </summary>
        public bool Start(TimeSpan duration, bool endValue, Action<int>? onCompleted = null)
        {
            return Start((long)duration.TotalMilliseconds, endValue, onCompleted);
        }

        /// <summary>
        /// 强制启动脉冲（覆盖正在执行的脉冲）
        /// </summary>
        /// <param name="durationMs">持续时间（毫秒）</param>
        /// <param name="endValue">脉冲结束后的输出值</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        public void ForceStart(long durationMs, bool endValue, Action<int>? onCompleted = null)
        {
            Volatile.Write(ref _isActive, 1);
            _durationMs = durationMs;
            _endValue = endValue;
            _onCompleted = onCompleted;
            _stopwatch.Restart();
        }

        /// <summary>
        /// 检查脉冲是否完成，如果完成则触发回调并重置状态
        /// </summary>
        /// <param name="completedEndValue">如果完成，输出脉冲结束值</param>
        /// <returns>是否刚刚完成</returns>
        public bool TryComplete(out bool completedEndValue)
        {
            completedEndValue = false;

            if (!IsActive)
                return false;

            if (!IsExpired)
                return false;

            // 原子设置为非活跃
            if (Interlocked.CompareExchange(ref _isActive, 0, 1) != 1)
                return false;

            _stopwatch.Stop();
            completedEndValue = _endValue;

            // 触发回调
            var callback = _onCompleted;
            _onCompleted = null;

            try
            {
                callback?.Invoke(OutputIndex);
            }
            catch
            {
                // 忽略回调异常，防止影响 Tick 循环
            }

            return true;
        }

        /// <summary>
        /// 取消脉冲（不触发回调，不改变输出值）
        /// </summary>
        public void Cancel()
        {
            if (Interlocked.CompareExchange(ref _isActive, 0, 1) == 1)
            {
                _stopwatch.Stop();
                _onCompleted = null;
            }
        }

        /// <summary>
        /// 重置脉冲状态
        /// </summary>
        public void Reset()
        {
            Volatile.Write(ref _isActive, 0);
            _stopwatch.Reset();
            _durationMs = 0;
            _endValue = false;
            _onCompleted = null;
        }

        #endregion
    }

    /// <summary>
    /// 脉冲管理器 - 管理多个输出的脉冲操作
    /// </summary>
    public class PulseManager
    {
        private readonly PulseOperation[] _operations;
        private int _activeCount;

        /// <summary>
        /// 输出数量
        /// </summary>
        public int OutputCount { get; }

        /// <summary>
        /// 活跃脉冲数量
        /// </summary>
        public int ActiveCount => Volatile.Read(ref _activeCount);

        /// <summary>
        /// 是否有活跃脉冲
        /// </summary>
        public bool HasActivePulse => ActiveCount > 0;

        /// <summary>
        /// 创建脉冲管理器
        /// </summary>
        /// <param name="outputCount">输出数量</param>
        public PulseManager(int outputCount)
        {
            OutputCount = outputCount;
            _operations = new PulseOperation[outputCount];
            _activeCount = 0;

            for (int i = 0; i < outputCount; i++)
            {
                _operations[i] = new PulseOperation(i);
            }
        }

        /// <summary>
        /// 获取指定输出的脉冲操作
        /// </summary>
        public PulseOperation this[int index]
        {
            get
            {
                if ((uint)index >= (uint)OutputCount)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _operations[index];
            }
        }

        /// <summary>
        /// 启动脉冲
        /// </summary>
        /// <param name="index">输出索引</param>
        /// <param name="durationMs">持续时间（毫秒）</param>
        /// <param name="endValue">脉冲结束后的输出值</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        /// <returns>是否成功启动</returns>
        public bool Start(int index, long durationMs, bool endValue, Action<int>? onCompleted = null)
        {
            if ((uint)index >= (uint)OutputCount)
                return false;

            bool started = _operations[index].Start(durationMs, endValue, onCompleted);
            if (started)
            {
                Interlocked.Increment(ref _activeCount);
            }
            return started;
        }

        /// <summary>
        /// 启动脉冲（TimeSpan 重载）
        /// </summary>
        public bool Start(int index, TimeSpan duration, bool endValue, Action<int>? onCompleted = null)
        {
            return Start(index, (long)duration.TotalMilliseconds, endValue, onCompleted);
        }

        /// <summary>
        /// 强制启动脉冲
        /// </summary>
        public void ForceStart(int index, long durationMs, bool endValue, Action<int>? onCompleted = null)
        {
            if ((uint)index >= (uint)OutputCount)
                return;

            bool wasActive = _operations[index].IsActive;
            _operations[index].ForceStart(durationMs, endValue, onCompleted);

            if (!wasActive)
            {
                Interlocked.Increment(ref _activeCount);
            }
        }

        /// <summary>
        /// 检查是否有脉冲活跃
        /// </summary>
        public bool IsPulseActive(int index)
        {
            if ((uint)index >= (uint)OutputCount)
                return false;
            return _operations[index].IsActive;
        }

        /// <summary>
        /// 获取剩余时间（毫秒）
        /// </summary>
        public long GetRemainingMs(int index)
        {
            if ((uint)index >= (uint)OutputCount)
                return 0;
            return _operations[index].RemainingMs;
        }

        /// <summary>
        /// 取消脉冲
        /// </summary>
        public void Cancel(int index)
        {
            if ((uint)index >= (uint)OutputCount)
                return;

            if (_operations[index].IsActive)
            {
                _operations[index].Cancel();
                Interlocked.Decrement(ref _activeCount);
            }
        }

        /// <summary>
        /// 取消所有脉冲
        /// </summary>
        public void CancelAll()
        {
            for (int i = 0; i < OutputCount; i++)
            {
                if (_operations[i].IsActive)
                {
                    _operations[i].Cancel();
                }
            }
            Volatile.Write(ref _activeCount, 0);
        }

        /// <summary>
        /// 更新所有脉冲状态（在 Tick 中调用）
        /// </summary>
        /// <param name="setOutput">设置输出值的委托</param>
        public void Update(Action<int, bool> setOutput)
        {
            // 快路径：无活跃脉冲
            if (Volatile.Read(ref _activeCount) == 0)
                return;

            for (int i = 0; i < OutputCount; i++)
            {
                if (_operations[i].TryComplete(out bool endValue))
                {
                    setOutput(i, endValue);
                    Interlocked.Decrement(ref _activeCount);
                }
            }
        }
    }
}
