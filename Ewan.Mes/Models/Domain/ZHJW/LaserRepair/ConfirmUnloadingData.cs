using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 确认卸料业务数据（Domain模型）
    /// </summary>
    public class ConfirmUnloadingData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string PlateCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
