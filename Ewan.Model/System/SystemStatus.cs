using System;

namespace Ewan.Model.System
{
    /// <summary>
    /// 系统运行状态枚举
    /// </summary>
    public enum SystemStatus
    {
        /// <summary>
        /// 系统初始化中
        /// </summary>
        Initializing = 0,
        
        /// <summary>
        /// 系统待机 - 绿灯常亮
        /// </summary>
        Standby = 1,
        
        /// <summary>
        /// 系统运行中 - 绿灯闪烁
        /// </summary>
        Running = 2,
        
        /// <summary>
        /// 系统暂停 - 黄灯常亮
        /// </summary>
        Paused = 3,
        
        /// <summary>
        /// 系统警告 - 黄灯闪烁
        /// </summary>
        Warning = 4,
        
        /// <summary>
        /// 系统报警 - 红灯闪烁 + 蜂鸣器
        /// </summary>
        Alarm = 5,
        
        /// <summary>
        /// 系统严重故障 - 红灯常亮 + 蜂鸣器
        /// </summary>
        Critical = 6,
        
        /// <summary>
        /// 系统停止/离线 - 所有灯关闭
        /// </summary>
        Stopped = 7
    }

    /// <summary>
    /// 系统状态变化事件参数
    /// </summary>
    public class SystemStatusChangedEventArgs : EventArgs
    {
        public SystemStatus PreviousStatus { get; set; }
        public SystemStatus CurrentStatus { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; }

        public SystemStatusChangedEventArgs(SystemStatus previous, SystemStatus current, string reason)
        {
            PreviousStatus = previous;
            CurrentStatus = current;
            Reason = reason;
            Timestamp = DateTime.Now;
        }
    }
}