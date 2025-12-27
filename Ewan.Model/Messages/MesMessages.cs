using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// MES 环线操作类型枚举
    /// </summary>
    public enum MesRingLineAction
    {
        /// <summary>前料仓上料</summary>
        FeedingQianLiaocang = 1,
        /// <summary>前料仓上料成功</summary>
        FeedingQianLiaocangSuccess = 2,
        /// <summary>前料仓下料</summary>
        UnloadingQianLiaocang = 3,
        /// <summary>中料仓上料</summary>
        FeedingZhongLiaocang = 4,
        /// <summary>中料仓下料</summary>
        UnloadingZhongLiaocang = 5,
        /// <summary>清洗烘干机上料</summary>
        FeedingQingxihongganji = 6,
        /// <summary>后料仓上料</summary>
        FeedingHouLiaocang = 7,
        /// <summary>后料仓下料</summary>
        UnloadingHouLiaocang = 8
    }

    /// <summary>
    /// MES 环线请求消息
    /// </summary>
    public class MesRingLineRequest : IMessage, ICorrelatedMessage<Guid>
    {
        /// <summary>
        /// 关联ID，用于匹配请求和响应
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// 环线操作类型
        /// </summary>
        public MesRingLineAction Action { get; set; }

        /// <summary>
        /// 料片编码
        /// </summary>
        public string PlateCode { get; set; }

        /// <summary>
        /// WIP单号
        /// </summary>
        public string BillNoWip { get; set; }

        /// <summary>
        /// 上料料框编码
        /// </summary>
        public string FeedingLiaokuangCode { get; set; }

        /// <summary>
        /// 等待 MES 反馈的超时毫秒；小于等于 0 时使用默认值。
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    }

    /// <summary>
    /// MES 环线反馈消息
    /// </summary>
    public class MesRingLineFeedback : IMessage, ICorrelatedMessage<Guid>
    {
        /// <summary>
        /// 关联ID，用于匹配请求和响应
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// 环线操作类型
        /// </summary>
        public MesRingLineAction Action { get; set; }

        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 反馈消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 发布上行消息返回的 MessageId（若不可用则为 0）。
        /// </summary>
        public ushort PublishMessageId { get; set; }

        /// <summary>
        /// 反馈附加数据：例如 FeedingQianLiaocangResponseData。
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    }
}
