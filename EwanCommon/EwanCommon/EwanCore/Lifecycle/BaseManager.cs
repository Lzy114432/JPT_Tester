using EwanCommon.Logging;
using log4net;
using System;
using System.Threading;

namespace EwanCore
{
    /// <summary>
    /// 基础管理类（懒加载单例 + 生命周期）
    /// </summary>
    /// <typeparam name="T">管理类的类型（CRTP：T 必须继承 BaseManager&lt;T&gt;）</typeparam>
    public abstract class BaseManager<T> : IManager where T : BaseManager<T>
    {
        protected static readonly ILog s_logger = Log.GetLogger(typeof(T));

        private static readonly object s_instanceLock = new object();
        private static Func<T> s_instanceFactory;
        private static readonly Lazy<T> s_instance = new Lazy<T>(CreateInstance, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly object s_lifecycleLock = new object();
        private static int s_initOnceState;
        private static int s_destroyOnceState;

        private int _disposeState;

        /// <summary>
        /// 单例
        /// </summary>
        public static T Instance()
        {
            return s_instance.Value;
        }

        /// <summary>
        /// 是否已创建实例（不会触发创建）
        /// </summary>
        public static bool IsInstanceCreated => s_instance.IsValueCreated;

        /// <summary>
        /// 在不触发创建的前提下获取实例。
        /// </summary>
        /// <param name="instance">已创建的实例。</param>
        /// <returns>是否已创建实例。</returns>
        public static bool TryGetInstance(out T instance)
        {
            if (!s_instance.IsValueCreated)
            {
                instance = default;
                return false;
            }

            instance = s_instance.Value;
            return true;
        }

        /// <summary>
        /// 配置实例创建工厂（建议在任何 Instance() 调用之前设置）
        /// </summary>
        /// <param name="factory">实例工厂。</param>
        /// <param name="throwIfAlreadyCreated">如果实例已创建，是否抛异常。</param>
        public static void ConfigureFactory(Func<T> factory, bool throwIfAlreadyCreated = true)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (s_instanceLock)
            {
                if (s_instance.IsValueCreated)
                {
                    if (throwIfAlreadyCreated)
                    {
                        throw new InvalidOperationException($"{typeof(T).FullName} instance has already been created.");
                    }
                    return;
                }

                s_instanceFactory = factory;
            }
        }

        /// <summary>
        /// 直接绑定一个实例（常用于 DI 容器中的单例实例绑定）。
        /// </summary>
        /// <param name="instance">实例。</param>
        /// <param name="throwIfAlreadyCreated">如果实例已创建，是否抛异常。</param>
        public static void ConfigureInstance(T instance, bool throwIfAlreadyCreated = true)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            ConfigureFactory(() => instance, throwIfAlreadyCreated);
        }

        /// <summary>
        /// 确保 Init 只执行一次（不影响你手动多次调用 Init 的自由度）
        /// </summary>
        public static bool EnsureInit()
        {
            if (Volatile.Read(ref s_initOnceState) == 1)
            {
                return true;
            }

            lock (s_lifecycleLock)
            {
                if (s_initOnceState == 1)
                {
                    return true;
                }

                var ok = Instance().Init();
                if (ok)
                {
                    s_initOnceState = 1;
                }
                return ok;
            }
        }

        /// <summary>
        /// 确保 Destroy 只执行一次
        /// </summary>
        public static void EnsureDestroy()
        {
            if (Volatile.Read(ref s_destroyOnceState) == 1)
            {
                return;
            }

            lock (s_lifecycleLock)
            {
                if (s_destroyOnceState == 1)
                {
                    return;
                }

                try
                {
                    Instance().Dispose();
                }
                finally
                {
                    s_destroyOnceState = 1;
                }
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual bool Init()
        {
            s_logger.Debug($"{GetType()} has been init.");
            return true;
        }

        /// <summary>
        /// 销毁
        /// </summary>
        public virtual void Destroy()
        {
            s_logger.Debug($"{GetType()} has been destroyed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) == 1)
            {
                return;
            }

            Destroy();
        }

        private static T CreateInstance()
        {
            var factory = Volatile.Read(ref s_instanceFactory);
            if (factory != null)
            {
                return factory();
            }

            // 允许非 public 的无参构造（便于把构造函数设为 private 来限制外部 new）
            return (T)Activator.CreateInstance(typeof(T), nonPublic: true);
        }
    }
}
