using Ewan.Core.Msg;
using Ewan.Model.Messages;
using log4net;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Resources;

namespace Ewan.Core.Logger
{
    /// <summary>
    /// 国际化界面日志记录器
    /// </summary>
    public class UILogger
    {
        private static readonly ILog s_techLog = LogManager.GetLogger(typeof(UILogger));
        private readonly ResourceManager _resourceManager;
        private readonly Type _resourceType;

        public UILogger(Type resourceType)
        {
            _resourceType = resourceType;
            _resourceManager = new ResourceManager(resourceType);
        }

        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Info(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Info, messageKey, parameters);
            LogToFile(LogLevel.Info, messageKey, parameters);
        }

        /// <summary>
        /// 记录信息级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Info(System.Linq.Expressions.Expression<System.Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Info, messageKey, parameters);
            LogToFile(LogLevel.Info, messageKey, parameters);
        }

        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Warn(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Warn, messageKey, parameters);
            LogToFile(LogLevel.Warn, messageKey, parameters);
        }

        /// <summary>
        /// 记录警告级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Warn(System.Linq.Expressions.Expression<System.Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Warn, messageKey, parameters);
            LogToFile(LogLevel.Warn, messageKey, parameters);
        }

        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Error(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Error, messageKey, parameters);
            LogToFile(LogLevel.Error, messageKey, parameters);
        }

        /// <summary>
        /// 记录错误级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Error(System.Linq.Expressions.Expression<System.Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Error, messageKey, parameters);
            LogToFile(LogLevel.Error, messageKey, parameters);
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Debug(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Debug, messageKey, parameters);
            LogToFile(LogLevel.Debug, messageKey, parameters);
        }

        /// <summary>
        /// 记录调试级别日志 - 使用资源表达式
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <param name="parameters">参数</param>
        public void Debug(System.Linq.Expressions.Expression<System.Func<string>> messageExpression, params object[] parameters)
        {
            var messageKey = GetResourceKeyFromExpression(messageExpression);
            LogToUI(LogLevel.Debug, messageKey, parameters);
            LogToFile(LogLevel.Debug, messageKey, parameters);
        }

        /// <summary>
        /// 记录致命错误级别日志
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public void Fatal(string messageKey, params object[] parameters)
        {
            LogToUI(LogLevel.Fatal, messageKey, parameters);
            LogToFile(LogLevel.Fatal, messageKey, parameters);
        }

        /// <summary>
        /// 记录原始消息日志（不国际化）
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="rawMessage">原始消息</param>
        public void LogRaw(LogLevel level, string rawMessage)
        {
            LogToUIRaw(level, rawMessage);
            LogToFileRaw(level, rawMessage);
        }


        /// <summary>
        /// 获取本地化消息
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        /// <returns>本地化后的消息</returns>
        public string GetLocalizedMessage(string messageKey, params object[] parameters)
        {
            try
            {
                var culture = GetCurrentCulture();
                var template = _resourceManager.GetString(messageKey, culture);
                if (string.IsNullOrEmpty(template))
                {
                    return $"[Missing Resource: {messageKey}]";
                }

                if (parameters != null && parameters.Length > 0)
                {
                    return string.Format(template, parameters);
                }

                return template;
            }
            catch (Exception ex)
            {
                s_techLog.Error($"Failed to get localized message for key: {messageKey}", ex);
                return $"[Resource Error: {messageKey}]";
            }
        }

        /// <summary>
        /// 获取当前文化设置
        /// </summary>
        /// <returns>当前文化设置</returns>
        private CultureInfo GetCurrentCulture()
        {
            try
            {
                return Culture.CultureManager.Instance().CurrentCulture;
            }
            catch
            {
                return CultureInfo.CurrentUICulture;
            }
        }

        /// <summary>
        /// 从资源表达式获取消息键
        /// </summary>
        /// <param name="messageExpression">资源表达式</param>
        /// <returns>消息键</returns>
        private string GetResourceKeyFromExpression(System.Linq.Expressions.Expression<System.Func<string>> messageExpression)
        {
            try
            {
                // 解析表达式树获取属性名
                if (messageExpression.Body is System.Linq.Expressions.MemberExpression memberExpression)
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


        private void LogToTech(LogLevel level, string messageKey, object[] parameters)
        {
            try
            {
                var message = GetLocalizedMessage(messageKey, parameters);
                LogToTechRaw(level, $"[{messageKey}] {message}");
            }
            catch (Exception ex)
            {
                s_techLog.Error($"Failed to log tech message for key: {messageKey}", ex);
            }
        }

        private void LogToTechRaw(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    s_techLog.Debug(message);
                    break;
                case LogLevel.Info:
                    s_techLog.Info(message);
                    break;
                case LogLevel.Warn:
                    s_techLog.Warn(message);
                    break;
                case LogLevel.Error:
                    s_techLog.Error(message);
                    break;
                case LogLevel.Fatal:
                    s_techLog.Fatal(message);
                    break;
            }
        }

        private void LogToUI(LogLevel level, string messageKey, object[] parameters)
        {
            try
            {
                var uiLogData = new UILogMsg(level, messageKey, parameters);
                var message = new MessageModel(MsgSubject.UILog, uiLogData);
                MsgManager.Instance().PushMsg(message);
            }
            catch (Exception ex)
            {
                s_techLog.Error($"Failed to push UI log message for key: {messageKey}", ex);
            }
        }

        private void LogToUIRaw(LogLevel level, string rawMessage)
        {
            try
            {
                var uiLogData = new UILogMsg(level, rawMessage);
                var message = new MessageModel(MsgSubject.UILog, uiLogData);
                MsgManager.Instance().PushMsg(message);
            }
            catch (Exception ex)
            {
                s_techLog.Error($"Failed to push raw UI log message: {rawMessage}", ex);
            }
        }

        /// <summary>
        /// 记录UI日志到app.log文件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        private void LogToFile(LogLevel level, string messageKey, object[] parameters)
        {
            try
            {
                // 获取调用者信息
                var callerInfo = GetCallerInfo();
                var localizedMessage = GetLocalizedMessage(messageKey, parameters);
                var logMessage = $"[{callerInfo}] [{messageKey}] {localizedMessage}";
                
                switch (level)
                {
                    case LogLevel.Debug:
                        s_techLog.Debug(logMessage);
                        break;
                    case LogLevel.Info:
                        s_techLog.Info(logMessage);
                        break;
                    case LogLevel.Warn:
                        s_techLog.Warn(logMessage);
                        break;
                    case LogLevel.Error:
                        s_techLog.Error(logMessage);
                        break;
                    case LogLevel.Fatal:
                        s_techLog.Fatal(logMessage);
                        break;
                }
            }
            catch (Exception ex)
            {
                s_techLog.Error($"Failed to write UI log to file for key: {messageKey}", ex);
            }
        }

        /// <summary>
        /// 记录原始UI日志到app.log文件
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="rawMessage">原始消息</param>
        private void LogToFileRaw(LogLevel level, string rawMessage)
        {
            try
            {
                // 获取调用者信息
                var callerInfo = GetCallerInfo();
                var logMessage = $"[{callerInfo}] {rawMessage}";
                
                switch (level)
                {
                    case LogLevel.Debug:
                        s_techLog.Debug(logMessage);
                        break;
                    case LogLevel.Info:
                        s_techLog.Info(logMessage);
                        break;
                    case LogLevel.Warn:
                        s_techLog.Warn(logMessage);
                        break;
                    case LogLevel.Error:
                        s_techLog.Error(logMessage);
                        break;
                    case LogLevel.Fatal:
                        s_techLog.Fatal(logMessage);
                        break;
                }
            }
            catch (Exception ex)
            {
                s_techLog.Error($"Failed to write raw UI log to file: {rawMessage}", ex);
            }
        }

        /// <summary>
        /// 获取调用者信息（类名:行号）
        /// </summary>
        /// <returns>调用者信息字符串</returns>
        private string GetCallerInfo()
        {
            try
            {
                var stackTrace = new StackTrace(true);
                
                // 遍历调用栈，找到第一个不是UILogger的调用者
                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    var frame = stackTrace.GetFrame(i);
                    var method = frame.GetMethod();
                    
                    if (method.DeclaringType != typeof(UILogger))
                    {
                        var className = method.DeclaringType.FullName;
                        var lineNumber = frame.GetFileLineNumber();
                        
                        if (lineNumber > 0)
                        {
                            return $"{className}:{lineNumber}";
                        }
                        else
                        {
                            return $"{className}:{method.Name}";
                        }
                    }
                }
                
                return "Unknown:0";
            }
            catch
            {
                return "Unknown:0";
            }
        }
    }
}