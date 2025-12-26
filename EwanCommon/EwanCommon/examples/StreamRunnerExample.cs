using EwanCore.Module.Interface;
using EwanCore.Messaging;
using EwanCore.Runner;
using EwanModel.Plc;
using System;
using System.Collections.Generic;
using System.Threading;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - StreamRunner 是一个简单的“模块顺序轮询器”：循环调用每个 IModule.Run()。
// - 模块 Run() 建议“短、快、不阻塞”，用内部节拍控制频率（例如 50ms/100ms 一次）。

namespace EwanCommon.Examples
{
    public static class StreamRunnerExample
    {
        private sealed class PlcSnapshot
        {
            public DateTime Timestamp { get; set; }
            public bool ExternalReady { get; set; }
        }

        private sealed class PlcSnapshotUpdated : IMessage
        {
            public DateTimeOffset Timestamp { get; set; }
            public PlcSnapshot Snapshot { get; }

            public PlcSnapshotUpdated(PlcSnapshot snapshot)
            {
                Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            }
        }

        private sealed class ProcessOutbound : IMessage
        {
            public DateTimeOffset Timestamp { get; set; }
            public string Op { get; }
            public object Data { get; }

            public ProcessOutbound(string op, object data)
            {
                Op = op ?? string.Empty;
                Data = data;
            }
        }

        private sealed class PlcPollingModule : IModule
        {
            private readonly IMessageBus _bus;
            private DateTime _last;

            public PlcPollingModule(IMessageBus bus) => _bus = bus;

            public void Init()
            {
                // 项目级默认字节序：MC 小端 / Modbus 大端
                PlcCodecDefaults.UseMc();
                _last = DateTime.MinValue;
            }

            public bool Run()
            {
                // 100ms 轮询一次（示例）
                if ((DateTime.Now - _last).TotalMilliseconds < 100)
                {
                    return true;
                }
                _last = DateTime.Now;

                // 真实项目：这里读 PLC，得到 snapshot（外部系统状态/报警位/计数等）
                var snapshot = new PlcSnapshot { Timestamp = DateTime.Now, ExternalReady = true };

                // 推送给 UI/其它模块（异步队列）
                _bus.Post(new PlcSnapshotUpdated(snapshot));
                return true;
            }

            public void SetObject(object obj) { /* 可选：注入共享上下文 */ }

            public void Destroy() { /* 释放通讯/解绑监听器 */ }
        }

        private sealed class MesSendModule : IModule
        {
            private readonly IMessageBus _bus;
            private IDisposable _subscription;

            public MesSendModule(IMessageBus bus) => _bus = bus;

            public void Init()
            {
                // 监听流程推送的业务消息，统一在后台线程发 MES
                _subscription = _bus.Subscribe<ProcessOutbound>(msg =>
                {
                    // 真实项目：这里调用 MES SDK/HTTP/TCP，并根据结果 Push Mes.Result/Mes.Error
                    Console.WriteLine($"[MES] send: op={msg.Op}, data={msg.Data}");
                });
            }

            public bool Run()
            {
                // 发送由 listener 驱动，Run() 只需保持“模块存活”
                return true;
            }

            public void SetObject(object obj) { }

            public void Destroy()
            {
                _subscription?.Dispose();
                _subscription = null;
            }
        }

        public static void Run()
        {
            using var bus = new MessageBus();

            var modules = new List<IModule>
            {
                new PlcPollingModule(bus),
                new MesSendModule(bus),
            };

            var runner = new StreamRunner(modules);
            runner.Start();

            // 模拟“流程层”推送一个业务消息给 MES 模块
            bus.Post(new ProcessOutbound("Upload", 123));

            Thread.Sleep(500);

            runner.Stop();
        }
    }
}
