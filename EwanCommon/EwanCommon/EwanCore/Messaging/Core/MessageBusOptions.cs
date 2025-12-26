namespace EwanCore.Messaging
{
    /// <summary>
    /// <see cref="MessageBus"/> 配置项。
    /// </summary>
    public sealed class MessageBusOptions
    {
        /// <summary>
        /// 异步队列容量（小于等于 0 会抛异常）。
        /// </summary>
        public int AsyncQueueCapacity { get; set; } = 1024;

        /// <summary>
        /// 队列满时的处理策略。
        /// </summary>
        public MessageOverflowStrategy OverflowStrategy { get; set; } = MessageOverflowStrategy.DropOldest;

        /// <summary>
        /// 是否捕获单个处理器异常并继续调用其它处理器。
        /// </summary>
        public bool CatchHandlerExceptions { get; set; } = true;
    }
}
