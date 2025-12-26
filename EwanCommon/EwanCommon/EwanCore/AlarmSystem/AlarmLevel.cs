using EwanModel.Common;

namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// 报警级别别名：直接复用 <see cref="EAlarmLevel"/>，保留一个更贴近“报警系统”的命名入口。
    /// </summary>
    public enum AlarmLevel
    {
        /// <inheritdoc cref="EAlarmLevel.H"/>
        H = EAlarmLevel.H,

        /// <inheritdoc cref="EAlarmLevel.M"/>
        M = EAlarmLevel.M,

        /// <inheritdoc cref="EAlarmLevel.L"/>
        L = EAlarmLevel.L
    }
}

