using System;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 强类型消息总线：支持同步发布（Publish）与异步排队（Post）。
    /// </summary>
    public interface IMessageBus : IPublishBus, ISubscribeBus, IRequestReplyBus, IDisposable
    {
    }
}
