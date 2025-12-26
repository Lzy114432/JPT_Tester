using System;

namespace EwanCore
{
    /// <summary>
    /// Manager 生命周期接口（Init/Dispose）。
    /// </summary>
    public interface IManager : IDisposable
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <returns>是否初始化成功。</returns>
        bool Init();
    }
}
