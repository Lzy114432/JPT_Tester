using System;

namespace EwanCore.Module.Interface
{
    /// <summary>
    /// <see cref="IModule"/> 扩展方法。
    /// </summary>
    public static class ModuleExtensions
    {
        /// <summary>
        /// 历史拼写兼容：<c>Destory</c>（错误拼写） -> <see cref="IModule.Destroy"/>。
        /// </summary>
        /// <param name="module">模块实例。</param>
        [Obsolete("Typo alias. Use IModule.Destroy() instead.", false)]
        public static void Destory(this IModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            module.Destroy();
        }
    }
}
