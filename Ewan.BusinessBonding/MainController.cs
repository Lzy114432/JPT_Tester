using System;
using System.Reflection;
using EwanCore;
using EwanCore.Bootstrap;
using EwanCommon.Logging;
using log4net;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 主控制器 - 使用 ManagerLifetimeHost 管理 Manager 生命周期
    /// 支持依赖注入：可通过构造函数提供自定义的 resolver
    /// </summary>
    public class MainController : IDisposable
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(MainController));
        private readonly ManagerLifetimeHost _host;
        private bool _disposed;

        #region 单例支持（兼容现有代码）
        private static readonly Lazy<MainController> s_instance = new Lazy<MainController>(() => new MainController());

        /// <summary>
        /// 获取单例实例（兼容现有代码）
        /// </summary>
        public static MainController Instance() => s_instance.Value;
        #endregion

        /// <summary>
        /// 是否已启动
        /// </summary>
        public bool IsStarted => _host?.IsStarted ?? false;

        /// <summary>
        /// 创建 MainController（使用默认的 Activator 解析器）
        /// </summary>
        public MainController() : this(DefaultResolver)
        {
        }

        /// <summary>
        /// 创建 MainController（依赖注入方式）
        /// </summary>
        /// <param name="resolver">Manager 实例解析器</param>
        /// <param name="assemblies">要扫描的程序集（可选）</param>
        public MainController(Func<Type, IManager> resolver, params Assembly[] assemblies)
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            _host = assemblies != null && assemblies.Length > 0
                ? new ManagerLifetimeHost(resolver, assemblies)
                : new ManagerLifetimeHost(resolver);
        }

        /// <summary>
        /// 初始化所有 Manager
        /// </summary>
        /// <returns>是否初始化成功</returns>
        public bool Initialize()
        {
            ThrowIfDisposed();

            s_logger.Info("MainController 开始初始化所有 Manager");

            try
            {
                var result = _host.Start(requireAll: false);
                if (result)
                {
                    s_logger.Info("MainController 初始化完成");
                }
                else
                {
                    s_logger.Error("MainController 初始化失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                s_logger.Error("MainController 初始化异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 销毁所有 Manager（兼容旧代码）
        /// </summary>
        [Obsolete("请使用 Dispose() 方法")]
        public void Destroy()
        {
            Dispose();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            s_logger.Info("MainController 开始销毁所有 Manager");

            try
            {
                _host?.Stop();
                _host?.Dispose();
                s_logger.Info("MainController 销毁完成");
            }
            catch (Exception ex)
            {
                s_logger.Error("MainController 销毁异常", ex);
            }
        }

        /// <summary>
        /// 默认的 Manager 解析器 - 使用 Activator.CreateInstance
        /// </summary>
        private static IManager DefaultResolver(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface)
            {
                return null;
            }

            try
            {
                // 尝试获取单例实例（兼容旧的 BaseManager<T> 模式）
                var instanceMethod = type.GetMethod("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceMethod != null && typeof(IManager).IsAssignableFrom(instanceMethod.ReturnType))
                {
                    var instance = instanceMethod.Invoke(null, null) as IManager;
                    if (instance != null)
                    {
                        return instance;
                    }
                }

                // 使用无参构造函数创建实例
                var instance2 = Activator.CreateInstance(type);
                if (instance2 is IManager manager)
                {
                    return manager;
                }

                s_logger.Warn($"创建的实例未实现 IManager 接口: {type.FullName}");
                return null;
            }
            catch (Exception ex)
            {
                s_logger.Error($"创建 Manager 实例失败: {type.FullName}", ex);
                return null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MainController));
            }
        }
    }
}
