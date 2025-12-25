using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 扫描板片业务数据（Domain模型）
    /// </summary>
    public class ScanPlateData
    {
        public string DeviceCode { get; set; }
        public string PlateCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
