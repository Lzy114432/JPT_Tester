using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 上料响应业务数据（Domain模型）
    /// </summary>
    public class FeedingResponseData
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
