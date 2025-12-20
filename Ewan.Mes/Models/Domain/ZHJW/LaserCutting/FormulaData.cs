using System;

namespace Ewan.Mes.Models.Domain.ZHJW.LaserCutting
{
    /// <summary>
    /// 配方业务数据（Domain模型）
    /// </summary>
    public class FormulaData
    {
        public string DeviceCode { get; set; }
        public string FormulaName { get; set; }
        public object FormulaContent { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
