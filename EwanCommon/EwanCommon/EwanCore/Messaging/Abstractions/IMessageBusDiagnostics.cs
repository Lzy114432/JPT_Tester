using System;
using System.Collections.Generic;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 消息总线诊断接口（队列、订阅、统计）。
    /// </summary>
    public interface IMessageBusDiagnostics
    {
        /// <summary>
        /// 当前异步队列长度（近似值）。
        /// </summary>
        int QueueLength { get; }

        /// <summary>
        /// 获取指定消息类型的订阅者数量。
        /// </summary>
        int GetSubscriberCount<TMessage>() where TMessage : IMessage;

        /// <summary>
        /// 获取指定消息类型的订阅者数量。
        /// </summary>
        int GetSubscriberCount(Type messageType);

        /// <summary>
        /// 累计发布（Publish/Post 成功入队）消息数。
        /// </summary>
        long TotalPublished { get; }

        /// <summary>
        /// 累计丢弃消息数（队列溢出）。
        /// </summary>
        long TotalDropped { get; }

        /// <summary>
        /// 累计处理器异常数。
        /// </summary>
        long TotalHandlerExceptions { get; }

        /// <summary>
        /// 获取所有已订阅的消息类型（不含订阅数为 0 的类型）。
        /// </summary>
        IReadOnlyList<Type> SubscribedTypes { get; }

        /// <summary>
        /// 重置统计计数（Published/Dropped/HandlerExceptions）。
        /// </summary>
        void ResetStatistics();
    }
}
