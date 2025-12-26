using System;
using System.Threading;
using System.Threading.Tasks;

namespace EwanCore.Messaging
{
    /// <summary>
    /// Request/Reply 语义的消息总线接口。
    /// </summary>
    public interface IRequestReplyBus
    {
        /// <summary>
        /// 内置 Request/Reply：发送 <typeparamref name="TRequest"/> 并等待 <typeparamref name="TReply"/>。
        /// </summary>
        /// <remarks>
        /// - 使用 <see cref="ICorrelatedMessage{TKey}"/> 的 <see cref="ICorrelatedMessage{TKey}.CorrelationId"/> 做关联。
        /// - 若请求的 CorrelationId 为空（Guid.Empty），总线会自动生成。
        /// </remarks>
        Task<TReply> RequestAsync<TRequest, TReply>(
            TRequest request,
            int timeoutMs = 30000,
            CancellationToken cancellationToken = default,
            bool postRequest = true)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>;

        /// <summary>
        /// 注册请求处理器：收到 <typeparamref name="TRequest"/> 后生成并发送 <typeparamref name="TReply"/>。
        /// </summary>
        IDisposable Respond<TRequest, TReply>(
            Func<TRequest, TReply> handler,
            bool postReply = true)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>;

        /// <summary>
        /// 注册异步请求处理器：收到请求后异步生成响应并发送。
        /// </summary>
        IDisposable RespondAsync<TRequest, TReply>(
            Func<TRequest, Task<TReply>> handler,
            bool postReply = true)
            where TRequest : IMessage, ICorrelatedMessage<Guid>
            where TReply : IMessage, ICorrelatedMessage<Guid>;
    }
}
