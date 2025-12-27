using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 报警消息 - 用于通过 MessageHub 广播报警（由上层统一转换为 AlarmService）
    /// </summary>
    public sealed class AlarmMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 唯一键：用于去重/定位
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 报警内容（显示用）
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// 报警级别
        /// </summary>
        public AlarmLevel Level { get; }

        /// <summary>
        /// 是否需要复位
        /// </summary>
        public bool NeedReset { get; }

        /// <summary>
        /// 报警来源（可选）
        /// </summary>
        public string Unit { get; }

        public AlarmMessage(string key, string content, AlarmLevel level, bool needReset, string unit = null)
        {
            Key = key ?? string.Empty;
            Content = content ?? string.Empty;
            Level = level;
            NeedReset = needReset;
            Unit = unit ?? string.Empty;
        }

        public static AlarmMessage Create(string key, string content, AlarmLevel level, bool needReset, string unit = null)
            => new AlarmMessage(key, content, level, needReset, unit);
    }
}

