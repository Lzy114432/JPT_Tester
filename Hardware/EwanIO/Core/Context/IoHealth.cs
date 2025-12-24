using System;
using System.Diagnostics;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IO 健康状态
    /// </summary>
    public class IoHealth
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; internal set; }

        /// <summary>
        /// 最后一次错误信息
        /// </summary>
        public string? LastError { get; internal set; }

        /// <summary>
        /// 最后一次错误时间
        /// </summary>
        public DateTime? LastErrorTime { get; internal set; }

        /// <summary>
        /// 最后一次 Tick 耗时（毫秒）
        /// </summary>
        public double LastTickMs { get; internal set; }

        /// <summary>
        /// 平均 Tick 耗时（毫秒）
        /// </summary>
        public double AverageTickMs { get; internal set; }

        /// <summary>
        /// 最大 Tick 耗时（毫秒）
        /// </summary>
        public double MaxTickMs { get; internal set; }

        /// <summary>
        /// 总 Tick 次数
        /// </summary>
        public long TotalTicks { get; internal set; }

        /// <summary>
        /// 超时次数（Wait/Confirm 超时）
        /// </summary>
        public int TimeoutCount { get; internal set; }

        /// <summary>
        /// 上次连接时间
        /// </summary>
        public DateTime? LastConnectTime { get; internal set; }

        /// <summary>
        /// 运行时长
        /// </summary>
        public TimeSpan Uptime => LastConnectTime.HasValue
            ? DateTime.UtcNow - LastConnectTime.Value
            : TimeSpan.Zero;

        /// <summary>
        /// 是否健康（已连接且无最近错误）
        /// </summary>
        public bool IsHealthy => IsConnected &&
            (!LastErrorTime.HasValue || (DateTime.UtcNow - LastErrorTime.Value).TotalMinutes > 5);

        internal IoHealth()
        {
            IsConnected = false;
            LastTickMs = 0;
            AverageTickMs = 0;
            MaxTickMs = 0;
            TotalTicks = 0;
            TimeoutCount = 0;
        }

        /// <summary>
        /// 记录 Tick 性能
        /// </summary>
        internal void RecordTick(double elapsedMs)
        {
            LastTickMs = elapsedMs;
            TotalTicks++;

            // 更新最大值
            if (elapsedMs > MaxTickMs)
                MaxTickMs = elapsedMs;

            // 更新平均值（滑动平均）
            if (TotalTicks == 1)
                AverageTickMs = elapsedMs;
            else
                AverageTickMs = (AverageTickMs * 0.95) + (elapsedMs * 0.05);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        internal void RecordError(string error)
        {
            LastError = error;
            LastErrorTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 记录超时
        /// </summary>
        internal void RecordTimeout()
        {
            TimeoutCount++;
        }

        /// <summary>
        /// 记录连接
        /// </summary>
        internal void RecordConnect()
        {
            IsConnected = true;
            LastConnectTime = DateTime.UtcNow;
        }

        /// <summary>
        /// 记录断开
        /// </summary>
        internal void RecordDisconnect()
        {
            IsConnected = false;
        }

        /// <summary>
        /// 重置统计信息
        /// </summary>
        public void ResetStatistics()
        {
            LastTickMs = 0;
            AverageTickMs = 0;
            MaxTickMs = 0;
            TotalTicks = 0;
            TimeoutCount = 0;
            LastError = null;
            LastErrorTime = null;
        }

        /// <summary>
        /// 获取健康报告
        /// </summary>
        public string GetHealthReport()
        {
            return $@"IoHealth Report:
  Connected: {IsConnected}
  Healthy: {IsHealthy}
  Uptime: {Uptime:hh\:mm\:ss}
  Total Ticks: {TotalTicks}
  Last Tick: {LastTickMs:F2}ms
  Avg Tick: {AverageTickMs:F2}ms
  Max Tick: {MaxTickMs:F2}ms
  Timeouts: {TimeoutCount}
  Last Error: {LastError ?? "None"}
  Last Error Time: {LastErrorTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "None"}";
        }
    }

    /// <summary>
    /// IO 健康事件参数
    /// </summary>
    public class IoHealthEventArgs : EventArgs
    {
        public IoHealth Health { get; }
        public string EventType { get; }
        public string? Message { get; }

        internal IoHealthEventArgs(IoHealth health, string eventType, string? message = null)
        {
            Health = health;
            EventType = eventType;
            Message = message;
        }
    }
}
