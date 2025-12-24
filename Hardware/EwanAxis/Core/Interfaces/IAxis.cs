/*===================================================
 * 类名称: IAxis
 * 类描述: 单轴运动接口（与IO系统完全解耦）
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴运动接口
    /// 注意：此接口不依赖任何IO系统，轴相关的IO信号（限位、原点等）
    /// 由轴卡硬件自身提供，通过 GetAxisIO() 获取
    /// </summary>
    public interface IAxis
    {
        /// <summary>
        /// 轴参数
        /// </summary>
        AxisParameter Parameter { get; set; }

        /// <summary>
        /// 轴名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 轴号
        /// </summary>
        int AxisIndex { get; }

        #region 位置

        /// <summary>
        /// 获取或设置马达当前的指令位置
        /// 单位：mm 或 °
        /// </summary>
        double Position { get; set; }

        /// <summary>
        /// 编码器反馈位置
        /// 单位：mm 或 °
        /// </summary>
        double FeedbackPosition { get; set; }

        #endregion

        #region 状态

        /// <summary>
        /// 获取一个值，该值指示当前马达是否处在运动状态
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// 电机励磁状态
        /// true = 励磁（上电）
        /// false = 释放
        /// </summary>
        bool ServoOn { get; set; }

        /// <summary>
        /// 获取一个值，该值指示轴是否处于报警状态
        /// </summary>
        bool IsAlarm { get; }

        /// <summary>
        /// 回原点是否完成
        /// </summary>
        bool IsHomed { get; }

        #endregion

        #region 运动控制

        /// <summary>
        /// 开始回原点
        /// </summary>
        void Home();

        /// <summary>
        /// 回原点完成检查
        /// </summary>
        bool HomeIsDown();

        /// <summary>
        /// 绝对位置移动
        /// </summary>
        /// <param name="pos">目标位置（单位：mm 或 °）</param>
        /// <returns>是否成功发送指令</returns>
        bool AbsMove(double pos);

        /// <summary>
        /// 相对位置移动
        /// </summary>
        /// <param name="distance">移动距离（单位：mm 或 °）</param>
        /// <returns>是否成功发送指令</returns>
        bool RelMove(double distance);

        /// <summary>
        /// 点动（连续运动）
        /// </summary>
        /// <param name="speed">速度，正值正向，负值反向</param>
        /// <returns>是否成功</returns>
        bool Jog(double speed);

        /// <summary>
        /// 停止点动
        /// </summary>
        bool JogStop();

        /// <summary>
        /// 减速停止
        /// </summary>
        void DecStop();

        /// <summary>
        /// 立即停止（紧急停止）
        /// </summary>
        void EmgStop();

        #endregion

        #region 参数设置

        /// <summary>
        /// 设置运动参数
        /// </summary>
        /// <param name="startVelocity">起始速度</param>
        /// <param name="velocity">运行速度</param>
        /// <param name="accTime">加速时间（秒）</param>
        /// <param name="decTime">减速时间（秒）</param>
        void SetMotionParams(double startVelocity, double velocity, double accTime, double decTime);

        /// <summary>
        /// 设置回原点参数
        /// </summary>
        /// <param name="homeDir">回原点方向</param>
        /// <param name="homeMode">回原点模式</param>
        /// <param name="velocity">回原点速度</param>
        /// <param name="scale">轴比例</param>
        void SetHomeParams(bool homeDir, int homeMode, double velocity, double scale);

        #endregion

        #region 报警处理

        /// <summary>
        /// 清除硬件错误，使硬件恢复到能够运动的状态
        /// </summary>
        void ClearError();

        #endregion

        #region 轴专属IO

        /// <summary>
        /// 获取轴专属IO状态
        /// 这些IO信号是轴卡硬件自带的（如：限位、原点、励磁信号等）
        /// 不是通用IO系统的信号
        /// </summary>
        /// <returns>轴IO状态</returns>
        AxisIOState GetAxisIO();

        #endregion
    }
}
