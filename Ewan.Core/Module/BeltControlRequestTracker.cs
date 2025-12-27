using Ewan.Model.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 皮带控制请求追踪器
    /// 线程安全地管理来自不同模块的停止请求
    /// </summary>
    public sealed class BeltControlRequestTracker
    {
        #region 私有字段

        private readonly object _lock = new object();
        private readonly HashSet<BeltConveyorControlSource> _activeRequests = new HashSet<BeltConveyorControlSource>();
        private readonly Dictionary<BeltConveyorControlSource, string> _requestReasons = new Dictionary<BeltConveyorControlSource, string>();

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否有任何活跃的停止请求
        /// </summary>
        public bool HasAnyStopRequest
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.Count > 0;
                }
            }
        }

        /// <summary>
        /// 当前活跃请求数量
        /// </summary>
        public int ActiveRequestCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.Count;
                }
            }
        }

        /// <summary>
        /// 获取所有活跃请求的来源（只读副本）
        /// </summary>
        public IReadOnlyList<BeltConveyorControlSource> ActiveRequests
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.ToList().AsReadOnly();
                }
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 请求或释放皮带控制
        /// </summary>
        /// <param name="source">请求来源</param>
        /// <param name="stopRequested">true=请求停止, false=释放控制</param>
        /// <param name="reason">原因说明（可选）</param>
        /// <returns>当前是否有任何活跃的停止请求</returns>
        public bool Request(BeltConveyorControlSource source, bool stopRequested, string reason = null)
        {
            lock (_lock)
            {
                if (stopRequested)
                {
                    _activeRequests.Add(source);
                    _requestReasons[source] = reason ?? string.Empty;
                }
                else
                {
                    _activeRequests.Remove(source);
                    _requestReasons.Remove(source);
                }

                return _activeRequests.Count > 0;
            }
        }

        /// <summary>
        /// 处理皮带控制消息
        /// </summary>
        /// <param name="message">控制消息</param>
        /// <returns>当前是否有任何活跃的停止请求</returns>
        public bool ProcessMessage(BeltConveyorControlMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Request(message.Source, message.StopRequested, message.Reason);
        }

        /// <summary>
        /// 检查指定来源是否有活跃请求
        /// </summary>
        /// <param name="source">请求来源</param>
        /// <returns>是否有活跃请求</returns>
        public bool HasRequestFrom(BeltConveyorControlSource source)
        {
            lock (_lock)
            {
                return _activeRequests.Contains(source);
            }
        }

        /// <summary>
        /// 获取指定来源的请求原因
        /// </summary>
        /// <param name="source">请求来源</param>
        /// <returns>原因说明，如果无请求则返回null</returns>
        public string GetReasonFor(BeltConveyorControlSource source)
        {
            lock (_lock)
            {
                if (_requestReasons.TryGetValue(source, out var reason))
                {
                    return reason;
                }
                return null;
            }
        }

        /// <summary>
        /// 获取所有活跃请求的详细信息
        /// </summary>
        /// <returns>来源与原因的字典</returns>
        public Dictionary<BeltConveyorControlSource, string> GetAllActiveRequestDetails()
        {
            lock (_lock)
            {
                return new Dictionary<BeltConveyorControlSource, string>(_requestReasons);
            }
        }

        /// <summary>
        /// 清除所有请求
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _activeRequests.Clear();
                _requestReasons.Clear();
            }
        }

        /// <summary>
        /// 强制释放指定来源的请求
        /// </summary>
        /// <param name="source">请求来源</param>
        /// <returns>是否存在该请求（true=之前存在并已移除）</returns>
        public bool ForceRelease(BeltConveyorControlSource source)
        {
            lock (_lock)
            {
                bool existed = _activeRequests.Remove(source);
                _requestReasons.Remove(source);
                return existed;
            }
        }

        /// <summary>
        /// 获取诊断信息
        /// </summary>
        /// <returns>当前状态的诊断字符串</returns>
        public string GetDiagnosticInfo()
        {
            lock (_lock)
            {
                if (_activeRequests.Count == 0)
                {
                    return "无活跃停止请求";
                }

                var details = _requestReasons
                    .Select(kvp => $"  - {kvp.Key}: {(string.IsNullOrEmpty(kvp.Value) ? "无原因" : kvp.Value)}")
                    .ToList();

                return $"活跃停止请求 ({_activeRequests.Count}):\n" + string.Join("\n", details);
            }
        }

        #endregion
    }
}
