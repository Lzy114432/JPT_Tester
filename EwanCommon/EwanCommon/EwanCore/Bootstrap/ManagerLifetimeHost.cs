using EwanCommon.Logging;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EwanCore.Bootstrap
{
    /// <summary>
    /// DI 友好的 Manager 生命周期宿主：按 <c>[Manager]</c> 扫描并依次调用 <see cref="IManager.Init"/> / <see cref="IDisposable.Dispose"/>。
    /// </summary>
    public sealed class ManagerLifetimeHost : IDisposable, IAsyncDisposable
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(ManagerLifetimeHost));

        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly Func<Type, IManager> _resolver;
        private readonly IReadOnlyList<Type> _managerTypes;
        private readonly List<IManager> _startedManagers = new List<IManager>();
        private bool _started;
        private bool _disposed;

        public bool IsStarted => Volatile.Read(ref _started);

        public bool IsDisposed => Volatile.Read(ref _disposed);

        /// <summary>
        /// 创建一个 <see cref="ManagerLifetimeHost"/>。
        /// </summary>
        /// <param name="resolver">从容器/工厂解析实例的方法（必须返回 <see cref="IManager"/>）。</param>
        /// <param name="assemblies">要扫描的程序集；为 null 则扫描当前 AppDomain。</param>
        public ManagerLifetimeHost(Func<Type, object> resolver, IEnumerable<Assembly> assemblies = null)
            : this(WrapResolver(resolver), assemblies)
        {
        }

        /// <summary>
        /// 创建一个 <see cref="ManagerLifetimeHost"/>。
        /// </summary>
        /// <param name="resolver">从容器/工厂解析实例的方法。</param>
        /// <param name="assemblies">要扫描的程序集；为 null 则扫描当前 AppDomain。</param>
        public ManagerLifetimeHost(Func<Type, IManager> resolver, IEnumerable<Assembly> assemblies = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _managerTypes = ManagerTypeScanner.Discover(assemblies);
        }

        /// <summary>
        /// 初始化所有 Manager（按 Priority 顺序）。
        /// </summary>
        /// <param name="requireAll">为 true 时，遇到未注册的 Manager 会失败并回滚 Stop。</param>
        /// <returns>是否启动成功。</returns>
        public bool Start(bool requireAll = true)
        {
            _gate.Wait();
            try
            {
                ThrowIfDisposed();

                if (_started)
                {
                    return true;
                }

                if (s_logger.IsInfoEnabled)
                {
                    s_logger.Info($"ManagerLifetimeHost starting. requireAll={requireAll}");
                }

                foreach (var managerType in _managerTypes)
                {
                    if (managerType.IsAbstract)
                    {
                        continue;
                    }

                    IManager manager;
                    try
                    {
                        manager = _resolver(managerType);
                    }
                    catch (Exception ex)
                    {
                        s_logger.Error($"Resolve manager failed: {managerType.FullName}", ex);
                        StopCore();
                        return false;
                    }

                    if (manager == null)
                    {
                        if (requireAll)
                        {
                            s_logger.Error($"Manager not registered in container: {managerType.FullName}");
                            StopCore();
                            return false;
                        }
                        continue;
                    }

                    _startedManagers.Add(manager);

                    try
                    {
                        if (!manager.Init())
                        {
                            s_logger.Error($"Init manager returned false: {managerType.FullName}");
                            StopCore();
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        s_logger.Error($"Init manager failed: {managerType.FullName}", ex);
                        StopCore();
                        return false;
                    }
                }

                Volatile.Write(ref _started, true);
                if (s_logger.IsInfoEnabled)
                {
                    s_logger.Info($"ManagerLifetimeHost started. startedManagers={_startedManagers.Count}");
                }
                return true;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<bool> StartAsync(bool requireAll = true)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                if (_started)
                {
                    return true;
                }

                if (s_logger.IsInfoEnabled)
                {
                    s_logger.Info($"ManagerLifetimeHost starting async. requireAll={requireAll}");
                }

                foreach (var managerType in _managerTypes)
                {
                    if (managerType.IsAbstract)
                    {
                        continue;
                    }

                    IManager manager;
                    try
                    {
                        manager = _resolver(managerType);
                    }
                    catch (Exception ex)
                    {
                        s_logger.Error($"Resolve manager failed: {managerType.FullName}", ex);
                        await StopCoreAsync().ConfigureAwait(false);
                        return false;
                    }

                    if (manager == null)
                    {
                        if (requireAll)
                        {
                            s_logger.Error($"Manager not registered in container: {managerType.FullName}");
                            await StopCoreAsync().ConfigureAwait(false);
                            return false;
                        }
                        continue;
                    }

                    _startedManagers.Add(manager);

                    try
                    {
                        var ok = manager is IAsyncManager asyncManager
                            ? await asyncManager.InitAsync().ConfigureAwait(false)
                            : manager.Init();

                        if (!ok)
                        {
                            s_logger.Error($"Init manager returned false: {managerType.FullName}");
                            await StopCoreAsync().ConfigureAwait(false);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        s_logger.Error($"Init manager failed: {managerType.FullName}", ex);
                        await StopCoreAsync().ConfigureAwait(false);
                        return false;
                    }
                }

                Volatile.Write(ref _started, true);
                if (s_logger.IsInfoEnabled)
                {
                    s_logger.Info($"ManagerLifetimeHost started async. startedManagers={_startedManagers.Count}");
                }
                return true;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// 逆序释放（按 Priority 反序）。
        /// </summary>
        public void Stop()
        {
            _gate.Wait();
            try
            {
                if (Volatile.Read(ref _disposed))
                {
                    return;
                }

                StopCore();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task StopAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _disposed))
                {
                    return;
                }

                await StopCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// 调用 <see cref="Stop"/>。
        /// </summary>
        public void Dispose()
        {
            _gate.Wait();
            try
            {
                if (Volatile.Read(ref _disposed))
                {
                    return;
                }

                StopCore();
                Volatile.Write(ref _disposed, true);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _disposed))
                {
                    return;
                }

                await StopCoreAsync().ConfigureAwait(false);
                Volatile.Write(ref _disposed, true);
            }
            finally
            {
                _gate.Release();
            }
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return new ValueTask(DisposeAsync());
        }

        private static Func<Type, IManager> WrapResolver(Func<Type, object> resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            return type =>
            {
                var instance = resolver(type);
                if (instance == null)
                {
                    return null;
                }

                if (instance is IManager manager)
                {
                    return manager;
                }

                throw new InvalidCastException($"Resolved instance must implement {typeof(IManager).FullName}: {type.FullName}");
            };
        }

        private void StopCore()
        {
            var hadManagers = _startedManagers.Count > 0;

            if (hadManagers && s_logger.IsInfoEnabled)
            {
                s_logger.Info($"ManagerLifetimeHost stopping. startedManagers={_startedManagers.Count}");
            }

            for (var i = _startedManagers.Count - 1; i >= 0; i--)
            {
                var manager = _startedManagers[i];
                try
                {
                    manager.Dispose();
                }
                catch (Exception ex)
                {
                    s_logger.Warn($"Dispose manager failed: {manager.GetType().FullName}.", ex);
                }
            }

            _startedManagers.Clear();
            Volatile.Write(ref _started, false);

            if (hadManagers && s_logger.IsInfoEnabled)
            {
                s_logger.Info("ManagerLifetimeHost stopped.");
            }
        }

        private async Task StopCoreAsync()
        {
            var hadManagers = _startedManagers.Count > 0;

            if (hadManagers && s_logger.IsInfoEnabled)
            {
                s_logger.Info($"ManagerLifetimeHost stopping async. startedManagers={_startedManagers.Count}");
            }

            for (var i = _startedManagers.Count - 1; i >= 0; i--)
            {
                var manager = _startedManagers[i];
                try
                {
                    if (manager is IAsyncManager asyncManager)
                    {
                        await asyncManager.DisposeAsync().ConfigureAwait(false);
                    }
                    else if (manager is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        manager.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    s_logger.Warn($"Dispose manager failed: {manager.GetType().FullName}.", ex);
                }
            }

            _startedManagers.Clear();
            Volatile.Write(ref _started, false);

            if (hadManagers && s_logger.IsInfoEnabled)
            {
                s_logger.Info("ManagerLifetimeHost stopped async.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed))
            {
                throw new ObjectDisposedException(nameof(ManagerLifetimeHost));
            }
        }
    }
}
