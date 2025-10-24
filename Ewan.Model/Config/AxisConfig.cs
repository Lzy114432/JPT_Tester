using System.Collections.Generic;

namespace Ewan.Model.Config
{
    public class AxisConfig
    {
        /// <summary>
        /// 轴ID
        /// </summary>
        public int AxisID { get; set; } = 0;


        /// <summary>
        /// 轴启用
        /// </summary>
        public bool IsUsing { get; set; } = false;


        /// <summary>
        /// 上限
        /// </summary>
        public float MaxPos { get; set; } = 700;

        /// <summary>
        /// 下限
        /// </summary>
        public float MinPos { get; set; } = -11;

        /// <summary>
        /// 脉冲细分
        /// </summary>
        public double Step { get; set; } = 1;

        /// <summary>
        /// 回零方向 (0=反向, 1=正向)
        /// </summary>
        public int HomingDir { get; set; } = 1;

        /// <summary>
        /// 加加速度（Jerk）
        /// </summary>
        public double Jerk { get; set; } = 500000;

        /// <summary>
        /// 运动方向
        /// </summary>
        public MotionDir MotionDir { get; set; } = MotionDir.Positive;


        /// <summary>
        /// 速度(mm/s)
        /// </summary>
        public double Speed { get; set; } = 1000;

        /// <summary>
        /// 加速度
        /// </summary>
        public double Acc { get; set; } = 6500;

        /// <summary>
        /// 减速度
        /// </summary>
        public double Dec { get; set; } = 6500;
    }

    /// <summary>
    /// 下拉选择项
    /// </summary>
    public class OptionItem
    {
        public string Display { get; set; }
        public bool Value { get; set; }
    }
}
