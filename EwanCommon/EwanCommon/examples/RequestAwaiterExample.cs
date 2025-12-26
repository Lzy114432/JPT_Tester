using EwanCore.Messaging;
using System;
using System.Threading.Tasks;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 你可以把这个文件拷贝到任意 WinForms/WPF/Console 项目里（引用 EwanCommon.dll）直接运行。

namespace EwanCommon.Examples
{
    /// <summary>
    /// RequestAwaiter 的底层用法示例：手工管理等待表（CorrelationId -> Task）。
    /// 一般场景推荐直接用 MessageBus 内置 Request/Reply：IMessageBus.RequestAsync/Respond。
    /// </summary>
    public static class RequestAwaiterExample
    {
        public sealed class MesRequest : IMessage, ICorrelatedMessage<Guid>
        {
            public Guid CorrelationId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public string Api { get; set; }
            public object Payload { get; set; }
        }

        public sealed class MesResult : IMessage, ICorrelatedMessage<Guid>
        {
            public Guid CorrelationId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public bool Success { get; set; }
            public string Error { get; set; }
            public object Data { get; set; }
        }

        /// <summary>
        /// 演示：流程侧发送 Mes.Request 并等待 Mes.Result；后台侧收到请求后回发结果。
        /// </summary>
        public static async Task RunAsync()
        {
            // 1) 创建消息总线（真实项目建议在 Composition Root 创建并注册为单例）
            using var bus = new MessageBus();

            // 2) 创建等待器（通常注册为 DI 单例，让“流程”和“后台模块”共享）
            using var awaiter = new RequestAwaiter<Guid, MesResult>();

            // 3) 建立 “Mes.Result -> awaiter.TrySetResult” 的桥（后台收到响应消息后完成等待）
            using var resultSub = bus.Subscribe<MesResult>(res => awaiter.TrySetResult(res.CorrelationId, res));

            // 4) 示例的“后台 MES 模块”：收到 Mes.Request 后，模拟发送并回发 Mes.Result
            using var backendSub = bus.Subscribe<MesRequest>(req =>
            {
                // 真实项目这里会调用 MES SDK/HTTP/TCP...；拿到返回后再 push result
                var res = new MesResult
                {
                    CorrelationId = req.CorrelationId,
                    Success = true,
                    Data = "OK"
                };

                bus.Post(res);
            });

            // 5) 流程侧：注册等待 -> 发送请求 -> await 结果（在状态机里通常是“发请求一步 + 等待一步”）
            var id = Guid.NewGuid();
            var pending = awaiter.Register(id, timeoutMs: 3000);

            bus.Post(new MesRequest
            {
                CorrelationId = id,
                Api = "Confirm",
                Payload = new { BatchId = "B001" }
            });

            try
            {
                var result = await pending.ConfigureAwait(false);
                Console.WriteLine($"MES result: success={result.Success}, data={result.Data}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("MES timeout/canceled.");
            }
        }
    }
}
