using System;

namespace Ewan.Core.Msg
{
    public enum MesRingLineAction
    {
        FeedingQianLiaocang = 1,
        FeedingQianLiaocangSuccess = 2,
        UnloadingQianLiaocang = 3,
        FeedingZhongLiaocang = 4,
        UnloadingZhongLiaocang = 5,
        FeedingQingxihongganji = 6,
        FeedingHouLiaocang = 7,
        UnloadingHouLiaocang = 8
    }

    public class MesRingLineRequest
    {
        public Guid CorrelationId { get; set; }

        public MesRingLineAction Action { get; set; }

        public string PlateCode { get; set; }

        public string BillNoWip { get; set; }

        public string FeedingLiaokuangCode { get; set; }

        /// <summary>
        /// 等待 MES 反馈的超时毫秒；小于等于 0 时使用默认值。
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class MesRingLineFeedback
    {
        public Guid CorrelationId { get; set; }

        public MesRingLineAction Action { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }

        /// <summary>
        /// 发布上行消息返回的 MessageId（若不可用则为 0）。
        /// </summary>
        public ushort PublishMessageId { get; set; }

        /// <summary>
        /// 反馈附加数据：例如 FeedingQianLiaocangResponseData。
        /// </summary>
        public object Data { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}

