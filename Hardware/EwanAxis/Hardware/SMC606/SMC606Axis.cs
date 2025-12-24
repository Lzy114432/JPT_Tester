/*===================================================
 * 类名称: SMC606Axis
 * 类描述: 雷赛SMC606轴实现
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

using System;
using System.Threading;
using System.Threading.Tasks;
using EwanAxis.Core.Interfaces;

namespace EwanAxis.Hardware.SMC606
{
    /// <summary>
    /// 雷赛SMC606轴
    /// </summary>
    public class SMC606Axis : AxisBase
    {
        private readonly SMC606Card _card;
        private bool _isHoming;

        public SMC606Axis(SMC606Card card, AxisParameter parameter) : base(parameter)
        {
            _card = card ?? throw new ArgumentNullException(nameof(card));
        }

        #region 位置属性

        public override double Position
        {
            get
            {
                lock (_card.SyncRoot)
                {
                    double pos = 0;
                    LTSMC.smc_get_position_unit(_card.CardNo, (ushort)_parameter.AxisNum, ref pos);
                    return PulseToPosition(pos);
                }
            }
            set
            {
                lock (_card.SyncRoot)
                {
                    double pulse = PositionToPulse(value);
                    LTSMC.smc_set_position_unit(_card.CardNo, (ushort)_parameter.AxisNum, pulse);
                }
            }
        }

        public override double FeedbackPosition
        {
            get
            {
                lock (_card.SyncRoot)
                {
                    double pos = 0;
                    LTSMC.smc_get_encoder_unit(_card.CardNo, (ushort)_parameter.AxisNum, ref pos);
                    return PulseToPosition(pos);
                }
            }
            set
            {
                lock (_card.SyncRoot)
                {
                    double pulse = PositionToPulse(value);
                    LTSMC.smc_set_encoder_unit(_card.CardNo, (ushort)_parameter.AxisNum, pulse);
                }
            }
        }

        #endregion

        #region 状态属性

        public override bool IsBusy
        {
            get
            {
                lock (_card.SyncRoot)
                {
                    short ret = LTSMC.smc_check_done(_card.CardNo, (ushort)_parameter.AxisNum);
                    return ret != 1;
                }
            }
        }

        public override bool ServoOn
        {
            get
            {
                lock (_card.SyncRoot)
                {
                    int state = LTSMC.smc_read_sevon_pin(_card.CardNo, (ushort)_parameter.AxisNum);
                    return state != 1; // 0=ON, 1=OFF
                }
            }
            set
            {
                lock (_card.SyncRoot)
                {
                    ushort onOff = (ushort)(value ? 0 : 1);
                    short ret = LTSMC.smc_write_sevon_pin(_card.CardNo, (ushort)_parameter.AxisNum, onOff);

                    if (ret == 0 && value && _parameter.CalibrationData.Enable)
                    {
                        // 启用丝杆补偿
                        EnableLeadscrewCompensation();
                    }
                }
            }
        }

        public override bool IsAlarm
        {
            get
            {
                lock (_card.SyncRoot)
                {
                    uint ioStatus = LTSMC.smc_axis_io_status(_card.CardNo, (ushort)_parameter.AxisNum);
                    return (ioStatus & 0x01) > 0;
                }
            }
        }

        public override bool IsHomed
        {
            get => _isHomed && !_isHoming;
            protected set => _isHomed = value;
        }

        #endregion

        #region 运动控制

        public override bool AbsMove(double pos)
        {
            // 检查软限位
            if (!CheckSoftLimit(pos))
            {
                return false;
            }

            // 如果有报警，先清除
            if (IsAlarm)
            {
                ClearError();
            }

            lock (_card.SyncRoot)
            {
                // 检查轴状态机
                ushort stateMachine = 0;
                LTSMC.nmcs_get_axis_state_machine(_card.CardNo, (ushort)_parameter.AxisNum, ref stateMachine);

                if (stateMachine != 0)
                {
                    return false; // 轴不在正常状态
                }

                // 设置S曲线时间
                LTSMC.smc_set_s_profile(_card.CardNo, (ushort)_parameter.AxisNum, 0, _parameter.Dec / 3);

                // 设置速度参数
                LTSMC.smc_set_profile_unit(
                    _card.CardNo,
                    (ushort)_parameter.AxisNum,
                    10000,                              // 起始速度
                    _parameter.Speed * _parameter.Step, // 运行速度
                    _parameter.Acc,                     // 加速时间
                    _parameter.Dec,                     // 减速时间
                    10000                               // 停止速度
                );

                // 设置减速停止时间
                LTSMC.smc_set_dec_stop_time(_card.CardNo, (ushort)_parameter.AxisNum, _parameter.Dec);

                // 执行定长运动（绝对位置）
                int pulse = PositionToPulse(pos);
                LTSMC.smc_pmove_unit(_card.CardNo, (ushort)_parameter.AxisNum, pulse, 1);
            }

            return true;
        }

        public override bool Jog(double speed)
        {
            double vel = Math.Abs(speed);
            ushort dir = (ushort)(speed > 0 ? 1 : 0);

            // 应用方向转换
            if (_parameter.Dir)
            {
                dir = (ushort)(dir == 1 ? 0 : 1);
            }

            lock (_card.SyncRoot)
            {
                // 设置S曲线
                LTSMC.smc_set_s_profile(_card.CardNo, (ushort)_parameter.AxisNum, 0, _parameter.Dec / 3);

                // 设置速度参数
                LTSMC.smc_set_profile_unit(
                    _card.CardNo,
                    (ushort)_parameter.AxisNum,
                    10000,
                    vel * _parameter.Step,
                    _parameter.Acc,
                    _parameter.Dec,
                    10000
                );

                // 执行连续运动
                LTSMC.smc_vmove(_card.CardNo, (ushort)_parameter.AxisNum, dir);
            }

            return true;
        }

        public override bool JogStop()
        {
            lock (_card.SyncRoot)
            {
                LTSMC.smc_stop(_card.CardNo, (ushort)_parameter.AxisNum, 0);
            }
            return true;
        }

        public override void DecStop()
        {
            lock (_card.SyncRoot)
            {
                LTSMC.smc_stop(_card.CardNo, (ushort)_parameter.AxisNum, 0);
            }
        }

        public override void EmgStop()
        {
            lock (_card.SyncRoot)
            {
                LTSMC.smc_stop(_card.CardNo, (ushort)_parameter.AxisNum, 1);
            }
        }

        #endregion

        #region 回原点

        public override void Home()
        {
            lock (_card.SyncRoot)
            {
                // 检查总线错误
                ushort errCode = 0;
                LTSMC.nmcs_get_errcode(_card.CardNo, 2, ref errCode);
                if (errCode != 0)
                {
                    return;
                }

                // 清除驱动器报警
                LTSMC.smc_write_erc_pin(_card.CardNo, (ushort)_parameter.AxisNum, 0);
                LTSMC.smc_write_erc_pin(_card.CardNo, (ushort)_parameter.AxisNum, 1);

                // 获取轴状态机
                ushort stateMachine = 0;
                LTSMC.nmcs_get_axis_state_machine(_card.CardNo, (ushort)_parameter.AxisNum, ref stateMachine);

                if (stateMachine != 0)
                {
                    return; // 轴不在正常状态
                }
            }

            _isHoming = true;
            _isHomed = false;

            if (_parameter.HomeMode < 100)
            {
                // 标准回原点模式
                StartStandardHome();
            }
            else if (_parameter.HomeMode == 101)
            {
                // 凸轮轴回原点模式
                StartCamHome();
            }
        }

        private void StartStandardHome()
        {
            int homeDir = _parameter.HomeDir ? 1 : 0;

            lock (_card.SyncRoot)
            {
                // 设置回原点速度参数
                LTSMC.smc_set_home_profile_unit(
                    _card.CardNo,
                    (ushort)_parameter.AxisNum,
                    (_parameter.HomeSpeed * _parameter.Step) / 5, // 低速
                    _parameter.HomeSpeed * _parameter.Step,        // 高速
                    0.1,                                           // 加速时间
                    0.1                                            // 减速时间
                );

                // 设置回原点模式
                LTSMC.smc_set_homemode(
                    _card.CardNo,
                    (ushort)_parameter.AxisNum,
                    (ushort)homeDir,
                    1,
                    (ushort)_parameter.HomeMode,
                    0
                );

                // 开始回原点
                LTSMC.smc_home_move(_card.CardNo, (ushort)_parameter.AxisNum);
            }

            // 启动监控任务
            Task.Run(() => MonitorHomeCompletion(30000));
        }

        private void StartCamHome()
        {
            Task.Run(() =>
            {
                try
                {
                    var timeout = TimeSpan.FromSeconds(30);
                    var startTime = DateTime.Now;

                    // 如果不在原点，先正向找原点
                    if (!GetAxisIO().ORG)
                    {
                        Jog(_parameter.HomeSpeed);
                        while (DateTime.Now - startTime < timeout)
                        {
                            if (GetAxisIO().ORG)
                            {
                                Thread.Sleep(500);
                                JogStop();
                                break;
                            }
                            Thread.Sleep(10);
                        }
                    }

                    // 如果在原点，反向离开原点
                    if (GetAxisIO().ORG)
                    {
                        Thread.Sleep(1500);
                        Jog(-_parameter.HomeSpeed);
                        startTime = DateTime.Now;

                        while (DateTime.Now - startTime < timeout)
                        {
                            if (!GetAxisIO().ORG)
                            {
                                JogStop();
                                Thread.Sleep(1000);

                                // 清零位置
                                Position = 0;
                                FeedbackPosition = 0;

                                _isHomed = true;
                                break;
                            }
                            Thread.Sleep(10);
                        }
                    }
                }
                finally
                {
                    _isHoming = false;
                }
            });
        }

        private void MonitorHomeCompletion(int timeoutMs)
        {
            try
            {
                var startTime = DateTime.Now;
                var timeout = TimeSpan.FromMilliseconds(timeoutMs);

                while (DateTime.Now - startTime < timeout)
                {
                    if (HomeIsDown())
                    {
                        Thread.Sleep(1000);

                        // 清零位置
                        Position = 0;
                        FeedbackPosition = 0;

                        _isHomed = true;
                        break;
                    }
                    Thread.Sleep(500);
                }
            }
            finally
            {
                _isHoming = false;
            }
        }

        public override bool HomeIsDown()
        {
            if (_parameter.HomeMode < 100)
            {
                lock (_card.SyncRoot)
                {
                    ushort state = 0;
                    LTSMC.smc_get_home_result(_card.CardNo, (ushort)_parameter.AxisNum, ref state);
                    return state == 1;
                }
            }
            else if (_parameter.HomeMode == 101)
            {
                var ioState = GetAxisIO();
                return !ioState.ORG && Math.Abs(FeedbackPosition) < 0.01;
            }

            return false;
        }

        #endregion

        #region 参数设置

        public override void SetMotionParams(double startVelocity, double velocity, double accTime, double decTime)
        {
            _parameter.Speed = velocity;
            _parameter.Acc = accTime;
            _parameter.Dec = decTime;
        }

        public override void SetHomeParams(bool homeDir, int homeMode, double velocity, double scale)
        {
            _parameter.HomeDir = homeDir;
            _parameter.HomeMode = homeMode;
            _parameter.HomeSpeed = velocity;
        }

        #endregion

        #region 错误处理

        public override void ClearError()
        {
            // 发送ERC信号清除报警
            lock (_card.SyncRoot)
            {
                LTSMC.smc_write_erc_pin(_card.CardNo, (ushort)_parameter.AxisNum, 0);
            }
            Thread.Sleep(300);
            lock (_card.SyncRoot)
            {
                LTSMC.smc_write_erc_pin(_card.CardNo, (ushort)_parameter.AxisNum, 1);
            }
        }

        #endregion

        #region IO状态

        public override AxisIOState GetAxisIO()
        {
            uint ret;
            lock (_card.SyncRoot)
            {
                ret = LTSMC.smc_axis_io_status(_card.CardNo, (ushort)_parameter.AxisNum);
            }

            var ioState = new AxisIOState
            {
                ALM = (ret & (1 << 0)) > 0,  // bit 0: 报警
                ELP = (ret & (1 << 1)) > 0,  // bit 1: 正限位
                ELN = (ret & (1 << 2)) > 0,  // bit 2: 负限位
                ORG = (ret & (1 << 4)) > 0,  // bit 4: 原点
                SLP = (ret & (1 << 6)) > 0,  // bit 6: 正软限位
                SLN = (ret & (1 << 7)) > 0,  // bit 7: 负软限位
                INP = (ret & (1 << 8)) > 0,  // bit 8: 到位信号
                Busy = IsBusy,
                Home = _isHoming
            };

            return ioState;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 启用丝杆补偿
        /// </summary>
        private void EnableLeadscrewCompensation()
        {
            if (_parameter.CalibrationData == null || !_parameter.CalibrationData.Enable)
                return;

            lock (_card.SyncRoot)
            {
                LTSMC.smc_enable_leadscrew_comp(_card.CardNo, (ushort)_parameter.AxisNum, 1);

                var calData = _parameter.CalibrationData;
                LTSMC.smc_set_leadscrew_comp_config_unit(
                    _card.CardNo,
                    (ushort)_parameter.AxisNum,
                    (ushort)calData.PointNum,
                    calData.StartPoint,
                    calData.Length,
                    calData.CalData.ToArray(),
                    calData.CalData.ToArray()
                );
            }
        }

        #endregion
    }
}
