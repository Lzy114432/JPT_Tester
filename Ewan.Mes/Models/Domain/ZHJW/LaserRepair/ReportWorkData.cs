using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 报工业务数据（Domain模型）
    /// </summary>
    public class ReportWorkData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string Model { get; set; }
        public string FormulaName { get; set; }
        public string PlateCode { get; set; }
        public string ProbecardCode { get; set; }
        public string Size { get; set; }
        public int Result { get; set; }
        public string NgReason { get; set; }
        public int OkQty { get; set; }
        public int NgQty { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
