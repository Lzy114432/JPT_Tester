using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 强类型消息接口。
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// 消息时间戳（建议使用本地时间 <see cref="DateTimeOffset.Now"/>）。
        /// </summary>
        /// <remarks>
        /// - 若发送方未赋值（默认值），<see cref="MessageBus"/> 会在 Publish/Post 时自动补齐。
        /// - 业务上如需区分“采集时间/发生时间/入队时间”，请在消息类型中自行扩展字段。
        /// </remarks>
        DateTimeOffset Timestamp { get; set; }
    }
}
