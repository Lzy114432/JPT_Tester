using EwanCore.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 皮带状态变化类型
    /// </summary>
    public enum BeltStatusChangeType
    {
        /// <summary>
        /// 状态更新（一般性状态变化）
        /// </summary>
        StatusUpdate = 0,

        /// <summary>
        /// 皮带启动
        /// </summary>
        Started = 1,

        /// <summary>
        /// 皮带停止（由业务请求触发）
        /// </summary>
        Stopped = 2,

        /// <summary>
        /// 系统级停止（急停、暂停等）
        /// </summary>
        SystemStopped = 3,

        /// <summary>
        /// 模块禁用
        /// </summary>
        ModuleDisabled = 4
    }

    /// <summary>
    /// 皮带输送机状态消息
    /// 用于广播皮带当前状态，供其他模块订阅
    /// </summary>
    public sealed class BeltConveyorStatusMessage : IMessage
    {
        #region IMessage 实现

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        #endregion

        #region 状态属性

        /// <summary>
        /// 皮带是否正在运行
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// 当前活跃的停止请求来源列表
        /// </summary>
        public IReadOnlyList<BeltConveyorControlSource> ActiveStopRequests { get; }

        /// <summary>
        /// 最后一次状态变化的原因
        /// </summary>
        public string LastChangeReason { get; }

        /// <summary>
        /// 状态变化类型
        /// </summary>
        public BeltStatusChangeType ChangeType { get; }

        /// <summary>
        /// 是否因系统级命令停止（急停、暂停等）
        /// </summary>
        public bool IsSystemStopped { get; }

        /// <summary>
        /// 模块是否启用
        /// </summary>
        public bool IsModuleEnabled { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建皮带状态消息
        /// </summary>
        /// <param name="isRunning">是否运行中</param>
        /// <param name="activeRequests">活跃的停止请求来源</param>
        /// <param name="reason">状态变化原因</param>
        /// <param name="changeType">变化类型</param>
        /// <param name="isSystemStopped">是否系统级停止</param>
        /// <param name="isModuleEnabled">模块是否启用</param>
        public BeltConveyorStatusMessage(
            bool isRunning,
            IEnumerable<BeltConveyorControlSource> activeRequests = null,
            string reason = null,
            BeltStatusChangeType changeType = BeltStatusChangeType.StatusUpdate,
            bool isSystemStopped = false,
            bool isModuleEnabled = true)
        {
            Timestamp = DateTimeOffset.Now;
            IsRunning = isRunning;
            ActiveStopRequests = activeRequests?.ToList().AsReadOnly()
                ?? new List<BeltConveyorControlSource>().AsReadOnly();
            LastChangeReason = reason ?? string.Empty;
            ChangeType = changeType;
            IsSystemStopped = isSystemStopped;
            IsModuleEnabled = isModuleEnabled;
        }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建"已启动"状态消息
        /// </summary>
        /// <param name="reason">启动原因</param>
        /// <returns>状态消息</returns>
        public static BeltConveyorStatusMessage Started(string reason = null)
        {
            return new BeltConveyorStatusMessage(
                isRunning: true,
                activeRequests: null,
                reason: reason ?? "皮带启动",
                changeType: BeltStatusChangeType.Started,
                isSystemStopped: false,
                isModuleEnabled: true);
        }

        /// <summary>
        /// 创建"已停止"状态消息（业务请求触发）
        /// </summary>
        /// <param name="activeRequests">活跃的停止请求</param>
        /// <param name="reason">停止原因</param>
        /// <returns>状态消息</returns>
        public static BeltConveyorStatusMessage Stopped(
            IEnumerable<BeltConveyorControlSource> activeRequests,
            string reason = null)
        {
            return new BeltConveyorStatusMessage(
                isRunning: false,
                activeRequests: activeRequests,
                reason: reason ?? "皮带停止",
                changeType: BeltStatusChangeType.Stopped,
                isSystemStopped: false,
                isModuleEnabled: true);
        }

        /// <summary>
        /// 创建"系统级停止"状态消息
        /// </summary>
        /// <param name="reason">停止原因（如急停、暂停）</param>
        /// <returns>状态消息</returns>
        public static BeltConveyorStatusMessage SystemStopped(string reason)
        {
            return new BeltConveyorStatusMessage(
                isRunning: false,
                activeRequests: null,
                reason: reason,
                changeType: BeltStatusChangeType.SystemStopped,
                isSystemStopped: true,
                isModuleEnabled: true);
        }

        /// <summary>
        /// 创建"模块禁用"状态消息
        /// </summary>
        /// <returns>状态消息</returns>
        public static BeltConveyorStatusMessage ModuleDisabled()
        {
            return new BeltConveyorStatusMessage(
                isRunning: false,
                activeRequests: null,
                reason: "模块已禁用",
                changeType: BeltStatusChangeType.ModuleDisabled,
                isSystemStopped: false,
                isModuleEnabled: false);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取状态描述
        /// </summary>
        /// <returns>人类可读的状态描述</returns>
        public string GetStatusDescription()
        {
            if (!IsModuleEnabled)
            {
                return "模块禁用";
            }

            if (IsSystemStopped)
            {
                return $"系统停止: {LastChangeReason}";
            }

            if (IsRunning)
            {
                return "运行中";
            }

            if (ActiveStopRequests.Count > 0)
            {
                var sources = string.Join(", ", ActiveStopRequests);
                return $"停止 (请求来源: {sources})";
            }

            return "已停止";
        }

        #endregion
    }
}
