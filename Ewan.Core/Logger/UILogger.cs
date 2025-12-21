using Ewan.Core.Msg;
using Ewan.Model.Messages;
using System;
using System.Linq.Expressions;
using FileLogger = Ewan.LogManager.Logger.FileLogger;
using LogLevel = Ewan.LogManager.Logger.LogLevel;

namespace Ewan.Core.Logger
{
    /// <summary>
    /// 界面日志记录器（已移除国际化资源依赖）
    /// </summary>
    public class UILogger : FileLogger
    {

        public UILogger(Type resourceType = null) : base("UILogger", resourceType)
        {
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
            LogToUIRaw(level, rawMessage);
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

        private void LogToUI(LogLevel level, string messageKey, object[] parameters)
        {
            try
            {
                var uiLogLevel = ConvertToUILogLevel(level);
                var uiLogData = new UILogMsg(uiLogLevel, messageKey, parameters);
                var message = new MessageModel(MsgSubject.UILog, uiLogData);
                MsgManager.Instance().PushMsg(message);
            }
            catch (Exception ex)
            {
                base.Error($"Failed to push UI log message for key: {messageKey}", ex);
            }
        }

        private void LogToUIRaw(LogLevel level, string rawMessage)
        {
            try
            {
                var uiLogLevel = ConvertToUILogLevel(level);
                var uiLogData = new UILogMsg(uiLogLevel, rawMessage);
                var message = new MessageModel(MsgSubject.UILog, uiLogData);
                MsgManager.Instance().PushMsg(message);
            }
            catch (Exception ex)
            {
                base.Error($"Failed to push raw UI log message: {rawMessage}", ex);
            }
        }

        /// <summary>
        /// 使用原始字符串记录日志
        /// </summary>
        private void LogRawMessage(LogLevel level, string message, object[] parameters)
        {
            var formattedMessage = FormatMessage(message, parameters);
            LogToUIRaw(level, formattedMessage);
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
        /// 转换日志级别
        /// </summary>
        /// <param name="level">FileLogger的日志级别</param>
        /// <returns>UILogMsg的日志级别</returns>
        private Ewan.Model.Messages.LogLevel ConvertToUILogLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return Ewan.Model.Messages.LogLevel.Debug;
                case LogLevel.Info:
                    return Ewan.Model.Messages.LogLevel.Info;
                case LogLevel.Warn:
                    return Ewan.Model.Messages.LogLevel.Warn;
                case LogLevel.Error:
                    return Ewan.Model.Messages.LogLevel.Error;
                case LogLevel.Fatal:
                    return Ewan.Model.Messages.LogLevel.Fatal;
                default:
                    return Ewan.Model.Messages.LogLevel.Info;
            }
        }
        
        /// <summary>
        /// 重写获取文化设置方法
        /// </summary>
        /// <returns>当前文化设置</returns>
        protected override System.Globalization.CultureInfo GetCurrentCulture()
        {
            try
            {
                return Culture.CultureManager.Instance().CurrentCulture;
            }
            catch
            {
                return System.Globalization.CultureInfo.CurrentUICulture;
            }
        }

    }
}
