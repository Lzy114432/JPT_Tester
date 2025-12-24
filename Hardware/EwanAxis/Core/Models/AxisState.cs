/*===================================================
 * 类名称: AxisState
 * 类描述: 轴状态枚举
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴运动状态
    /// </summary>
    public enum AxisMotionState
    {
        /// <summary>
        /// 空闲
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 运动中
        /// </summary>
        Moving = 1,

        /// <summary>
        /// 回原点中
        /// </summary>
        Homing = 2,

        /// <summary>
        /// 减速停止中
        /// </summary>
        Decelerating = 3,

        /// <summary>
        /// 报警
        /// </summary>
        Alarm = 4,

        /// <summary>
        /// 未初始化
        /// </summary>
        NotInitialized = 5
    }

    /// <summary>
    /// 运动类型
    /// </summary>
    public enum MotionType
    {
        /// <summary>
        /// 绝对位置移动
        /// </summary>
        Absolute = 0,

        /// <summary>
        /// 相对位置移动
        /// </summary>
        Relative = 1,

        /// <summary>
        /// 点动
        /// </summary>
        Jog = 2,

        /// <summary>
        /// 回原点
        /// </summary>
        Home = 3
    }

    /// <summary>
    /// 停止类型
    /// </summary>
    public enum StopType
    {
        /// <summary>
        /// 减速停止
        /// </summary>
        Deceleration = 0,

        /// <summary>
        /// 紧急停止
        /// </summary>
        Emergency = 1
    }
}
