using Ewan.Core.Logger;
using Ewan.LogManager.Logger;

namespace Ewan.Core
{
    /// <summary>
    /// 基础管理类
    /// </summary>
    /// <typeparam name="T">管理类的类型</typeparam>
    public abstract class BaseManager<T>
    {
        protected readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        protected readonly AppLogger _appLogger = AppLogger.Instance;

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
            _appLogger.Debug(Ewan.Resources.LogMessages.BaseManagerInitialized + ": " + GetType().Name);
            return true;
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public virtual void Destroy()
        {
            _appLogger.Debug(Ewan.Resources.LogMessages.BaseManagerDestroyed + ": " + GetType().Name);
        }

        private static class InstanceHolder
        {
            public static T INSTANCE = System.Activator.CreateInstance<T>();
        }
    }
}
