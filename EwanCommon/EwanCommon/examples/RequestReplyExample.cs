using EwanCore.Messaging;
using System;
using System.Threading.Tasks;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 你可以把这个文件拷贝到任意 WinForms/WPF/Console 项目里（引用 EwanCommon.dll）直接运行。

namespace EwanCommon.Examples
{
    /// <summary>
    /// 内置 Request/Reply 的最小用法示例：RequestAsync + Respond（基于 CorrelationId）。
    /// </summary>
    public static class RequestReplyExample
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
        /// 演示：流程侧发送 MesRequest 并等待 MesResult；后台侧收到请求后回发结果。
        /// </summary>
        public static async Task RunAsync()
        {
            // 1) 创建消息总线（真实项目建议在 Composition Root 创建并注册为单例）
            using var bus = new MessageBus();

            // 2) 示例的“后台 MES 模块”：收到 MesRequest 后，模拟发送并回发 MesResult（CorrelationId 自动拷贝）
            using var backend = bus.Respond<MesRequest, MesResult>(req => new MesResult
            {
                Success = true,
                Data = "OK"
            });

            // 3) 流程侧：发送请求 -> await 结果（CorrelationId 若为空会自动生成）
            try
            {
                var result = await bus.RequestAsync<MesRequest, MesResult>(
                    new MesRequest
                    {
                        Api = "Confirm",
                        Payload = new { BatchId = "B001" }
                    },
                    timeoutMs: 3000).ConfigureAwait(false);

                Console.WriteLine($"MES result: success={result.Success}, data={result.Data}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("MES timeout/canceled.");
            }
        }
    }
}
