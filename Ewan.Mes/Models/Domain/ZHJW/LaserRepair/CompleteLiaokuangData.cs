using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 完成镂空业务数据（Domain模型）
    /// </summary>
    public class CompleteLiaokuangData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string PlateCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
