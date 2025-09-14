using System;

namespace Ewan.Model.Production
{
    /// <summary>
    /// 料仓升降控制指令
    /// </summary>
    public enum BinCommand
    {
        /// <summary>
        /// 停止
        /// </summary>
        Stop,
        
        /// <summary>
        /// 上料位置（上升到感应位置，然后下降到无感应停止）
        /// </summary>
        FeedPosition,
        
        /// <summary>
        /// 下料位置（直接下降到底部）
        /// </summary>
        DownPosition
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
    /// 料仓升降控制指令消息
    /// </summary>
    public class BinElevatorCommandMessage
    {
        /// <summary>
        /// 料仓编号（1, 2, 3）
        /// </summary>
        public int BinNumber { get; set; }
        
        /// <summary>
        /// 控制指令
        /// </summary>
        public BinCommand Command { get; set; }
        
        /// <summary>
        /// 指令ID（用于跟踪指令执行）
        /// </summary>
        public string CommandId { get; set; }
        
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// 指令描述
        /// </summary>
        public string Description { get; set; }

        public BinElevatorCommandMessage()
        {
            Timestamp = DateTime.Now;
            CommandId = Guid.NewGuid().ToString();
        }

        public BinElevatorCommandMessage(int binNumber, BinCommand command, string description = "")
        {
            BinNumber = binNumber;
            Command = command;
            Description = description;
            Timestamp = DateTime.Now;
            CommandId = Guid.NewGuid().ToString();
        }
    }
    
    /// <summary>
    /// 料仓升降状态反馈消息
    /// </summary>
    public class BinElevatorStatusMessage
    {
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
        public DateTime Timestamp { get; set; }
        
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
            Timestamp = DateTime.Now;
        }

        public BinElevatorStatusMessage(int binNumber, BinExecuteState state, BinCommand currentCommand, string commandId = "", string description = "")
        {
            BinNumber = binNumber;
            State = state;
            CurrentCommand = currentCommand;
            CommandId = commandId;
            Description = description;
            Timestamp = DateTime.Now;
        }
    }
}