using System;
using System.Threading;

namespace EwanCore.Messaging
{
    /// <summary>
    /// 进程级消息总线入口（可由项目在 Composition Root 替换为自己的实例）。
    /// </summary>
    public static class MessageHub
    {
        private static IMessageBus s_current = MessageBus.Default;

        /// <summary>
        /// 当前进程使用的消息总线实例。
        /// </summary>
        public static IMessageBus Current
        {
            get => Volatile.Read(ref s_current);
            set => Volatile.Write(ref s_current, value ?? throw new ArgumentNullException(nameof(value)));
        }

        /// <summary>
        /// 仅发布接口（Publish/Post）。
        /// </summary>
        public static IPublishBus PublishBus => Current;

        /// <summary>
        /// 仅订阅接口（Subscribe/SubscribeWeak/SubscribeAll）。
        /// </summary>
        public static ISubscribeBus SubscribeBus => Current;

        /// <summary>
        /// 仅 Request/Reply 接口。
        /// </summary>
        public static IRequestReplyBus RequestReplyBus => Current;

        /// <summary>
        /// 诊断接口（队列/订阅/统计）。
        /// </summary>
        public static IMessageBusDiagnostics Diagnostics =>
            Current as IMessageBusDiagnostics
            ?? throw new InvalidOperationException($"{nameof(Current)} does not implement {nameof(IMessageBusDiagnostics)}.");
    }
}
