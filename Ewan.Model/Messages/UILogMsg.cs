using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 界面日志消息数据
    /// </summary>
    public class UILogMsg
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// 消息键（用于国际化）
        /// </summary>
        public string MessageKey { get; set; }

        /// <summary>
        /// 消息参数（用于格式化）
        /// </summary>
        public object[] Parameters { get; set; }

        /// <summary>
        /// 原始消息（备用）
        /// </summary>
        public string RawMessage { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        public UILogMsg(LogLevel level, string messageKey, params object[] parameters)
        {
            Level = level;
            MessageKey = messageKey;
            Parameters = parameters;
            Timestamp = DateTime.Now;
        }

        public UILogMsg(LogLevel level, string rawMessage)
        {
            Level = level;
            RawMessage = rawMessage;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }
}