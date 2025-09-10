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
    }
}
