using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 环线数据消息 - 用于环线状态和数据的强类型消息
    /// </summary>
    public sealed class RingLineDataMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 是否正在上料
        /// </summary>
        public bool IsLoading { get; }

        /// <summary>
        /// 上升沿标志（False → True）
        /// </summary>
        public bool RisingEdge { get; }

        /// <summary>
        /// 下降沿标志（True → False）
        /// </summary>
        public bool FallingEdge { get; }

        /// <summary>
        /// 空车数量
        /// </summary>
        public int EmptyCarCount { get; }

        /// <summary>
        /// 切栈桥车数量
        /// </summary>
        public int CuttingBridgeCarCount { get; }

        /// <summary>
        /// 创建环线数据消息
        /// </summary>
        public RingLineDataMessage(bool isLoading, bool risingEdge, bool fallingEdge, int emptyCarCount, int cuttingBridgeCarCount)
        {
            IsLoading = isLoading;
            RisingEdge = risingEdge;
            FallingEdge = fallingEdge;
            EmptyCarCount = emptyCarCount;
            CuttingBridgeCarCount = cuttingBridgeCarCount;
        }

        /// <summary>
        /// 从 RingLineModel 创建消息
        /// </summary>
        public static RingLineDataMessage FromModel(RingLineModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            return new RingLineDataMessage(
                model.IsLoading,
                model.RisingEdge,
                model.FallingEdge,
                model.EmptyCarCount,
                model.CuttingBridgeCarCount);
        }
    }
}
