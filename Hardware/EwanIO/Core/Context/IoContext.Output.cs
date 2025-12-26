using System;
using System.Linq.Expressions;
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
        /// 输出脉冲（按 Tick 周期计数）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationTicks">脉冲持续 Tick 数</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲值（true=ON->OFF，false=OFF->ON）</param>
        public void Pulse(Expression<Func<TLayout, OutputSignal>> expr, int durationTicks, bool now = false, bool value = true)
        {
            int index = _meta.GetOutputIndex(expr);
            Pulse(index, durationTicks, now, value);
        }

        /// <summary>
        /// 输出脉冲（索引）
        /// </summary>
        public void Pulse(int index, int durationTicks, bool now = false, bool value = true)
        {
            if (durationTicks <= 0)
                return;
            if (!EnsureOutputIndex(index, nameof(Pulse)))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;
                if (_pulseRemainingTicks[index] > 0)
                    return;

                _pulseRemainingTicks[index] = durationTicks;
                _pulseEndValues[index] = !value;

                SetOutputInternal(index, value);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        /// <summary>
        /// 输出物理值脉冲（按 Tick 周期计数，绕过 NO/NC 映射影响）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <param name="durationTicks">脉冲持续 Tick 数</param>
        /// <param name="now">是否立即下发（默认等 Tick）</param>
        /// <param name="value">脉冲物理值（true=ON->OFF，false=OFF->ON）</param>
        public void PulsePhysical(Expression<Func<TLayout, OutputSignal>> expr, int durationTicks, bool now = false, bool value = true)
        {
            int index = _meta.GetOutputIndex(expr);
            PulsePhysical(index, durationTicks, now, value);
        }

        /// <summary>
        /// 输出物理值脉冲（索引，绕过 NO/NC 映射影响）
        /// </summary>
        public void PulsePhysical(int index, int durationTicks, bool now = false, bool value = true)
        {
            if (durationTicks <= 0)
                return;
            if (!EnsureOutputIndex(index, nameof(PulsePhysical)))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;
                if (_pulseRemainingTicks[index] > 0)
                    return;

                bool isNc = _mapping.IsOutputNormallyClosed(index);
                bool logicalStart = value ^ isNc;
                bool logicalEnd = (!value) ^ isNc;

                _pulseRemainingTicks[index] = durationTicks;
                _pulseEndValues[index] = logicalEnd;

                SetOutputInternal(index, logicalStart);

                if (now)
                {
                    FlushOutputsIfDirty();
                }
            }
        }

        private void WriteOutputInternal(int index, bool value, bool now, string caller)
        {
            if (!EnsureOutputIndex(index, caller))
                return;

            lock (_ioLock)
            {
                if (_disposed) return;

                _pulseRemainingTicks[index] = 0;
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

                _pulseRemainingTicks[index] = 0;
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

        #endregion
    }
}
