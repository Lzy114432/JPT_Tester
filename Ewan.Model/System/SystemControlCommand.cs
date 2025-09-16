namespace Ewan.Model.System
{
    /// <summary>
    /// 系统控制命令枚举
    /// </summary>
    public enum SystemControlCommand
    {
        /// <summary>
        /// 启动系统
        /// </summary>
        Start,

        /// <summary>
        /// 停止系统
        /// </summary>
        Stop,

        /// <summary>
        /// 紧急停止
        /// </summary>
        EmergencyStop,

        /// <summary>
        /// 暂停系统
        /// </summary>
        Pause,

        /// <summary>
        /// 恢复系统
        /// </summary>
        Resume
    }
}