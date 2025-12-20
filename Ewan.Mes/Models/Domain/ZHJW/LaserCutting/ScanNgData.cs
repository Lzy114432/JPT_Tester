using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 扫描NG业务数据（Domain模型）
    /// </summary>
    public class ScanNgData
    {
        public string DeviceCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
