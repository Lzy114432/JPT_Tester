using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓升降控制接口
    /// 用于抽象 BinElevatorModule 的核心功能，支持单元测试和依赖注入
    /// </summary>
    public interface IBinElevator
    {
        /// <summary>
        /// 将指定料仓上升到有料感应位置，并返回物料检测结果
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        /// <returns>物料检测结果</returns>
        BinMaterialCheckResult RaiseToSensor(int binNumber);

        /// <summary>
        /// 将指定料仓上升到有料感应位置，并返回物料检测结果（异步）
        /// </summary>
        /// <param name="binNumber">料仓编号 (1-3)</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>物料检测结果</returns>
        Task<BinMaterialCheckResult> RaiseToSensorAsync(int binNumber, CancellationToken ct = default);

        /// <summary>
        /// 强制停止所有料仓升降并将状态机置为停止
        /// </summary>
        void ForceStopAllBins();

        /// <summary>
        /// 执行料仓升降硬件初始化
        /// 将模式设置为Init，触发料仓初始化流程
        /// </summary>
        void PerformHardwareInitialization();

        /// <summary>
        /// 初始化模块
        /// </summary>
        void Init();

        /// <summary>
        /// 销毁模块
        /// </summary>
        void Destroy();

        /// <summary>
        /// 运行一次模块逻辑（用于轮询线程调用）
        /// </summary>
        /// <returns>是否继续运行</returns>
        bool Run();
    }
}
