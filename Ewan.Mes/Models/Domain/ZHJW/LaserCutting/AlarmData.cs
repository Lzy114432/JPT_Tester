using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 报警业务数据（Domain模型）
    /// </summary>
    public class AlarmData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string PlateCode { get; set; }
        public string Model { get; set; }
        public string FormulaName { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
