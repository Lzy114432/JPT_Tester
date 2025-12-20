using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 扫描探针卡响应业务数据（Domain模型）
    /// </summary>
    public class ScanProbecardResponseData
    {
        public string ProbecardCode { get; set; }
        public string Size { get; set; }
        public string ProbecardRemainingCount { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
