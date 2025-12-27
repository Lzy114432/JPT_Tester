using EwanCore.Messaging;
using Ewan.Model.System;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 系统控制消息 - 用于系统启动/停止/暂停/急停等控制命令
    /// </summary>
    public sealed class SystemControlMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 控制命令
        /// </summary>
        public SystemControlCommand Command { get; }

        /// <summary>
        /// 命令来源（可选，用于追踪命令发起者）
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 命令原因（可选，用于记录触发原因）
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 创建系统控制消息
        /// </summary>
        /// <param name="command">控制命令</param>
        /// <param name="source">命令来源</param>
        /// <param name="reason">命令原因</param>
        public SystemControlMessage(SystemControlCommand command, string source = null, string reason = null)
        {
            Command = command;
            Source = source ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        /// <summary>
        /// 创建初始化命令
        /// </summary>
        public static SystemControlMessage Initialize(string source = null, string reason = null)
            => new SystemControlMessage(SystemControlCommand.Initialize, source, reason);

        /// <summary>
        /// 创建启动命令
        /// </summary>
        public static SystemControlMessage Start(string source = null, string reason = null)
            => new SystemControlMessage(SystemControlCommand.Start, source, reason);

        /// <summary>
        /// 创建停止命令
        /// </summary>
        public static SystemControlMessage Stop(string source = null, string reason = null)
            => new SystemControlMessage(SystemControlCommand.Stop, source, reason);

        /// <summary>
        /// 创建紧急停止命令
        /// </summary>
        public static SystemControlMessage EmergencyStop(string source = null, string reason = null)
            => new SystemControlMessage(SystemControlCommand.EmergencyStop, source, reason);

        /// <summary>
        /// 创建暂停命令
        /// </summary>
        public static SystemControlMessage Pause(string source = null, string reason = null)
            => new SystemControlMessage(SystemControlCommand.Pause, source, reason);

        /// <summary>
        /// 创建恢复命令
        /// </summary>
        public static SystemControlMessage Resume(string source = null, string reason = null)
            => new SystemControlMessage(SystemControlCommand.Resume, source, reason);
    }
}
