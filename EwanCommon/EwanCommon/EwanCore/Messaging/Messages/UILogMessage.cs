using System;

namespace EwanCore.Messaging.Messages
{
    /// <summary>
    /// UI日志级别
    /// </summary>
    public enum UILogLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    /// <summary>
    /// UI日志消息 - 用于通过 MessageBus 传递日志到 UI 显示
    /// </summary>
    public sealed class UILogMessage : IMessage
    {
        /// <summary>
        /// 日志级别
        /// </summary>
        public UILogLevel Level { get; }

        /// <summary>
        /// 日志消息内容
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 调用者信息（类名:行号）
        /// </summary>
        public string? CallerInfo { get; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// 创建 UI 日志消息
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息内容</param>
        /// <param name="callerInfo">调用者信息</param>
        public UILogMessage(UILogLevel level, string message, string? callerInfo = null)
        {
            Level = level;
            Message = message ?? string.Empty;
            CallerInfo = callerInfo;
            Timestamp = DateTimeOffset.Now;
        }

        /// <summary>
        /// 创建 Debug 级别日志消息
        /// </summary>
        public static UILogMessage Debug(string message, string? callerInfo = null)
            => new UILogMessage(UILogLevel.Debug, message, callerInfo);

        /// <summary>
        /// 创建 Info 级别日志消息
        /// </summary>
        public static UILogMessage Info(string message, string? callerInfo = null)
            => new UILogMessage(UILogLevel.Info, message, callerInfo);

        /// <summary>
        /// 创建 Warn 级别日志消息
        /// </summary>
        public static UILogMessage Warn(string message, string? callerInfo = null)
            => new UILogMessage(UILogLevel.Warn, message, callerInfo);

        /// <summary>
        /// 创建 Error 级别日志消息
        /// </summary>
        public static UILogMessage Error(string message, string? callerInfo = null)
            => new UILogMessage(UILogLevel.Error, message, callerInfo);

        /// <summary>
        /// 创建 Fatal 级别日志消息
        /// </summary>
        public static UILogMessage Fatal(string message, string? callerInfo = null)
            => new UILogMessage(UILogLevel.Fatal, message, callerInfo);
    }
}
