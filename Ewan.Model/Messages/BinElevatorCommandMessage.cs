using EwanCore.Messaging;
using System;

namespace Ewan.Model.Messages
{
    /// <summary>
    /// 料仓升降命令类型
    /// </summary>
    public enum BinElevatorCommand
    {
        /// <summary>
        /// 上升
        /// </summary>
        RaiseUp,

        /// <summary>
        /// 下降
        /// </summary>
        Lower,

        /// <summary>
        /// 停止
        /// </summary>
        Stop,

        /// <summary>
        /// 初始化
        /// </summary>
        Initialize
    }

    /// <summary>
    /// 料仓升降控制消息 - 用于料仓升降控制的强类型消息
    /// </summary>
    public sealed class BinElevatorCommandMessage : IMessage
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
        /// 控制命令
        /// </summary>
        public BinElevatorCommand Command { get; }

        /// <summary>
        /// 目标位置（可选）
        /// </summary>
        public double? TargetPosition { get; }

        /// <summary>
        /// 命令来源
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 创建料仓升降控制消息
        /// </summary>
        public BinElevatorCommandMessage(int binNumber, BinElevatorCommand command, double? targetPosition = null, string source = null)
        {
            BinNumber = binNumber;
            Command = command;
            TargetPosition = targetPosition;
            Source = source ?? string.Empty;
        }

        /// <summary>
        /// 创建上升命令
        /// </summary>
        public static BinElevatorCommandMessage RaiseUp(int binNumber, string source = null)
            => new BinElevatorCommandMessage(binNumber, BinElevatorCommand.RaiseUp, null, source);

        /// <summary>
        /// 创建下降命令
        /// </summary>
        public static BinElevatorCommandMessage Lower(int binNumber, string source = null)
            => new BinElevatorCommandMessage(binNumber, BinElevatorCommand.Lower, null, source);

        /// <summary>
        /// 创建停止命令
        /// </summary>
        public static BinElevatorCommandMessage Stop(int binNumber, string source = null)
            => new BinElevatorCommandMessage(binNumber, BinElevatorCommand.Stop, null, source);

        /// <summary>
        /// 创建初始化命令
        /// </summary>
        public static BinElevatorCommandMessage Initialize(int binNumber, string source = null)
            => new BinElevatorCommandMessage(binNumber, BinElevatorCommand.Initialize, null, source);
    }
}
