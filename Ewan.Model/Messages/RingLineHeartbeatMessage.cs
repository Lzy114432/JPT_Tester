using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 环线心跳消息 - 用于环线心跳状态的强类型消息
    /// </summary>
    public sealed class RingLineHeartbeatMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 心跳状态
        /// </summary>
        public int State { get; }

        /// <summary>
        /// 创建环线心跳消息
        /// </summary>
        public RingLineHeartbeatMessage(int state)
        {
            State = state;
        }

        /// <summary>
        /// 从 RingLineHeartbeatModel 创建消息
        /// </summary>
        public static RingLineHeartbeatMessage FromModel(RingLineHeartbeatModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return new RingLineHeartbeatMessage(model.State);
        }
    }
}
