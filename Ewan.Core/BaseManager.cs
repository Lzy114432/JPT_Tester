using EwanCommon.Logging;
using EwanCore;

namespace Ewan.Core
{
    /// <summary>
    /// 基础管理类
    /// </summary>
    /// <typeparam name="T">管理类的类型</typeparam>
    public abstract class BaseManager<T> : IManager
    {
        protected readonly UILogger _uiLogger = new UILogger();
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
            _appLogger.Debug("基础管理器初始化成功: " + GetType().Name);
            return true;
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public virtual void Destroy()
        {
            _appLogger.Debug("基础管理器已销毁: " + GetType().Name);
        }

        /// <summary>
        /// 释放资源（IDisposable 实现）
        /// </summary>
        public virtual void Dispose()
        {
            Destroy();
        }

        private static class InstanceHolder
        {
            public static T INSTANCE = System.Activator.CreateInstance<T>();
        }
    }
}
