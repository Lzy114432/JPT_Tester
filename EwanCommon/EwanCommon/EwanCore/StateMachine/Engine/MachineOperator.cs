using EwanCore.AlarmSystem;
using System;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 面向监控/主控的“机台操作器”：把 Start/Stop/Pause/Home/ClearAlarm 等常用动作组织成一套约定。
    /// </summary>
    public sealed class MachineOperator
    {
        private readonly IAlarmService _alarms;
        private readonly LogicRunner _runner;

        /// <summary>
        /// 创建一个机台操作器。
        /// </summary>
        /// <param name="alarms">报警服务。</param>
        /// <param name="runner">逻辑 runner。</param>
        public MachineOperator(IAlarmService alarms, LogicRunner runner)
        {
            _alarms = alarms ?? throw new ArgumentNullException(nameof(alarms));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        /// <summary>
        /// 启动：如果存在报警则拒绝启动；若队列为空则自动追加 mainLogic。
        /// </summary>
        /// <param name="mainLogicFactory">主流程逻辑工厂。</param>
        /// <returns>是否启动成功。</returns>
        public bool Start(Func<LogicBase> mainLogicFactory)
        {
            if (mainLogicFactory == null) throw new ArgumentNullException(nameof(mainLogicFactory));

            if (_alarms.HasAlarm)
            {
                return false;
            }

            if (_runner.Count == 0)
            {
                _runner.AddAction(mainLogicFactory());
            }

            _runner.Start();
            return true;
        }

        /// <summary>
        /// 暂停：仅停止执行，不清空队列。
        /// </summary>
        public void Pause()
        {
            _runner.Pause();
        }

        /// <summary>
        /// 停止：停止执行，并可选清空队列。
        /// </summary>
        /// <param name="clearQueue">是否清空队列。</param>
        public void Stop(bool clearQueue = false)
        {
            _runner.Stop();
            if (clearQueue)
            {
                _runner.ClearAction();
            }
        }

        /// <summary>
        /// 单步：执行一轮后自动停机。
        /// </summary>
        public void Step()
        {
            _runner.Step();
        }

        /// <summary>
        /// 复位（Home）：停止→清队列→可选清报警→追加 homeLogic→启动。
        /// </summary>
        /// <param name="homeLogicFactory">复位/回原流程逻辑工厂。</param>
        /// <param name="clearAlarm">是否在复位前清除报警。</param>
        /// <param name="beforeHome">复位前回调（可用于做一些同步准备动作）。</param>
        public void Home(Func<LogicBase> homeLogicFactory, bool clearAlarm = true, Action beforeHome = null)
        {
            if (homeLogicFactory == null) throw new ArgumentNullException(nameof(homeLogicFactory));

            Stop(clearQueue: true);
            beforeHome?.Invoke();

            if (clearAlarm)
            {
                _alarms.Clear();
            }

            _runner.AddAction(homeLogicFactory());
            _runner.Start();
        }

        /// <summary>
        /// 清除报警。
        /// </summary>
        public void ClearAlarm()
        {
            _alarms.Clear();
        }
    }
}
