namespace Ewan.Core.Msg
{
    /// <summary>
    /// 消息主题
    /// </summary>
    public enum MsgSubject
    {
        None,
        UILog,
        IOUpdate,  // IO数据更新消息
        StatusIndicator,  // 系统状态指示器控制消息（蜂鸣器和信号灯）
        SafetyAlert,  // 安全报警消息
        SystemStatus,  // 系统状态变化消息
        BinElevatorCommand,  // 料仓升降控制指令
        BinElevatorStatus,   // 料仓升降状态反馈

        LoadingandunloadingState, //装卸状态
    }
}
