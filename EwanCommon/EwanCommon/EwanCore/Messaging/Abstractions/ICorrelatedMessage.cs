namespace EwanCore.Messaging
{
    /// <summary>
    /// 带关联 ID 的消息（用于 Request/Reply 场景）。
    /// </summary>
    /// <typeparam name="TKey">关联键类型（建议 Guid/string）。</typeparam>
    public interface ICorrelatedMessage<TKey> : IMessage
    {
        TKey CorrelationId { get; set; }
    }
}
