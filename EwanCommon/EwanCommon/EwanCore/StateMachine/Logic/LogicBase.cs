using EwanCore.Messaging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 状态机逻辑基类（参考 Byron.Commond.BaseLogic）。
    /// - 使用 <see cref="SwitchIndex"/> 表达步骤
    /// - 使用 <see cref="TimeoutWatch"/> 处理超时
    /// - 通过 <see cref="GetLogicState"/> 生成状态字符串（用于监控界面显示）
    /// </summary>
    public abstract class LogicBase
    {
        private readonly object _listLock = new object();
        private readonly List<LogicBase> _curSonLogicList = new List<LogicBase>();

        private string _switchIndex = "初始状态";

        /// <summary>
        /// 是否完成。
        /// </summary>
        public bool IsFinish { get; protected set; }

        /// <summary>
        /// 当前步骤。
        /// </summary>
        public string SwitchIndex
        {
            get => _switchIndex;
            protected set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_switchIndex, next, StringComparison.Ordinal))
                {
                    return;
                }

                var from = _switchIndex;
                _switchIndex = next;
                OnStepChanged(from, next);
            }
        }

        /// <summary>
        /// 超时工具（每个逻辑一个）。
        /// </summary>
        protected TimeoutWatch Tw { get; } = new TimeoutWatch();

        /// <summary>
        /// 步骤切换事件。
        /// </summary>
        public event EventHandler<StepChangedEventArgs> StepChanged;

        /// <summary>
        /// 子逻辑：仅用于“监控显示”组合（参考 Byron 的 AddCurSonLogic：每次会覆盖当前子逻辑）。
        /// </summary>
        public void AddCurSonLogic(LogicBase baseLogic)
        {
            if (baseLogic == null) throw new ArgumentNullException(nameof(baseLogic));

            lock (_listLock)
            {
                baseLogic.Rset();
                _curSonLogicList.Clear();
                _curSonLogicList.Add(baseLogic);
            }
        }

        /// <summary>
        /// 获取状态字符串（用于显示）。
        /// </summary>
        public virtual string GetLogicState()
        {
            lock (_listLock)
            {
                var son = string.Empty;
                for (var i = 0; i < _curSonLogicList.Count; i++)
                {
                    son += _curSonLogicList[i]?.ToString() ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(son))
                {
                    return $"【 {this}:{SwitchIndex}-{son} 】";
                }

                return $"【{this}:{SwitchIndex}】";
            }
        }

        /// <summary>
        /// 复位：回到初始状态，并清空完成标记。
        /// </summary>
        public virtual void Rset()
        {
            IsFinish = false;
            SwitchIndex = "初始状态";
        }

        /// <summary>
        /// 完成：进入结束状态，并标记完成。
        /// </summary>
        public virtual void Complete()
        {
            IsFinish = true;
            SwitchIndex = "结束状态";
        }

        /// <summary>
        /// 执行一次步骤逻辑。
        /// </summary>
        public abstract void Handler();

        protected virtual void OnStepChanged(string from, string to)
        {
            var args = new StepChangedEventArgs(GetType().Name, from, to, DateTimeOffset.Now);
            StepChanged?.Invoke(this, args);

            // 全局广播：便于 UI/监控订阅（强类型，无 topic 字符串）。
            MessageHub.Current.Publish(args);
        }
    }
}
