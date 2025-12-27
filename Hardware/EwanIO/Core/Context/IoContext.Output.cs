using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EwanIO.Core.Attributes;
using EwanIO.Core.Data;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext - Output 输出写入操作
    /// </summary>
    public partial class IoContext<TLayout> where TLayout : class, new()
    {
        #region Output（写输出）

        /// <summary>
        /// 立即下发当前 dirty 输出
        /// </summary>
        public void Flush()
        {
            if (_disposed) return;
            lock (_ioLock)
            {
                if (_disposed) return;
                FlushOutputsIfDirty();
            }
        }

        /// <summary>
        /// 快捷设置输出为 ON
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void On(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            On(index, now);
        }

        /// <summary>
        /// 快捷设置输出为 ON（索引）
        /// </summary>
        public void On(int index, bool now = false)
        {
            WriteOutputInternal(index, true, now, nameof(On));
        }

        /// <summary>
        /// 快捷设置输出为 OFF
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void Off(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            Off(index, now);
        }

        /// <summary>
        /// 快捷设置输出为 OFF（索引）
        /// </summary>
        public void Off(int index, bool now = false)
        {
            WriteOutputInternal(index, false, now, nameof(Off));
        }

        /// <summary>
        /// 快捷设置输出物理值为 ON（绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void OnPhysical(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            OnPhysical(index, now);
        }

        /// <summary>
        /// 快捷设置输出物理值为 ON（索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void OnPhysical(int index, bool now = false)
        {
            WriteOutputPhysicalInternal(index, true, now, nameof(OnPhysical));
        }

        /// <summary>
        /// 快捷设置输出物理值为 OFF（绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        public void OffPhysical(Expression<Func<TLayout, OutputSignal>> expr, bool now = false)
        {
            int index = _meta.GetOutputIndex(expr);
            OffPhysical(index, now);
        }

        /// <summary>
        /// 快捷设置输出物理值为 OFF（索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void OffPhysical(int index, bool now = false)
        {
            WriteOutputPhysicalInternal(index, false, now, nameof(OffPhysical));
        }

        /// <summary>
        /// 输出脉冲（毫秒）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationMs">脉冲持续时间（毫秒）</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        public void Pulse(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            int index = _meta.GetOutputIndex(expr);
            Pulse(index, durationMs, now, value, onCompleted);
        }

        /// <summary>
        /// 输出脉冲（毫秒，索引）
        /// </summary>
        /// <param name="index">输出索引</param>
        /// <param name="durationMs">脉冲持续时间（毫秒）</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        public void Pulse(int index, int durationMs, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            if (durationMs <= 0)
                return;
            if (!EnsureOutputIndex(index, nameof(Pulse)))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;

                bool started = _pulseManager.Start(index, durationMs, !value, onCompleted);
                if (!started)
                    return; // 已有脉冲在执行

                SetOutputInternal(index, value);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        /// <summary>
        /// 输出脉冲（TimeSpan）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="duration">脉冲持续时间</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        public void Pulse(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            int index = _meta.GetOutputIndex(expr);
            Pulse(index, duration, now, value, onCompleted);
        }

        /// <summary>
        /// 输出脉冲（TimeSpan，索引）
        /// </summary>
        /// <param name="index">输出索引</param>
        /// <param name="duration">脉冲持续时间</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        public void Pulse(int index, TimeSpan duration, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            Pulse(index, (int)duration.TotalMilliseconds, now, value, onCompleted);
        }

        /// <summary>
        /// 输出物理值脉冲（毫秒，绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationMs">脉冲持续时间（毫秒）</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲物理值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="onCompleted">脉冲完成回调（可选）</param>
        public void PulsePhysical(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            int index = _meta.GetOutputIndex(expr);
            PulsePhysical(index, durationMs, now, value, onCompleted);
        }

        /// <summary>
        /// 输出物理值脉冲（毫秒，索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void PulsePhysical(int index, int durationMs, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            if (durationMs <= 0)
                return;
            if (!EnsureOutputIndex(index, nameof(PulsePhysical)))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;

                bool isNc = _mapping.IsOutputNormallyClosed(index);
                bool logicalStart = value ^ isNc;
                bool logicalEnd = (!value) ^ isNc;

                bool started = _pulseManager.Start(index, durationMs, logicalEnd, onCompleted);
                if (!started)
                    return;

                SetOutputInternal(index, logicalStart);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        /// <summary>
        /// 输出物理值脉冲（TimeSpan，绕过 NO/NC 映射影响）
        /// </summary>
        public void PulsePhysical(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            int index = _meta.GetOutputIndex(expr);
            PulsePhysical(index, duration, now, value, onCompleted);
        }

        /// <summary>
        /// 输出物理值脉冲（TimeSpan，索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void PulsePhysical(int index, TimeSpan duration, bool now = false, bool value = true, Action<int>? onCompleted = null)
        {
            PulsePhysical(index, (int)duration.TotalMilliseconds, now, value, onCompleted);
        }

        /// <summary>
        /// 检查指定输出是否有脉冲正在执行
        /// </summary>
        public bool IsPulseActive(int index)
        {
            return _pulseManager.IsPulseActive(index);
        }

        /// <summary>
        /// 检查指定输出是否有脉冲正在执行
        /// </summary>
        public bool IsPulseActive(Expression<Func<TLayout, OutputSignal>> expr)
        {
            int index = _meta.GetOutputIndex(expr);
            return _pulseManager.IsPulseActive(index);
        }

        /// <summary>
        /// 获取脉冲剩余时间（毫秒）
        /// </summary>
        public long GetPulseRemainingMs(int index)
        {
            return _pulseManager.GetRemainingMs(index);
        }

        /// <summary>
        /// 取消脉冲（不改变输出值）
        /// </summary>
        public void CancelPulse(int index)
        {
            _pulseManager.Cancel(index);
        }

        /// <summary>
        /// 取消脉冲（不改变输出值）
        /// </summary>
        public void CancelPulse(Expression<Func<TLayout, OutputSignal>> expr)
        {
            int index = _meta.GetOutputIndex(expr);
            _pulseManager.Cancel(index);
        }

        #endregion

        #region Pulse 等待方法

        /// <summary>
        /// 异步等待脉冲完成
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationMs">脉冲持续时间（毫秒）</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功完成（false 表示被取消或已有脉冲在执行）</returns>
        public Task<bool> PulseAsync(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, CancellationToken cancellationToken = default)
        {
            int index = _meta.GetOutputIndex(expr);
            return PulseAsync(index, durationMs, now, value, cancellationToken);
        }

        /// <summary>
        /// 异步等待脉冲完成（索引）
        /// </summary>
        public Task<bool> PulseAsync(int index, int durationMs, bool now = false, bool value = true, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 注册取消
            CancellationTokenRegistration registration = default;
            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    CancelPulse(index);
                    tcs.TrySetResult(false);
                });
            }

            // 启动脉冲
            Pulse(index, durationMs, now, value, onCompleted: _ =>
            {
                registration.Dispose();
                tcs.TrySetResult(true);
            });

            // 检查是否启动成功（如果已有脉冲在执行，Pulse 不会启动新的）
            if (!IsPulseActive(index))
            {
                registration.Dispose();
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        /// <summary>
        /// 异步等待脉冲完成（TimeSpan）
        /// </summary>
        public Task<bool> PulseAsync(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, CancellationToken cancellationToken = default)
        {
            return PulseAsync(expr, (int)duration.TotalMilliseconds, now, value, cancellationToken);
        }

        /// <summary>
        /// 异步等待脉冲完成（TimeSpan，索引）
        /// </summary>
        public Task<bool> PulseAsync(int index, TimeSpan duration, bool now = false, bool value = true, CancellationToken cancellationToken = default)
        {
            return PulseAsync(index, (int)duration.TotalMilliseconds, now, value, cancellationToken);
        }

        /// <summary>
        /// 同步等待脉冲完成（阻塞当前线程）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationMs">脉冲持续时间（毫秒）</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        /// <param name="timeoutMs">等待超时（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功完成（false 表示超时、被取消或已有脉冲在执行）</returns>
        public bool PulseAndWait(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, int timeoutMs = -1)
        {
            int index = _meta.GetOutputIndex(expr);
            return PulseAndWait(index, durationMs, now, value, timeoutMs);
        }

        /// <summary>
        /// 同步等待脉冲完成（索引，阻塞当前线程）
        /// </summary>
        public bool PulseAndWait(int index, int durationMs, bool now = false, bool value = true, int timeoutMs = -1)
        {
            using (var mre = new ManualResetEventSlim(false))
            {
                bool completed = false;

                // 启动脉冲
                Pulse(index, durationMs, now, value, onCompleted: _ =>
                {
                    completed = true;
                    mre.Set();
                });

                // 检查是否启动成功
                if (!IsPulseActive(index))
                {
                    return false;
                }

                // 计算实际超时时间（至少要等脉冲时间 + 一些余量）
                int actualTimeout = timeoutMs < 0 ? -1 : Math.Max(timeoutMs, durationMs + 100);

                // 等待完成
                if (mre.Wait(actualTimeout))
                {
                    return completed;
                }

                // 超时，取消脉冲
                CancelPulse(index);
                return false;
            }
        }

        /// <summary>
        /// 同步等待脉冲完成（TimeSpan）
        /// </summary>
        public bool PulseAndWait(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, int timeoutMs = -1)
        {
            return PulseAndWait(expr, (int)duration.TotalMilliseconds, now, value, timeoutMs);
        }

        /// <summary>
        /// 同步等待脉冲完成（TimeSpan，索引）
        /// </summary>
        public bool PulseAndWait(int index, TimeSpan duration, bool now = false, bool value = true, int timeoutMs = -1)
        {
            return PulseAndWait(index, (int)duration.TotalMilliseconds, now, value, timeoutMs);
        }

        #endregion

        private void WriteOutputInternal(int index, bool value, bool now, string caller)
        {
            if (!EnsureOutputIndex(index, caller))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;

                // 取消正在执行的脉冲（如果有）
                _pulseManager.Cancel(index);
                SetOutputInternal(index, value);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        private void WriteOutputPhysicalInternal(int index, bool physicalValue, bool now, string caller)
        {
            if (!EnsureOutputIndex(index, caller))
                return;

            bool logicalValue = physicalValue ^ _mapping.IsOutputNormallyClosed(index);

            lock (_ioLock)
            {
                if (_disposed) return;

                // 取消正在执行的脉冲（如果有）
                _pulseManager.Cancel(index);
                SetOutputInternal(index, logicalValue);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        private void SetOutputInternal(int index, bool value)
        {
            if (_useBulkWrite)
            {
                ((CommandOptimized)_command).SetOutput(index, value);
            }
            else
            {
                ((Command)_command).SetOutput(index, value);
            }
        }
    }
}
