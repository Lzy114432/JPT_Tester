using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 上报生产数据业务数据（Domain模型）
    /// </summary>
    public class ReportWorkData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string Model { get; set; }
        public string FormulaName { get; set; }
        public string PlateCode { get; set; }
        public int Result { get; set; }
        public string NgReason { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
