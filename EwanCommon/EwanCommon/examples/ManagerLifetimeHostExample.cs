using EwanCommon.Logging;
using EwanCore;
using EwanCore.Attribute;
using EwanCore.Bootstrap;
using EwanCore.Messaging;
using System;
using System.Collections.Generic;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 重点演示：DI-first 的 ManagerLifetimeHost（按 [Manager] 扫描并调用 Init/Dispose）。

namespace EwanCommon.Examples
{
    /// <summary>
    /// ManagerLifetimeHost 示例：用一个最小 IServiceProvider 演示如何启动/释放 Manager。
    /// </summary>
    public static class ManagerLifetimeHostExample
    {
        private sealed class HelloMessage : IMessage
        {
            public DateTimeOffset Timestamp { get; set; }
            public string Text { get; }

            public HelloMessage(string text)
            {
                Text = text ?? string.Empty;
            }
        }

        [Manager(Priority = 1)]
        private sealed class HelloManager : IManager
        {
            private readonly IMessageBus _bus;
            private IDisposable _subscription;

            public HelloManager(IMessageBus bus)
            {
                _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            }

            public bool Init()
            {
                _subscription = _bus.Subscribe<HelloMessage>(m => Console.WriteLine($"[HelloManager] {m.Text}"));
                return true;
            }

            public void Dispose()
            {
                _subscription?.Dispose();
                _subscription = null;
            }
        }

        private sealed class SimpleServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

            public void Register<T>(T instance) where T : class
            {
                _instances[typeof(T)] = instance;
            }

            public object GetService(Type serviceType)
            {
                if (serviceType == null) return null;
                return _instances.TryGetValue(serviceType, out var obj) ? obj : null;
            }
        }

        public static void Run()
        {
            // 1) 日志（约定式）：程序目录放 log4net.config（可直接从 log4net.config.template 复制）
            Log4NetBootstrapper.TryConfigureByConvention();

            // 2) 组装 “容器” 并注册需要的 manager 实例（示例只有 HelloManager）
            var provider = new SimpleServiceProvider();
            using var bus = new MessageBus();
            provider.Register(bus);
            provider.Register(new HelloManager(bus));

            // 3) ManagerLifetimeHost：按 [Manager] 扫描并调用 Init/Dispose（DI-first）
            var assemblies = new[] { typeof(HelloManager).Assembly };
            using (var host = new ManagerLifetimeHost(provider.GetService, assemblies))
            {
                host.Start(requireAll: true);

                // 4) 使用：同步 Publish / 异步 Post
                bus.Publish(new HelloMessage("sync publish"));
                bus.Post(new HelloMessage("async post"));

            }
        }
    }
}
