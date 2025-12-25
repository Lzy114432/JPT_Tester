using System;

namespace Ewan.Mes.Diagnostics
{
    /// <summary>
    /// MES 库全局连接状态监控
    /// 外部订阅此事件来监控连接状态变化
    /// </summary>
    public static class MesConnectionMonitor
    {
        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        public static event EventHandler<MesConnectionEventArgs> ConnectionStateChanged;

        /// <summary>
        /// 触发连接状态变更事件
        /// </summary>
        internal static void RaiseConnectionStateChanged(string source, ConnectionState state, string message = null, Exception ex = null)
        {
            var handler = ConnectionStateChanged;
            if (handler == null) return;

            var args = new MesConnectionEventArgs(source, state, message, ex);

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler<MesConnectionEventArgs>)subscriber)(null, args);
                }
                catch
                {
                    // 忽略订阅者异常，避免影响其他订阅者和库逻辑
                }
            }
        }

        /// <summary>
        /// 触发连接状态变更事件
        /// </summary>
        internal static void RaiseConnectionStateChanged(MesConnectionEventArgs args)
        {
            var handler = ConnectionStateChanged;
            if (handler == null) return;

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((EventHandler<MesConnectionEventArgs>)subscriber)(null, args);
                }
                catch
                {
                    // 忽略订阅者异常，避免影响其他订阅者和库逻辑
                }
            }
        }

        /// <summary>
        /// 触发已连接事件
        /// </summary>
        internal static void RaiseConnected(string source, string message = null)
        {
            RaiseConnectionStateChanged(source, ConnectionState.Connected, message ?? "已连接");
        }

        /// <summary>
        /// 触发断开连接事件
        /// </summary>
        internal static void RaiseDisconnected(string source, string message = null, Exception ex = null)
        {
            RaiseConnectionStateChanged(source, ConnectionState.Disconnected, message ?? "已断开", ex);
        }

        /// <summary>
        /// 触发连接中事件
        /// </summary>
        internal static void RaiseConnecting(string source, string message = null)
        {
            RaiseConnectionStateChanged(source, ConnectionState.Connecting, message ?? "连接中");
        }

        /// <summary>
        /// 触发重连中事件
        /// </summary>
        internal static void RaiseReconnecting(string source, string message = null)
        {
            RaiseConnectionStateChanged(source, ConnectionState.Reconnecting, message ?? "重连中");
        }

        /// <summary>
        /// 触发连接失败事件
        /// </summary>
        internal static void RaiseFailed(string source, string message = null, Exception ex = null)
        {
            RaiseConnectionStateChanged(source, ConnectionState.Failed, message ?? "连接失败", ex);
        }
    }
}
