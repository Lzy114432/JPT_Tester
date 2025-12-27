using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 装卸操作类型
    /// </summary>
    public enum LoadingUnloadingOperation
    {
        /// <summary>
        /// 上料
        /// </summary>
        Loading,

        /// <summary>
        /// 下料
        /// </summary>
        Unloading
    }

    /// <summary>
    /// 装卸状态
    /// </summary>
    public enum LoadingUnloadingState
    {
        /// <summary>
        /// 空闲
        /// </summary>
        Idle,

        /// <summary>
        /// 准备中
        /// </summary>
        Preparing,

        /// <summary>
        /// 进行中
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 失败
        /// </summary>
        Failed,

        /// <summary>
        /// 等待中
        /// </summary>
        Waiting
    }

    /// <summary>
    /// 装卸状态消息 - 用于装卸状态通知的强类型消息
    /// </summary>
    public sealed class LoadingUnloadingStateMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 操作类型
        /// </summary>
        public LoadingUnloadingOperation Operation { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public LoadingUnloadingState State { get; }

        /// <summary>
        /// 料仓编号（如果相关）
        /// </summary>
        public int? BinNumber { get; }

        /// <summary>
        /// 物料信息
        /// </summary>
        public string MaterialInfo { get; }

        /// <summary>
        /// 错误信息（如果状态为失败）
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 创建装卸状态消息
        /// </summary>
        public LoadingUnloadingStateMessage(LoadingUnloadingOperation operation, LoadingUnloadingState state, int? binNumber = null, string materialInfo = null, string errorMessage = null)
        {
            Operation = operation;
            State = state;
            BinNumber = binNumber;
            MaterialInfo = materialInfo ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// 创建上料进行中消息
        /// </summary>
        public static LoadingUnloadingStateMessage LoadingInProgress(int binNumber, string materialInfo = null)
            => new LoadingUnloadingStateMessage(LoadingUnloadingOperation.Loading, LoadingUnloadingState.InProgress, binNumber, materialInfo);

        /// <summary>
        /// 创建上料完成消息
        /// </summary>
        public static LoadingUnloadingStateMessage LoadingCompleted(int binNumber, string materialInfo = null)
            => new LoadingUnloadingStateMessage(LoadingUnloadingOperation.Loading, LoadingUnloadingState.Completed, binNumber, materialInfo);

        /// <summary>
        /// 创建下料进行中消息
        /// </summary>
        public static LoadingUnloadingStateMessage UnloadingInProgress(int binNumber, string materialInfo = null)
            => new LoadingUnloadingStateMessage(LoadingUnloadingOperation.Unloading, LoadingUnloadingState.InProgress, binNumber, materialInfo);

        /// <summary>
        /// 创建下料完成消息
        /// </summary>
        public static LoadingUnloadingStateMessage UnloadingCompleted(int binNumber, string materialInfo = null)
            => new LoadingUnloadingStateMessage(LoadingUnloadingOperation.Unloading, LoadingUnloadingState.Completed, binNumber, materialInfo);

        /// <summary>
        /// 创建失败消息
        /// </summary>
        public static LoadingUnloadingStateMessage Failed(LoadingUnloadingOperation operation, string errorMessage, int? binNumber = null)
            => new LoadingUnloadingStateMessage(operation, LoadingUnloadingState.Failed, binNumber, null, errorMessage);
    }
}
