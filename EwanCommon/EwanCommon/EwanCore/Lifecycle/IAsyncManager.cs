using System.Threading.Tasks;

namespace EwanCore
{
    /// <summary>
    /// 可选的异步生命周期接口：用于支持异步 Init/Dispose 场景（例如 IO/网络初始化与释放）。
    /// </summary>
    public interface IAsyncManager : IManager
    {
        /// <summary>
        /// 异步初始化。
        /// </summary>
        /// <returns>是否初始化成功。</returns>
        Task<bool> InitAsync();

        /// <summary>
        /// 异步释放。
        /// </summary>
        Task DisposeAsync();
    }
}

