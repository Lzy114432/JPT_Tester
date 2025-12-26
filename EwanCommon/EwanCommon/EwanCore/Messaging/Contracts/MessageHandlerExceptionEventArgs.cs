using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 消息处理器执行异常事件参数。
    /// </summary>
    public sealed class MessageHandlerExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public IMessage Message { get; }
        public Delegate Handler { get; }

        public MessageHandlerExceptionEventArgs(Exception exception, IMessage message, Delegate handler)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }
    }
}
