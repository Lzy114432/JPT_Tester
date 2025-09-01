using log4net;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Resources;

namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// 基础文件日志记录器
    /// </summary>
    public class FileLogger
    {
        private readonly ILog _log;
        private readonly ResourceManager _resourceManager;
        private readonly Type _resourceType;
        protected readonly string _loggerName;

        /// <summary>
        /// 创建文件日志记录器
        /// </summary>
        /// <param name="loggerName">日志记录器名称（用于log4net配置）</param>
        /// <param name="resourceType">资源类型（用于国际化）</param>
        public FileLogger(string loggerName, Type resourceType = null)
        {
            _loggerName = loggerName;
            _log = LogManager.GetLogger(loggerName);
            _resourceType = resourceType;
            
            if (resourceType != null)
            {
                _resourceManager = new ResourceManager(resourceType);
            }
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        /// <param name="message">消息</param>
        public virtual void Debug(string message)
        {
            LogWithCallerInfo(LogLevel.Debug, message);
        }

        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        /// <param name="message">消息</param>
        public virtual void Info(string message)
        {
            LogWithCallerInfo(LogLevel.Info, message);
        }

        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        /// <param name="message">消息</param>
        public virtual void Warn(string message)
        {
            LogWithCallerInfo(LogLevel.Warn, message);
        }

        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        /// <param name="message">消息</param>
        public virtual void Error(string message)
        {
            LogWithCallerInfo(LogLevel.Error, message);
        }

        /// <summary>
        /// 记录错误级别日志（带异常）
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="exception">异常</param>
        public virtual void Error(string message, Exception exception)
        {
            var callerInfo = GetCallerInfo();
            var logMessage = $"[{callerInfo}] {message}";
            _log.Error(logMessage, exception);
        }

        /// <summary>
        /// 记录致命错误级别日志
        /// </summary>
        /// <param name="message">消息</param>
        public virtual void Fatal(string message)
        {
            LogWithCallerInfo(LogLevel.Fatal, message);
        }

        /// <summary>
        /// 记录致命错误级别日志（带异常）
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="exception">异常</param>
        public virtual void Fatal(string message, Exception exception)
        {
            var callerInfo = GetCallerInfo();
            var logMessage = $"[{callerInfo}] {message}";
            _log.Fatal(logMessage, exception);
        }

        /// <summary>
        /// 记录国际化消息
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        public virtual void LogLocalized(LogLevel level, string messageKey, params object[] parameters)
        {
            if (_resourceManager == null)
            {
                LogWithCallerInfo(level, $"[{messageKey}] No resource manager configured");
                return;
            }

            try
            {
                var localizedMessage = GetLocalizedMessage(messageKey, parameters);
                var callerInfo = GetCallerInfo();
                var logMessage = $"[{callerInfo}] [{messageKey}] {localizedMessage}";
                LogRaw(level, logMessage);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to log localized message for key: {messageKey}", ex);
            }
        }

        /// <summary>
        /// 获取本地化消息
        /// </summary>
        /// <param name="messageKey">消息键</param>
        /// <param name="parameters">参数</param>
        /// <returns>本地化后的消息</returns>
        public virtual string GetLocalizedMessage(string messageKey, params object[] parameters)
        {
            if (_resourceManager == null)
            {
                return $"[Missing Resource Manager: {messageKey}]";
            }

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
                _log.Error($"Failed to get localized message for key: {messageKey}", ex);
                return $"[Resource Error: {messageKey}]";
            }
        }

        /// <summary>
        /// 获取当前文化设置
        /// </summary>
        /// <returns>当前文化设置</returns>
        protected virtual CultureInfo GetCurrentCulture()
        {
            // 默认使用当前UI文化，子类可以重写此方法来提供自定义的文化获取逻辑
            return CultureInfo.CurrentUICulture;
        }

        /// <summary>
        /// 记录带调用者信息的日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        public void LogWithCallerInfo(LogLevel level, string message)
        {
            var callerInfo = GetCallerInfo();
            var logMessage = $"[{callerInfo}] {message}";
            LogRaw(level, logMessage);
        }

        /// <summary>
        /// 记录原始日志
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <param name="message">消息</param>
        public void LogRaw(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    _log.Debug(message);
                    break;
                case LogLevel.Info:
                    _log.Info(message);
                    break;
                case LogLevel.Warn:
                    _log.Warn(message);
                    break;
                case LogLevel.Error:
                    _log.Error(message);
                    break;
                case LogLevel.Fatal:
                    _log.Fatal(message);
                    break;
            }
        }

        /// <summary>
        /// 获取调用者信息（类名:行号）
        /// </summary>
        /// <returns>调用者信息字符串</returns>
        protected virtual string GetCallerInfo()
        {
            try
            {
                var stackTrace = new StackTrace(true);
                
                // 遍历调用栈，找到第一个不是Logger类的调用者
                for (int i = 1; i < stackTrace.FrameCount; i++)
                {
                    var frame = stackTrace.GetFrame(i);
                    var method = frame.GetMethod();
                    var declaringType = method.DeclaringType;
                    
                    // 跳过所有Logger相关的类
                    if (declaringType != null &&
                        !declaringType.Name.EndsWith("Logger") &&
                        !declaringType.Namespace.Contains("LogManager"))
                    {
                        var className = declaringType.FullName;
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