using EwanCommon.Logging;
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        // Shared UI logger for state machine logging and alarms.
        protected readonly UILogger _uiLogger = new UILogger();

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

        /// <summary>
        /// 检查超时并自动产生报警
        /// </summary>
        protected bool CheckTimeout(int timeoutMs, string stepName = null)
        {
            if (Tw.StartCheckIsTimeout(SwitchIndex, timeoutMs))
            {
                var step = string.IsNullOrWhiteSpace(stepName) ? SwitchIndex : stepName;
                TryPostAlarmMessage(
                    key: $"Logic.Timeout.{GetType().Name}.{step}",
                    content: $"步骤超时: [{GetType().Name}] {step} 超过 {timeoutMs}ms",
                    level: AlarmLevel.M,
                    needReset: false);
                _uiLogger.Warn($"[{GetType().Name}] 步骤 {step} 超时 ({timeoutMs}ms)");
                return true;
            }
            return false;
        }

        protected virtual void OnStepChanged(string from, string to)
        {
            _uiLogger.InfoRaw($"[{GetType().Name}] 步骤: {from} → {to}");
            var args = new StepChangedEventArgs(GetType().Name, from, to, DateTimeOffset.Now);
            StepChanged?.Invoke(this, args);

            // 全局广播：便于 UI/监控订阅（强类型，无 topic 字符串）。
            MessageHub.Current.Publish(args);
        }

        // Use reflection to avoid a circular dependency on Ewan.Model.
        private static readonly object s_alarmSync = new object();
        private static bool s_alarmInitialized;
        private static MethodInfo s_alarmCreateMethod;
        private static MethodInfo s_alarmPostMethod;

        internal static void TryPostAlarmMessage(string key, string content, AlarmLevel level, bool needReset, string unit = null)
        {
            if (!EnsureAlarmMessageMethods())
            {
                return;
            }

            try
            {
                var alarmMessage = s_alarmCreateMethod?.Invoke(null, new object[] { key, content, level, needReset, unit });
                if (alarmMessage == null)
                {
                    return;
                }

                s_alarmPostMethod?.Invoke(MessageHub.Current, new object[] { alarmMessage });
            }
            catch
            {
                // Ignore alarm creation/publish failures to avoid breaking state machine flow.
            }
        }

        private static bool EnsureAlarmMessageMethods()
        {
            if (s_alarmInitialized)
            {
                return s_alarmCreateMethod != null && s_alarmPostMethod != null;
            }

            lock (s_alarmSync)
            {
                if (s_alarmInitialized)
                {
                    return s_alarmCreateMethod != null && s_alarmPostMethod != null;
                }

                var alarmType = Type.GetType("Ewan.Model.Messages.AlarmMessage, Ewan.Model");
                if (alarmType != null)
                {
                    s_alarmCreateMethod = alarmType.GetMethod(
                        "Create",
                        new[] { typeof(string), typeof(string), typeof(AlarmLevel), typeof(bool), typeof(string) });
                    var postMethod = typeof(IPublishBus).GetMethod("Post");
                    s_alarmPostMethod = postMethod?.MakeGenericMethod(alarmType);
                }

                s_alarmInitialized = true;
                return s_alarmCreateMethod != null && s_alarmPostMethod != null;
            }
        }
    }
}
