using EwanCore.Messaging;
using EwanCore.Messaging.Messages;
using System;
using System.Linq.Expressions;
using FileLogger = Ewan.LogManager.Logger.FileLogger;
using LogLevel = Ewan.LogManager.Logger.LogLevel;

namespace Ewan.Core.Logger
{
    /// <summary>
    /// 界面日志记录器 - 使用 MessageBus 进行 UI 日志通知
    /// 支持依赖注入：可通过构造函数注入 IPublishBus
    /// </summary>
    public class UILogger : FileLogger
    {
        private readonly IPublishBus _publishBus;

        /// <summary>
        /// 创建 UILogger（使用全局 MessageHub）
        /// </summary>
        /// <param name="resourceType">资源类型（用于国际化，已禁用）</param>
        public UILogger(Type resourceType = null) : this(MessageHub.PublishBus, resourceType)
        {
        }

        /// <summary>
        /// 创建 UILogger（依赖注入方式）
        /// </summary>
        /// <param name="publishBus">消息发布总线</param>
        /// <param name="resourceType">资源类型（用于国际化，已禁用）</param>
        public UILogger(IPublishBus publishBus, Type resourceType = null) : base("UILogger", resourceType)
        {
            _publishBus = publishBus ?? MessageHub.PublishBus;
        }

        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="parameters">参数</param>
        public void Info(string message, params object[] parameters)
        {
            InfoRaw(message, parameters);
        }

        /// <summary>
        /// 记录信息级别日志 - 使用表达式
        /// </summary>
        /// <param name="messageExpression">消息表达式</param>
        /// <param name="parameters">参数</param>
        public void Info(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var message = GetMessageFromExpression(messageExpression);
            InfoRaw(message, parameters);
        }

        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="parameters">参数</param>
        public void Warn(string message, params object[] parameters)
        {
            WarnRaw(message, parameters);
        }

        /// <summary>
        /// 记录警告级别日志 - 使用表达式
        /// </summary>
        /// <param name="messageExpression">消息表达式</param>
        /// <param name="parameters">参数</param>
        public void Warn(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var message = GetMessageFromExpression(messageExpression);
            WarnRaw(message, parameters);
        }

        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="parameters">参数</param>
        public void Error(string message, params object[] parameters)
        {
            ErrorRaw(message, parameters);
        }

        /// <summary>
        /// 记录错误级别日志 - 使用表达式
        /// </summary>
        /// <param name="messageExpression">消息表达式</param>
        /// <param name="parameters">参数</param>
        public void Error(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var message = GetMessageFromExpression(messageExpression);
            ErrorRaw(message, parameters);
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="parameters">参数</param>
        public new void Debug(string message, params object[] parameters)
        {
            DebugRaw(message, parameters);
        }

        /// <summary>
        /// 记录调试级别日志 - 使用表达式
        /// </summary>
        /// <param name="messageExpression">消息表达式</param>
        /// <param name="parameters">参数</param>
        public void Debug(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var message = GetMessageFromExpression(messageExpression);
            DebugRaw(message, parameters);
        }

        /// <summary>
        /// 记录致命错误级别日志
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="parameters">参数</param>
        public new void Fatal(string message, params object[] parameters)
        {
            FatalRaw(message, parameters);
        }

        /// <summary>
        /// 记录信息级别原始日志（直接输出字符串）
        /// </summary>
        /// <param name="message">原始消息</param>
        /// <param name="parameters">格式化参数</param>
        public void InfoRaw(string message, params object[] parameters)
        {
            LogRawMessage(LogLevel.Info, message, parameters);
        }

        /// <summary>
        /// 记录警告级别原始日志（直接输出字符串）
        /// </summary>
        /// <param name="message">原始消息</param>
        /// <param name="parameters">格式化参数</param>
        public void WarnRaw(string message, params object[] parameters)
        {
            LogRawMessage(LogLevel.Warn, message, parameters);
        }

        /// <summary>
        /// 记录错误级别原始日志（直接输出字符串）
        /// </summary>
        /// <param name="message">原始消息</param>
        /// <param name="parameters">格式化参数</param>
        public void ErrorRaw(string message, params object[] parameters)
        {
            LogRawMessage(LogLevel.Error, message, parameters);
        }

        /// <summary>
        /// 记录调试级别原始日志（直接输出字符串）
        /// </summary>
        /// <param name="message">原始消息</param>
        /// <param name="parameters">格式化参数</param>
        public void DebugRaw(string message, params object[] parameters)
        {
            LogRawMessage(LogLevel.Debug, message, parameters);
        }

        /// <summary>
        /// 记录致命级别原始日志（直接输出字符串）
        /// </summary>
        /// <param name="message">原始消息</param>
        /// <param name="parameters">格式化参数</param>
        public void FatalRaw(string message, params object[] parameters)
        {
            LogRawMessage(LogLevel.Fatal, message, parameters);
        }

        /// <summary>
        /// 记录原始消息日志（不国际化）
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="rawMessage">原始消息</param>
        public new void LogRaw(LogLevel level, string rawMessage)
        {
            PublishToUI(level, rawMessage);
            base.LogWithCallerInfo(level, rawMessage);
        }

        private static string GetMessageFromExpression(Expression<Func<string>> messageExpression)
        {
            if (messageExpression == null)
            {
                return string.Empty;
            }

            try
            {
                return messageExpression.Compile().Invoke() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 通过 MessageBus 发布 UI 日志消息
        /// </summary>
        private void PublishToUI(LogLevel level, string rawMessage)
        {
            try
            {
                var uiLogLevel = ConvertToUILogLevel(level);
                var callerInfo = GetCallerInfo();
                var uiLogMessage = new UILogMessage(uiLogLevel, rawMessage, callerInfo);

                // 使用 Post 异步发布，避免阻塞调用线程
                _publishBus.Post(uiLogMessage);
            }
            catch (Exception ex)
            {
                base.Error($"Failed to publish UI log message: {rawMessage}", ex);
            }
        }

        /// <summary>
        /// 使用原始字符串记录日志
        /// </summary>
        private void LogRawMessage(LogLevel level, string message, object[] parameters)
        {
            var formattedMessage = FormatMessage(message, parameters);
            PublishToUI(level, formattedMessage);
            base.LogWithCallerInfo(level, formattedMessage);
        }

        private static string FormatMessage(string message, object[] parameters)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            if (parameters == null || parameters.Length == 0)
            {
                return message;
            }

            try
            {
                return string.Format(message, parameters);
            }
            catch (FormatException)
            {
                // 如果格式化失败，返回原始消息并附带参数列表
                return message + " " + string.Join(", ", parameters);
            }
        }

        /// <summary>
        /// 转换日志级别到 UILogLevel
        /// </summary>
        private static UILogLevel ConvertToUILogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return UILogLevel.Debug;
                case LogLevel.Info:
                    return UILogLevel.Info;
                case LogLevel.Warn:
                    return UILogLevel.Warn;
                case LogLevel.Error:
                    return UILogLevel.Error;
                case LogLevel.Fatal:
                    return UILogLevel.Fatal;
                default:
                    return UILogLevel.Info;
            }
        }

        /// <summary>
        /// 重写获取文化设置方法
        /// </summary>
        protected override System.Globalization.CultureInfo GetCurrentCulture()
        {
            try
            {
                return System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            }
            catch
            {
                return System.Globalization.CultureInfo.CurrentUICulture;
            }
        }
    }
}
