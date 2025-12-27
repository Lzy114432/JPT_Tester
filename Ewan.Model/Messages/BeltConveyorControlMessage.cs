using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 皮带传送控制来源
    /// </summary>
    public enum BeltConveyorControlSource
    {
        /// <summary>
        /// 上料模块
        /// </summary>
        MaterialLoading,

        /// <summary>
        /// 下料模块
        /// </summary>
        MaterialUnloading
    }

    /// <summary>
    /// 皮带传送控制消息 - 用于控制皮带传送的强类型消息
    /// </summary>
    public sealed class BeltConveyorControlMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 控制来源
        /// </summary>
        public BeltConveyorControlSource Source { get; }

        /// <summary>
        /// 是否请求停止
        /// </summary>
        public bool StopRequested { get; }

        /// <summary>
        /// 停止/启动原因
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// 创建皮带传送控制消息
        /// </summary>
        public BeltConveyorControlMessage(BeltConveyorControlSource source, bool stopRequested, string reason = null)
        {
            Source = source;
            StopRequested = stopRequested;
            Reason = reason ?? string.Empty;
        }

        /// <summary>
        /// 创建停止请求消息
        /// </summary>
        public static BeltConveyorControlMessage Stop(BeltConveyorControlSource source, string reason = null)
            => new BeltConveyorControlMessage(source, true, reason);

        /// <summary>
        /// 创建释放控制消息（允许运行）
        /// </summary>
        public static BeltConveyorControlMessage Release(BeltConveyorControlSource source, string reason = null)
            => new BeltConveyorControlMessage(source, false, reason);
    }
}
