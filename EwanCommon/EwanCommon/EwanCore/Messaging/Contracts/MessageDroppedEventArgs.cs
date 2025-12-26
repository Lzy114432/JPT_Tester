using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 异步队列溢出导致消息被丢弃的事件参数。
    /// </summary>
    public sealed class MessageDroppedEventArgs : EventArgs
    {
        public MessageOverflowStrategy Strategy { get; }
        public IMessage Message { get; }

        public MessageDroppedEventArgs(MessageOverflowStrategy strategy, IMessage message)
        {
            Strategy = strategy;
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }
    }
}
