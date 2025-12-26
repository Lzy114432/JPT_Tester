using EwanCore.Messaging;
using EwanCore.Module.Interface;
using EwanCore.Runner;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// 说明：
// - 这是“使用示例文件”，不参与 EwanCommon.csproj 编译。
// - 目标：演示“流程层需要等待 MES 返回”的推荐写法：
//   - 流程侧：用 RequestAsync 发请求，但不在 IModule.Run() 里阻塞等待；而是保存 Task 并在后续轮询中检查完成状态。
//   - 后台侧：用 RespondAsync 接请求，内部异步发送（可限并发/重试），返回 MesUploadReply。

namespace EwanCommon.Examples
{
    public static class MesUploadBestPracticeExample
    {
        // 1) 消息契约：Request/Reply（CorrelationId 由总线自动生成/拷贝）
        public sealed class MesUploadRequest : IMessage, ICorrelatedMessage<Guid>
        {
            public Guid CorrelationId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public string Api { get; set; } = string.Empty;
            public object? Payload { get; set; }
        }

        public sealed class MesUploadReply : IMessage, ICorrelatedMessage<Guid>
        {
            public Guid CorrelationId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public bool Success { get; set; }
            public string? Error { get; set; }
            public object? Data { get; set; }
        }

        // 2) 后台 MES 模块：收到请求 -> 异步发送 -> 回包
        private sealed class MesGatewayModule : IModule
        {
            private readonly IMessageBus _bus;
            private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1); // 限并发：保证串行发送（可按需调整）

            private IDisposable? _responder;
            private CancellationTokenSource? _cts;
            private int _seq;

            public MesGatewayModule(IMessageBus bus) => _bus = bus ?? throw new ArgumentNullException(nameof(bus));

            public void Init()
            {
                _cts = new CancellationTokenSource();

                // RespondAsync：不会阻塞 MessageBus 的分发线程（内部 await）
                _responder = _bus.RespondAsync<MesUploadRequest, MesUploadReply>(
                    req => SendToMesWithRetryAsync(req, _cts.Token),
                    postReply: true);
            }

            public bool Run()
            {
                // 发送由订阅驱动；Run() 只保持模块存活
                return true;
            }

            public void SetObject(object obj) { }

            public void Destroy()
            {
                // 先取消，避免 Stop 后仍然继续发请求
                _cts?.Cancel();
                _responder?.Dispose();

                _cts?.Dispose();
                _cts = null;
                _responder = null;
            }

            private async Task<MesUploadReply> SendToMesWithRetryAsync(MesUploadRequest request, CancellationToken cancellationToken)
            {
                await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // 示例：最多重试 3 次（真实项目可加退避、错误分类等）
                    Exception? last = null;
                    for (var attempt = 1; attempt <= 3; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            // 这里替换为真实的 MES SDK/HTTP/TCP 调用
                            await Task.Delay(120, cancellationToken).ConfigureAwait(false);

                            // 模拟：每 5 次失败 1 次
                            var current = Interlocked.Increment(ref _seq);
                            if (current % 5 == 0)
                            {
                                throw new InvalidOperationException("Simulated MES failure.");
                            }

                            return new MesUploadReply
                            {
                                Success = true,
                                Data = new { Attempt = attempt, Ack = "OK" }
                            };
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex) when (attempt < 3)
                        {
                            last = ex;
                            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            last = ex;
                            break;
                        }
                    }

                    return new MesUploadReply
                    {
                        Success = false,
                        Error = last?.Message ?? "MES send failed."
                    };
                }
                finally
                {
                    _sendGate.Release();
                }
            }
        }

        // 3) “流程侧”模块：发请求 + 轮询等待结果（不阻塞 Run）
        private sealed class ProcessModule : IModule
        {
            private readonly IMessageBus _bus;
            private DateTime _last;
            private Task<MesUploadReply>? _pending;
            private int _payloadSeq;

            public ProcessModule(IMessageBus bus) => _bus = bus ?? throw new ArgumentNullException(nameof(bus));

            public void Init()
            {
                _last = DateTime.MinValue;
            }

            public bool Run()
            {
                // 先处理“上一次的等待结果”
                if (_pending != null)
                {
                    if (!_pending.IsCompleted)
                    {
                        return true;
                    }

                    try
                    {
                        var reply = _pending.GetAwaiter().GetResult();
                        if (reply.Success)
                        {
                            Console.WriteLine($"[PROCESS] MES OK: {reply.Data}");
                        }
                        else
                        {
                            Console.WriteLine($"[PROCESS] MES FAIL: {reply.Error}");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        Console.WriteLine("[PROCESS] MES timeout/canceled.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PROCESS] MES exception: {ex.GetBaseException().Message}");
                    }
                    finally
                    {
                        _pending = null;
                    }

                    return true;
                }

                // 每 500ms 触发一次上传（示例）
                if ((DateTime.Now - _last).TotalMilliseconds < 500)
                {
                    return true;
                }
                _last = DateTime.Now;

                try
                {
                    _pending = _bus.RequestAsync<MesUploadRequest, MesUploadReply>(
                        new MesUploadRequest
                        {
                            Api = "Upload",
                            Payload = new { Seq = Interlocked.Increment(ref _payloadSeq), Time = DateTime.Now },
                        },
                        timeoutMs: 2000);
                }
                catch (Exception ex)
                {
                    // 例如：队列满导致入队失败（AsyncQueueCapacity/OverflowStrategy）
                    Console.WriteLine($"[PROCESS] enqueue failed: {ex.GetBaseException().Message}");
                }

                return true;
            }

            public void SetObject(object obj) { }

            public void Destroy()
            {
                _pending = null;
            }
        }

        public static void Run()
        {
            using var bus = new MessageBus();

            var modules = new List<IModule>
            {
                // 先初始化后台 responder，避免流程侧请求“没人接”导致超时
                new MesGatewayModule(bus),
                new ProcessModule(bus),
            };

            var runner = new StreamRunner(modules);
            runner.Start();

            Thread.Sleep(5000);
            runner.Stop();
        }
    }
}

