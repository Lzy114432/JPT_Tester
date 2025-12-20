using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 确认上料业务数据（Domain模型）
    /// </summary>
    public class ConfirmFeedingData
    {
        public string DeviceCode { get; set; }
        public int Result { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
