using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 设备状态业务数据（Domain模型）
    /// </summary>
    public class DeviceStateData
    {
        public string DeviceCode { get; set; }
        public int State { get; set; }
        public string FaultMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
