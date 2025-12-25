using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 心跳业务数据（Domain模型）
    /// </summary>
    public class HeartbeatData
    {
        public string DeviceCode { get; set; }
        public int State { get; set; }
        public string FaultMessage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
