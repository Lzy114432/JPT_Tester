using System;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 逻辑执行异常事件参数。
    /// </summary>
    public sealed class LogicExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// 逻辑名称（通常为类型名）。
        /// </summary>
        public string LogicName { get; }

        /// <summary>
        /// 异常发生时所在步骤。
        /// </summary>
        public string Step { get; }

        /// <summary>
        /// 捕获到的异常对象。
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 创建事件参数。
        /// </summary>
        /// <param name="logicName">逻辑名称。</param>
        /// <param name="step">步骤名称。</param>
        /// <param name="exception">异常对象。</param>
        public LogicExceptionEventArgs(string logicName, string step, Exception exception)
        {
            LogicName = logicName ?? string.Empty;
            Step = step ?? string.Empty;
            Exception = exception;
        }
    }
}
