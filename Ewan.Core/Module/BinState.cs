namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降状态枚举
    /// </summary>
    internal enum BinElevatorState
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
        Stopped
    }

    /// <summary>
    /// 单个料仓的状态封装
    /// 用于简化 BinElevatorModule 中的状态管理
    /// </summary>
    internal class BinState
    {
        /// <summary>
        /// 料仓编号 (1-3)
        /// </summary>
        public int BinNumber { get; }

        /// <summary>
        /// 对应的轴 ID
        /// </summary>
        public int AxisId { get; }

        /// <summary>
        /// 当前升降状态
        /// </summary>
        public BinElevatorState CurrentState { get; set; } = BinElevatorState.Unknown;

        /// <summary>
        /// 是否已到达感应位置（用于初始化流程）
        /// </summary>
        public bool ReachedSensor { get; set; } = false;

        /// <summary>
        /// 创建料仓状态实例
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        /// <param name="axisId">轴 ID</param>
        public BinState(int binNumber, int axisId)
        {
            BinNumber = binNumber;
            AxisId = axisId;
        }

        /// <summary>
        /// 重置到未知状态
        /// </summary>
        public void Reset()
        {
            CurrentState = BinElevatorState.Unknown;
            ReachedSensor = false;
        }

        /// <summary>
        /// 设置为已停止状态
        /// </summary>
        public void Stop()
        {
            CurrentState = BinElevatorState.Stopped;
            ReachedSensor = false;
        }
    }
}
