using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 扫描探针卡业务数据（Domain模型）
    /// </summary>
    public class ScanProbecardData
    {
        public string DeviceCode { get; set; }
        public string ProbecardCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
