namespace Ewan.Core.Msg
{
    /// <summary>
    /// 消息主题
    /// </summary>
    public enum MsgSubject
    {
        None = 0,
        UILog = 1,
        StatusIndicator = 3,  // 系统状态指示器控制消息（蜂鸣器和信号灯）
        SafetyAlert,  // 安全报警消息
        SystemStatus,  // 系统状态变化消息
        SystemControl,  // 系统控制消息（启动/停止）
        BinElevatorCommand,  // 料仓升降控制指令
        BinElevatorStatus,   // 料仓升降状态反馈

        LoadingandunloadingState, //装卸状态

        RingLineData,

        RingLineheartData,

        BeltConveyorControl
    }
}
