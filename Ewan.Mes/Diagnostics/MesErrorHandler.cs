using System;

namespace Ewan.Mes.Diagnostics
{
    /// <summary>
    /// MES 库全局错误事件管理器
    /// 外部可订阅此事件来接收库内部的错误通知
    /// </summary>
    public static class MesErrorHandler
    {
        /// <summary>
        /// 错误发生事件，外部订阅此事件进行日志记录
        /// </summary>
        public static event EventHandler<MesErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// 触发错误事件
        /// </summary>
        internal static void RaiseError(string source, string message, Exception ex = null, ErrorLevel level = ErrorLevel.Error, object data = null)
        {
            var handler = ErrorOccurred;
            if (handler == null) return;

            var args = new MesErrorEventArgs
            {
                Source = source,
                Message = message,
                Exception = ex,
                Level = level,
                Data = data,
                Timestamp = DateTime.Now
            };

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler<MesErrorEventArgs>)subscriber)(null, args);
                }
                catch
                {
                    // 忽略订阅者异常，避免影响其他订阅者和库逻辑
                }
            }
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        internal static void RaiseError(MesErrorEventArgs args)
        {
            var handler = ErrorOccurred;
            if (handler == null) return;

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler<MesErrorEventArgs>)subscriber)(null, args);
                }
                catch
                {
                    // 忽略订阅者异常，避免影响其他订阅者和库逻辑
                }
            }
        }

        /// <summary>
        /// 触发 Debug 级别事件
        /// </summary>
        internal static void Debug(string source, string message, object data = null)
        {
            RaiseError(source, message, null, ErrorLevel.Debug, data);
        }

        /// <summary>
        /// 触发 Info 级别事件
        /// </summary>
        internal static void Info(string source, string message, object data = null)
        {
            RaiseError(source, message, null, ErrorLevel.Info, data);
        }

        /// <summary>
        /// 触发 Warning 级别事件
        /// </summary>
        internal static void Warning(string source, string message, Exception ex = null, object data = null)
        {
            RaiseError(source, message, ex, ErrorLevel.Warning, data);
        }

        /// <summary>
        /// 触发 Error 级别事件
        /// </summary>
        internal static void Error(string source, string message, Exception ex = null, object data = null)
        {
            RaiseError(source, message, ex, ErrorLevel.Error, data);
        }

        /// <summary>
        /// 触发 Fatal 级别事件
        /// </summary>
        internal static void Fatal(string source, string message, Exception ex = null, object data = null)
        {
            RaiseError(source, message, ex, ErrorLevel.Fatal, data);
        }
    }
}
