using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 请求规格型号业务数据（Domain模型）
    /// </summary>
    public class RequestModelData
    {
        public string DeviceCode { get; set; }
        public string BillNo { get; set; }
        public string PlateCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
