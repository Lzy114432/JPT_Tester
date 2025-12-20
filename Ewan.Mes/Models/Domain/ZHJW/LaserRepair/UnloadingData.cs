using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 卸料业务数据（Domain模型）
    /// </summary>
    public class UnloadingData
    {
        public string DeviceCode { get; set; }
        public string BillNoWip { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
