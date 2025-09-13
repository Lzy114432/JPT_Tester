namespace Ewan.Model.Production
{
    /// <summary>
    /// 系统运行模式枚举
    /// </summary>
    public enum SystemMode
    {
        /// <summary>
        /// 自动模式 - 系统自动响应信号执行流程
        /// </summary>
        Auto,

        /// <summary>
        /// 手动模式 - 需要手动控制每个步骤
        /// </summary>
        Manual,

        /// <summary>
        /// 初始化模式 - 料仓自动上升到感应位置
        /// </summary>
        Init
    }

    /// <summary>
    /// 自动生产流程状态枚举
    /// </summary>
    public enum AutoProductionState
    {
        /// <summary>
        /// 空闲状态，等待扫码信号或小车请求
        /// </summary>
        Idle,

        /// <summary>
        /// 等待扫码许可和执行扫码
        /// </summary>
        WaitingForScan,

        /// <summary>
        /// 扫码完成，等待外包流程完成
        /// </summary>
        WaitingForExternalComplete,

        /// <summary>
        /// 处理小车取料请求
        /// </summary>
        ProcessingCartRequest,

        /// <summary>
        /// 上料状态 - 等待机械手上料完成
        /// </summary>
        Loading,

        /// <summary>
        /// 下料状态 - 等待机械手下料完成
        /// </summary>
        Unloading,

        /// <summary>
        /// 暂停状态，保持当前位置和状态
        /// </summary>
        Paused,

        /// <summary>
        /// 停止状态，完成当前步骤后停止
        /// </summary>
        Stopping,

        /// <summary>
        /// 已停止状态，等待重新启动
        /// </summary>
        Stopped,

        /// <summary>
        /// 错误状态（急停）
        /// </summary>
        Error
    }

    /// <summary>
    /// 料仓状态机枚举
    /// </summary>
    public enum BinElevatorMode
    {
        /// <summary>
        /// 自动上升模式 - 上升到感应位置停止
        /// </summary>
        AutoUp,

        /// <summary>
        /// 自动下降模式 - 有料感应就下降
        /// </summary>
        AutoDown,

        /// <summary>
        /// 上料模式 - 等待机械手上料完成
        /// </summary>
        Loading,

        /// <summary>
        /// 下料模式 - 等待机械手下料完成
        /// </summary>
        Unloading,

        /// <summary>
        /// 停止模式 - 料仓停止运动
        /// </summary>
        Stopped
    }
}