using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 返回规格型号业务数据（Domain模型）
    /// </summary>
    public class ResponseModelData
    {
        public string BillNo { get; set; }
        public string Model { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
