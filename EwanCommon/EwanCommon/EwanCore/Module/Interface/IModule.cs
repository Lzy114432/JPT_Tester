using System;

namespace EwanCore.Module.Interface
{
    /// <summary>
    /// 流程节点接口（用于 <see cref="EwanCore.Runner.StreamRunner"/>）。
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// 初始化节点。
        /// </summary>
        void Init();

        /// <summary>
        /// 执行一次节点逻辑。
        /// 返回 false 表示本次流程中断（后续节点不再执行）。
        /// </summary>
        bool Run();

        /// <summary>
        /// 注入上下文对象/共享数据。
        /// </summary>
        /// <param name="obj">共享数据。</param>
        void SetObject(object obj);

        /// <summary>
        /// 销毁/清理资源。
        /// </summary>
        void Destroy();
    }
}
