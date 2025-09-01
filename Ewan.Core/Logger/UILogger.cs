using Ewan.Core.Msg;
using Ewan.Model.Messages;
using System;
using System.Linq.Expressions;
using FileLogger = Ewan.LogManager.Logger.FileLogger;
using LogLevel = Ewan.LogManager.Logger.LogLevel;

namespace Ewan.Core.Logger
{
    /// <summary>
    /// 国际化界面日志记录器
    /// </summary>
    public class UILogger : FileLogger
    {

        public UILogger(Type resourceType) : base("UILogger", resourceType)
        {
        }

        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Info(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Info, messageKey, parameters);
            LogLocalized(LogLevel.Info, messageKey, parameters);
        }

        /// <summary>
        /// 记录信息级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Info(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Info, messageKey, parameters);
            LogLocalized(LogLevel.Info, messageKey, parameters);
        }

        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Warn(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Warn, messageKey, parameters);
            LogLocalized(LogLevel.Warn, messageKey, parameters);
        }

        /// <summary>
        /// 记录警告级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Warn(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Warn, messageKey, parameters);
            LogLocalized(LogLevel.Warn, messageKey, parameters);
        }

        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Error(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Error, messageKey, parameters);
            LogLocalized(LogLevel.Error, messageKey, parameters);
        }

        /// <summary>
        /// 记录错误级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Error(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Error, messageKey, parameters);
            LogLocalized(LogLevel.Error, messageKey, parameters);
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public new void Debug(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Debug, messageKey, parameters);
            LogLocalized(LogLevel.Debug, messageKey, parameters);
        }

        /// <summary>
        /// 记录调试级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Debug(Expression<Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Debug, messageKey, parameters);
            LogLocalized(LogLevel.Debug, messageKey, parameters);
        }

        /// <summary>
        /// 记录致命错误级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public new void Fatal(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Fatal, messageKey, parameters);
            LogLocalized(LogLevel.Fatal, messageKey, parameters);
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



        /// <summary>
        /// 从资源表达式获取消息键
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <returns>消息键</returns>
        private string GetResourceKeyFromExpression(Expression<Func<string>> messageExpression)
        {
            try
            {
                // 解析表达式树获取属性名
                if (messageExpression.Body is MemberExpression memberExpression)
                {
                    return memberExpression.Member.Name;
                }
                
                return "UnknownResource";
            }
            catch
            {
                return "UnknownResource";
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