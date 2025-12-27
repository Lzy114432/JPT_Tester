using System;
using EwanCore.Messaging;

namespace Ewan.Model.System
{
    /// <summary>
    /// 系统状态指示器控制命令
    /// </summary>
    public class StatusIndicatorCommand : IMessage
    {
        /// <summary>
        /// 系统状态
        /// </summary>
        public SystemStatus Status { get; set; }

        /// <summary>
        /// 是否严重报警（仅在Alarm状态下有效）
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 消息时间戳（IMessage 接口）
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        public StatusIndicatorCommand()
        {
            Description = string.Empty;
        }

        public StatusIndicatorCommand(SystemStatus status, string description = "", bool isCritical = false)
        {
            Status = status;
            Description = description ?? string.Empty;
            IsCritical = isCritical;
        }
    }
}