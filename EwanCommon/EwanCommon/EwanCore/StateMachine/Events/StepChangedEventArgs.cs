using EwanCore.Messaging;
using System;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 步骤切换事件参数。
    /// </summary>
    public sealed class StepChangedEventArgs : EventArgs, IMessage
    {
        /// <summary>
        /// 逻辑名称（通常为类型名）。
        /// </summary>
        public string LogicName { get; }

        /// <summary>
        /// 切换前步骤。
        /// </summary>
        public string FromStep { get; }

        /// <summary>
        /// 切换后步骤。
        /// </summary>
        public string ToStep { get; }

        /// <summary>
        /// 切换时间。
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// 创建事件参数。
        /// </summary>
        /// <param name="logicName">逻辑名称。</param>
        /// <param name="fromStep">切换前步骤。</param>
        /// <param name="toStep">切换后步骤。</param>
        /// <param name="timestamp">切换时间。</param>
        public StepChangedEventArgs(string logicName, string fromStep, string toStep, DateTimeOffset timestamp)
        {
            LogicName = logicName ?? string.Empty;
            FromStep = fromStep ?? string.Empty;
            ToStep = toStep ?? string.Empty;
            Timestamp = timestamp;
        }
    }
}
