using System;

namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// 报警变更事件参数。
    /// </summary>
    public sealed class AlarmChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 变更类型。
        /// </summary>
        public AlarmChangeKind Kind { get; }

        /// <summary>
        /// 变更的报警对象。
        /// 当 <see cref="Kind"/> 为 <see cref="AlarmChangeKind.Cleared"/> 时可能为 null。
        /// </summary>
        public Alarm Alarm { get; }

        /// <summary>
        /// 创建事件参数。
        /// </summary>
        /// <param name="kind">变更类型。</param>
        /// <param name="alarm">报警对象（可为空）。</param>
        public AlarmChangedEventArgs(AlarmChangeKind kind, Alarm alarm)
        {
            Kind = kind;
            Alarm = alarm;
        }
    }
}
