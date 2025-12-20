using System;

namespace Ewan.Mes.Diagnostics
{
    /// <summary>
    /// MES 错误事件参数
    /// </summary>
    public class MesErrorEventArgs : EventArgs
    {
        /// <summary>
        /// 错误发生的模块/来源
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 异常对象（可能为空）
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 错误发生时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 错误级别
        /// </summary>
        public ErrorLevel Level { get; set; }

        /// <summary>
        /// 相关数据（可选）
        /// </summary>
        public object Data { get; set; }

        public MesErrorEventArgs()
        {
            Timestamp = DateTime.Now;
            Level = ErrorLevel.Error;
        }

        public MesErrorEventArgs(string source, string message, Exception ex = null)
            : this()
        {
            Source = source;
            Message = message;
            Exception = ex;
        }
    }

    /// <summary>
    /// 错误级别
    /// </summary>
    public enum ErrorLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }
}
