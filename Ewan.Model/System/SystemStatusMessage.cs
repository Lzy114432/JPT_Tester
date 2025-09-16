using System;
using Ewan.Model.Production;

namespace Ewan.Model.System
{
    /// <summary>
    /// 系统状态变化消息类型
    /// </summary>
    public enum SystemStatusChangeType
    {
        /// <summary>
        /// 系统启动状态变化
        /// </summary>
        SystemStarted,
        
        /// <summary>
        /// 系统模式变化
        /// </summary>
        SystemModeChanged,
        
        /// <summary>
        /// 系统停止
        /// </summary>
        SystemStopped
    }

    /// <summary>
    /// 系统状态变化消息
    /// </summary>
    public class SystemStatusMessage
    {
        /// <summary>
        /// 变化类型
        /// </summary>
        public SystemStatusChangeType ChangeType { get; set; }
        
        /// <summary>
        /// 系统启动状态（当ChangeType为SystemStarted或SystemStopped时有效）
        /// </summary>
        public bool IsStarted { get; set; }
        
        /// <summary>
        /// 系统模式（当ChangeType为SystemModeChanged时有效）
        /// </summary>
        public SystemMode SystemMode { get; set; }
        
        /// <summary>
        /// 旧状态
        /// </summary>
        public SystemStatus OldStatus { get; set; }
        
        /// <summary>
        /// 新状态
        /// </summary>
        public SystemStatus NewStatus { get; set; }
        
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// 消息描述
        /// </summary>
        public string Description { get; set; }

        public SystemStatusMessage()
        {
            Timestamp = DateTime.Now;
        }

        public SystemStatusMessage(SystemStatusChangeType changeType, string description = "")
        {
            ChangeType = changeType;
            Description = description;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 创建系统启动状态变化消息
        /// </summary>
        /// <param name="started">是否启动</param>
        /// <param name="description">描述</param>
        /// <returns>消息实例</returns>
        public static SystemStatusMessage CreateStartedMessage(bool started, string description = "")
        {
            return new SystemStatusMessage(started ? SystemStatusChangeType.SystemStarted : SystemStatusChangeType.SystemStopped)
            {
                IsStarted = started,
                Description = description
            };
        }

        /// <summary>
        /// 创建系统模式变化消息
        /// </summary>
        /// <param name="mode">新模式</param>
        /// <param name="description">描述</param>
        /// <returns>消息实例</returns>
        public static SystemStatusMessage CreateModeChangedMessage(SystemMode mode, string description = "")
        {
            return new SystemStatusMessage(SystemStatusChangeType.SystemModeChanged)
            {
                SystemMode = mode,
                Description = description
            };
        }
    }
}