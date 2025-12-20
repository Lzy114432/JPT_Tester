using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserRepair
{
    /// <summary>
    /// 返回机型业务数据（Domain模型）
    /// </summary>
    public class ResponseModelData
    {
        public string BillNo { get; set; }
        public string Model { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
