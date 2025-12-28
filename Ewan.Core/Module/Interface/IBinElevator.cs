using Ewan.Model.Production;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降控制接口（消息驱动）
    /// 用于抽象 BinElevatorModule 的核心操作，支持单元测试和依赖注入
    /// </summary>
    public interface IBinElevator
    {
        /// <summary>
        /// 初始化全部料仓，并返回执行结果（异步）
        /// </summary>
        /// <param name="ct">取消令牌</param>
        /// <returns>初始化状态消息</returns>
        Task<BinElevatorStatusMessage> InitializeAllAsync(CancellationToken ct = default);

        /// <summary>
        /// 将指定料仓上升到有料感应位置，并返回物料检测结果（异步）
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>物料检测状态消息</returns>
        Task<BinElevatorStatusMessage> RaiseToSensorAsync(int binNumber, CancellationToken ct = default);

        /// <summary>
        /// 装料完成，指定料仓下降一格（无需回复）
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        void LoadingCompleted(int binNumber);

        /// <summary>
        /// 强制停止所有料仓（无需回复）
        /// </summary>
        void ForceStopAll();
    }
}
