namespace EwanCore.Messaging
{
    /// <summary>
    /// Publish/Post 语义的消息总线接口。
    /// </summary>
    public interface IPublishBus
    {
        /// <summary>
        /// 同步发布：在调用线程中依次调用订阅者。
        /// </summary>
        void Publish<TMessage>(TMessage message) where TMessage : IMessage;

        /// <summary>
        /// 异步发布：消息进入队列，由后台线程按序分发给订阅者。
        /// </summary>
        /// <returns>是否成功进入队列（队列满时可能返回 false）。</returns>
        bool Post<TMessage>(TMessage message) where TMessage : IMessage;
    }
}
