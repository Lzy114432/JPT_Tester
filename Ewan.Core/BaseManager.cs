using Ewan.Core.Logger;

namespace Ewan.Core
{
    /// <summary>
    /// 基础管理类
    /// </summary>
    /// <typeparam name="T">管理类的类型</typeparam>
    public abstract class BaseManager<T>
    {
        protected readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));

        /// <summary>
        /// 单例
        /// </summary>
        /// <returns></returns>
        public static T Instance()
        {
            return InstanceHolder.INSTANCE;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual bool Init()
        {
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.BaseManagerInitialized, GetType().Name);
            return true;
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public virtual void Destroy()
        {
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.BaseManagerDestroyed, GetType().Name);
        }

        private static class InstanceHolder
        {
            public static T INSTANCE = System.Activator.CreateInstance<T>();
        }
    }
}
