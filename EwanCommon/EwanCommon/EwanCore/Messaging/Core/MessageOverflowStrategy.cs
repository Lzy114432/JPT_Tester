namespace EwanCore.Messaging
{
    /// <summary>
    /// 异步队列满时的处理策略。
    /// </summary>
    public enum MessageOverflowStrategy
    {
        /// <summary>
        /// 丢弃最新消息（不入队）。
        /// </summary>
        DropNewest,

        /// <summary>
        /// 丢弃最旧消息（从队列头移除一条），再尝试把新消息入队。
        /// </summary>
        DropOldest,

        /// <summary>
        /// 阻塞等待队列空位（可能导致发布方线程阻塞）。
        /// </summary>
        Block,
    }
}
