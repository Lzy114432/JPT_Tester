/*===================================================
 * 类名称: AxisIOState
 * 类描述: 轴专属IO状态（轴卡硬件自带的IO信号）
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴IO状态
    /// 这些是轴卡硬件自带的IO信号，不是通用IO系统的信号
    /// </summary>
    public class AxisIOState
    {
        /*
         * 信号名称        描述
         * ALM             1：表示伺服报警信号ALM 为ON； 0：OFF
         * EL+             1：表示正硬限位信号+EL 为ON； 0：OFF
         * EL-             1：表示负硬限位信号–EL 为ON； 0：OFF
         * ORG             1：表示原点信号ORG 为ON； 0：OFF
         * SL+             1：表示正软限位信号+SL 为ON； 0：OFF
         * SL-             1：表示负软件限位信号-SL 为ON； 0：OFF
         * INP             1：表示伺服到位信号INP 为ON； 0：OFF
         * Busy            1：表示电机正在运动中
         * Home            1：表示电机正在回原点
         */

        /// <summary>
        /// 伺服报警信号
        /// </summary>
        public bool ALM { get; set; } = false;

        /// <summary>
        /// 正向硬限位信号
        /// </summary>
        public bool ELP { get; set; } = false;

        /// <summary>
        /// 负向硬限位信号
        /// </summary>
        public bool ELN { get; set; } = false;

        /// <summary>
        /// 原点信号
        /// </summary>
        public bool ORG { get; set; } = false;

        /// <summary>
        /// 正向软限位信号
        /// </summary>
        public bool SLP { get; set; } = false;

        /// <summary>
        /// 负向软限位信号
        /// </summary>
        public bool SLN { get; set; } = false;

        /// <summary>
        /// 伺服到位信号
        /// </summary>
        public bool INP { get; set; } = false;

        /// <summary>
        /// 电机运动中
        /// </summary>
        public bool Busy { get; set; } = false;

        /// <summary>
        /// 电机正在回原点
        /// </summary>
        public bool Home { get; set; } = false;

        /// <summary>
        /// 克隆状态
        /// </summary>
        public AxisIOState Clone()
        {
            return new AxisIOState
            {
                ALM = this.ALM,
                ELP = this.ELP,
                ELN = this.ELN,
                ORG = this.ORG,
                SLP = this.SLP,
                SLN = this.SLN,
                INP = this.INP,
                Busy = this.Busy,
                Home = this.Home
            };
        }

        /// <summary>
        /// 是否有任何限位触发
        /// </summary>
        public bool HasLimitTriggered => ELP || ELN || SLP || SLN;

        /// <summary>
        /// 是否安全（无报警、无限位）
        /// </summary>
        public bool IsSafe => !ALM && !HasLimitTriggered;

        public override string ToString()
        {
            return $"ALM={ALM}, EL+={ELP}, EL-={ELN}, ORG={ORG}, INP={INP}, Busy={Busy}";
        }
    }
}
