using System;

namespace Ewan.Mes.Diagnostics
{
    /// <summary>
    /// 连接状态变更事件参数
    /// </summary>
    public class MesConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 连接状态
        /// </summary>
        public ConnectionState State { get; set; }

        /// <summary>
        /// 事件来源（服务名称）
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 消息说明
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 断线原因（断线时有效）
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 事件发生时间
        /// </summary>
        public DateTime Timestamp { get; set; }

        public MesConnectionEventArgs()
        {
            Timestamp = DateTime.Now;
        }

        public MesConnectionEventArgs(string source, ConnectionState state, string message = null, Exception ex = null)
            : this()
        {
            Source = source;
            State = state;
            IsConnected = state == ConnectionState.Connected;
            Message = message;
            Exception = ex;
        }
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// 已断开
        /// </summary>
        Disconnected,

        /// <summary>
        /// 连接中
        /// </summary>
        Connecting,

        /// <summary>
        /// 已连接
        /// </summary>
        Connected,

        /// <summary>
        /// 重连中
        /// </summary>
        Reconnecting,

        /// <summary>
        /// 连接失败
        /// </summary>
        Failed
    }
}
