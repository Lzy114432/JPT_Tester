using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 料仓状态
    /// </summary>
    public enum BinElevatorState
    {
        /// <summary>
        /// 未知状态
        /// </summary>
        Unknown,

        /// <summary>
        /// 已降低
        /// </summary>
        Lowered,

        /// <summary>
        /// 已升高
        /// </summary>
        Elevated,

        /// <summary>
        /// 正在移动
        /// </summary>
        Moving,

        /// <summary>
        /// 已停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 错误
        /// </summary>
        Error
    }

    /// <summary>
    /// 料仓升降状态消息 - 用于料仓状态反馈的强类型消息
    /// </summary>
    public sealed class BinElevatorStatusMessage : IMessage
    {
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        /// <summary>
        /// 料仓编号 (1-3)
        /// </summary>
        public int BinNumber { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public BinElevatorState State { get; }

        /// <summary>
        /// 当前位置（可选）
        /// </summary>
        public double? CurrentPosition { get; }

        /// <summary>
        /// 是否正在移动
        /// </summary>
        public bool IsMoving { get; }

        /// <summary>
        /// 感应器是否有信号（有料）
        /// </summary>
        public bool SensorActive { get; }

        /// <summary>
        /// 错误信息（如果有）
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 创建料仓状态消息
        /// </summary>
        public BinElevatorStatusMessage(int binNumber, BinElevatorState state, double? currentPosition = null, bool isMoving = false, bool sensorActive = false, string errorMessage = null)
        {
            BinNumber = binNumber;
            State = state;
            CurrentPosition = currentPosition;
            IsMoving = isMoving;
            SensorActive = sensorActive;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>
        /// 创建正常状态消息
        /// </summary>
        public static BinElevatorStatusMessage Normal(int binNumber, BinElevatorState state, bool sensorActive = false)
            => new BinElevatorStatusMessage(binNumber, state, null, false, sensorActive);

        /// <summary>
        /// 创建移动中状态消息
        /// </summary>
        public static BinElevatorStatusMessage Moving(int binNumber, bool sensorActive = false)
            => new BinElevatorStatusMessage(binNumber, BinElevatorState.Moving, null, true, sensorActive);

        /// <summary>
        /// 创建错误状态消息
        /// </summary>
        public static BinElevatorStatusMessage Error(int binNumber, string errorMessage)
            => new BinElevatorStatusMessage(binNumber, BinElevatorState.Error, null, false, false, errorMessage);
    }
}
