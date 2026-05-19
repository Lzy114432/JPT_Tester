using EwanCore.Messaging;
using System;

namespace Ewan.Model.Production
{
    /// <summary>
    /// 料仓升降控制指令
    /// </summary>
    public enum BinCommand
    {
        /// <summary>
        /// 初始化所有料仓（需要回复）
        /// </summary>
        Initialize,

        /// <summary>
        /// 上升到感应位置（需要轴号，需要回复）
        /// </summary>
        RaiseToSensor,

        /// <summary>
        /// 装料完成，下降一格（需要轴号，不需要回复）
        /// </summary>
        LoadingCompleted,

        /// <summary>
        /// 强制停止所有料仓（不需要回复）
        /// </summary>
        ForceStopAll,
            /// <summary>
        /// 移动（不需要回复）
        /// </summary>
        MoveRelative
    }

    /// <summary>
    /// 料仓升降执行状态
    /// </summary>
    public enum BinExecuteState
    {
        /// <summary>
        /// 空闲状态
        /// </summary>
        Idle,

        /// <summary>
        /// 执行中
        /// </summary>
        Executing,

        /// <summary>
        /// 执行完成
        /// </summary>
        Completed,

        /// <summary>
        /// 执行出错
        /// </summary>
        Error
    }

    /// <summary>
    /// 料仓操作结果
    /// </summary>
    public enum BinOperationResult
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success,

        /// <summary>
        /// 有料
        /// </summary>
        HasMaterial,

        /// <summary>
        /// 无料
        /// </summary>
        NoMaterial,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 错误
        /// </summary>
        Error
    }

    /// <summary>
    /// 料仓升降控制指令消息
    /// </summary>
    public class BinElevatorCommandMessage : IMessage, ICorrelatedMessage<Guid>
    {
        /// <summary>
        /// 关联ID，用于匹配请求和响应
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// 料仓编号（1, 2, 3）
        /// </summary>
        public int BinNumber { get; set; }

        /// <summary>
        /// 控制指令
        /// </summary>
        public BinCommand Command { get; set; }
        public double Distance { get; set; }
        /// <summary>
        /// 指令ID（用于跟踪指令执行）
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// 指令来源
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 指令描述
        /// </summary>
        public string Description { get; set; }

        public BinElevatorCommandMessage()
        {
            Timestamp = DateTimeOffset.Now;
            CommandId = Guid.NewGuid().ToString();
            Source = string.Empty;
        }

        public BinElevatorCommandMessage(int binNumber, BinCommand command, string description = "")
        {
            BinNumber = binNumber;
            Command = command;
            Description = description;
            Timestamp = DateTimeOffset.Now;
            CommandId = Guid.NewGuid().ToString();
            Source = string.Empty;
        }

        public BinElevatorCommandMessage(int binNumber, BinCommand command, string description, string source)
            : this(binNumber, command, description)
        {
            Source = source ?? string.Empty;
        }

        /// <summary>
        /// 上升到感应位置（请求/响应）
        /// </summary>
        public static BinElevatorCommandMessage RaiseToSensor(int binNumber, string source)
            => new BinElevatorCommandMessage(binNumber, BinCommand.RaiseToSensor, string.Empty, source);

        /// <summary>
        /// 初始化全部料仓
        /// </summary>
        public static BinElevatorCommandMessage InitializeAll(string source)
            => new BinElevatorCommandMessage(0, BinCommand.Initialize, string.Empty, source);

        /// <summary>
        /// 强制停止全部料仓
        /// </summary>
        public static BinElevatorCommandMessage ForceStopAll(string source)
            => new BinElevatorCommandMessage(0, BinCommand.ForceStopAll, string.Empty, source);

        /// <summary>
        /// 装料完成，指定料仓下降一格
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        /// <param name="source">消息来源</param>
        public static BinElevatorCommandMessage LoadingCompleted(int binNumber, string source)
            => new BinElevatorCommandMessage(binNumber, BinCommand.LoadingCompleted, string.Empty, source);

        public static BinElevatorCommandMessage MoveRelative(int binNumber, double distance, string source = "")
        {
            return new BinElevatorCommandMessage
            {
                Command = BinCommand.MoveRelative,
                BinNumber = binNumber,
                Distance = distance,
                Source = source
            };
        }
    }

    /// <summary>
    /// 料仓升降状态反馈消息
    /// </summary>
    public class BinElevatorStatusMessage : IMessage, ICorrelatedMessage<Guid>
    {
        /// <summary>
        /// 关联ID，用于匹配请求和响应
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// 料仓编号（1, 2, 3）
        /// </summary>
        public int BinNumber { get; set; }

        /// <summary>
        /// 执行状态
        /// </summary>
        public BinExecuteState State { get; set; }

        /// <summary>
        /// 当前执行的指令
        /// </summary>
        public BinCommand CurrentCommand { get; set; }

        /// <summary>
        /// 指令ID
        /// </summary>
        public string CommandId { get; set; }

        /// <summary>
        /// 感应器状态
        /// </summary>
        public bool HasMaterial { get; set; }

        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// 操作结果
        /// </summary>
        public BinOperationResult OperationResult { get; set; }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 错误信息（当State为Error时）
        /// </summary>
        public string ErrorMessage { get; set; }

        public BinElevatorStatusMessage()
        {
            Timestamp = DateTimeOffset.Now;
            OperationResult = BinOperationResult.Success;
        }

        public BinElevatorStatusMessage(int binNumber, BinExecuteState state, BinCommand currentCommand, string commandId = "", string description = "")
        {
            BinNumber = binNumber;
            State = state;
            CurrentCommand = currentCommand;
            CommandId = commandId;
            Description = description;
            Timestamp = DateTimeOffset.Now;
            OperationResult = BinOperationResult.Success;
        }

        /// <summary>
        /// 初始化完成结果
        /// </summary>
        public static BinElevatorStatusMessage InitializeResult(
            BinOperationResult result,
            string description = "",
            string errorMessage = "")
        {
            var state = result == BinOperationResult.Error
                ? BinExecuteState.Error
                : BinExecuteState.Completed;

            return new BinElevatorStatusMessage(0, state, BinCommand.Initialize, string.Empty, description)
            {
                OperationResult = result,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 物料检测结果
        /// </summary>
        public static BinElevatorStatusMessage MaterialCheckResult(int binNumber, BinOperationResult result, string description = "", string errorMessage = "")
        {
            var state = result == BinOperationResult.Error ? BinExecuteState.Error : BinExecuteState.Completed;
            return new BinElevatorStatusMessage(binNumber, state, BinCommand.RaiseToSensor, string.Empty, description)
            {
                OperationResult = result,
                HasMaterial = result == BinOperationResult.HasMaterial,
                ErrorMessage = errorMessage
            };
        }
    }
}
