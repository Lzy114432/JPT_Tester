/*===================================================
 * 类名称: AxisBase
 * 类描述: 轴抽象基类，提供通用实现
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴抽象基类
    /// </summary>
    public abstract class AxisBase : IAxis
    {
        protected AxisParameter _parameter;
        protected bool _isHomed;

        protected AxisBase(AxisParameter parameter)
        {
            _parameter = parameter ?? new AxisParameter();
        }

        public AxisParameter Parameter
        {
            get => _parameter;
            set => _parameter = value ?? new AxisParameter();
        }

        public virtual string Name => _parameter.Name;
        public virtual int AxisIndex => _parameter.AxisNum;

        public abstract double Position { get; set; }
        public abstract double FeedbackPosition { get; set; }
        public abstract bool IsBusy { get; }
        public abstract bool ServoOn { get; set; }
        public abstract bool IsAlarm { get; }

        public virtual bool IsHomed
        {
            get => _isHomed;
            protected set => _isHomed = value;
        }

        public abstract void Home();
        public abstract bool HomeIsDown();
        public abstract bool AbsMove(double pos);

        public virtual bool RelMove(double distance)
        {
            return AbsMove(Position + distance);
        }

        public abstract bool Jog(double speed);
        public abstract bool JogStop();
        public abstract void DecStop();
        public abstract void EmgStop();
        public abstract void SetMotionParams(double startVelocity, double velocity, double accTime, double decTime);
        public abstract void SetHomeParams(bool homeDir, int homeMode, double velocity, double scale);
        public abstract void ClearError();
        public abstract AxisIOState GetAxisIO();

        /// <summary>
        /// 检查软限位
        /// </summary>
        protected virtual bool CheckSoftLimit(double targetPos)
        {
            if (!_parameter.SoftLimitEnable)
                return true;

            return targetPos >= _parameter.SoftLimitNegative &&
                   targetPos <= _parameter.SoftLimitPositive;
        }

        /// <summary>
        /// 应用方向转换
        /// </summary>
        protected virtual double ApplyDirection(double value)
        {
            return _parameter.Dir ? -value : value;
        }

        /// <summary>
        /// 位置转脉冲
        /// </summary>
        protected virtual int PositionToPulse(double position)
        {
            return (int)(ApplyDirection(position) * _parameter.Step);
        }

        /// <summary>
        /// 脉冲转位置
        /// </summary>
        protected virtual double PulseToPosition(double pulse)
        {
            return ApplyDirection(pulse / _parameter.Step);
        }
    }
}
