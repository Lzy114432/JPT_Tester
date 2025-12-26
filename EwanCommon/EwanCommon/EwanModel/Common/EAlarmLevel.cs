using System.ComponentModel.DataAnnotations;

namespace EwanModel.Common
{
    /// <summary>
    /// 报警级别
    /// </summary>
    public enum EAlarmLevel
    {
        [Display(Name = "高")]
        H,
        [Display(Name = "中")]
        M,
        [Display(Name = "低")]
        L
    }
}

