using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 消息基类：提供 <see cref="IMessage.Timestamp"/> 的默认实现。
    /// </summary>
    public abstract record MessageBase : IMessage
    {
        public DateTimeOffset Timestamp { get; set; }
    }
}
