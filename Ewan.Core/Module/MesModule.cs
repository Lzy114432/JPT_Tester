using System;
using System.Threading;
using System.Threading.Tasks;
using Ewan.Core.Mes;
using Ewan.Model.Messages;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using EwanCore.Messaging;

namespace Ewan.Core.Module
{
    /// <summary>
    /// MES 后台常驻模块（消息驱动模式）：
    /// - OnInit：注册 MessageHub.RespondAsync 处理 MesRingLineRequest
    /// - OnRun：仅保持模块存活
    /// </summary>
    public class MesModule : BaseModule<MesModule>
    {
        private const int DefaultTimeoutMs = 30000;
        private const int RunLoopIntervalMs = 100;

        private IDisposable _responderSubscription;
        private IDisposable _postSubscription;

        private readonly object _ringLineResponseLock = new object();
        private IDisposable _feedingQianLiaocangResponseSubscription;
        private TaskCompletionSource<FeedingQianLiaocangResponseData> _pendingFeedingQianLiaocangResponse;

        protected override void OnInit()
        {
            _responderSubscription = MessageHub.Current.RespondAsync<MesRingLineRequest, MesRingLineFeedback>(
                ProcessRequestAsync,
                postReply: true);
            _postSubscription = MessageHub.Current.Subscribe<MesRingLineRequest>(ProcessPostRequest);

            _uiLogger.InfoRaw("模块初始化成功: {0}", "MesModule (MessageHub)");
        }

        protected override bool OnRun()
        {
            // 消息驱动模式：业务逻辑在回调中处理，OnRun 仅保持模块存活
            Thread.Sleep(RunLoopIntervalMs);
            return true;
        }

        protected override void OnDestroy()
        {
            try
            {
                _responderSubscription?.Dispose();
                _responderSubscription = null;
            }
            catch
            {
            }

            try
            {
                _postSubscription?.Dispose();
                _postSubscription = null;
            }
            catch
            {
            }

            TaskCompletionSource<FeedingQianLiaocangResponseData> pendingTcs = null;
            lock (_ringLineResponseLock)
            {
                pendingTcs = _pendingFeedingQianLiaocangResponse;
                _pendingFeedingQianLiaocangResponse = null;
            }

            try
            {
                pendingTcs?.TrySetCanceled();
            }
            catch
            {
            }

            try
            {
                _feedingQianLiaocangResponseSubscription?.Dispose();
            }
            catch
            {
            }
            _feedingQianLiaocangResponseSubscription = null;

            _uiLogger.InfoRaw("模块已销毁: {0}", "MesModule");
        }

        /// <summary>
        /// 异步处理 MES 请求
        /// </summary>
        private async Task<MesRingLineFeedback> ProcessRequestAsync(MesRingLineRequest request)
        {
            if (request == null)
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = Guid.Empty,
                    Success = false,
                    Message = "请求为空"
                };
            }

            if (request.CorrelationId == Guid.Empty)
            {
                return null;
            }

            try
            {
                return await ProcessRequestInternalAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "处理 MES 请求异常: " + ex.Message
                };
            }
        }

        private async Task<MesRingLineFeedback> ProcessRequestInternalAsync(MesRingLineRequest request)
        {
            string error;
            if (!EnsureMesReady(out error))
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = error
                };
            }

            switch (request.Action)
            {
                case MesRingLineAction.FeedingQianLiaocang:
                    return await HandleFeedingQianLiaocangAsync(request).ConfigureAwait(false);

                case MesRingLineAction.FeedingQianLiaocangSuccess:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishFeedingQianLiaocangSuccessAsync(request.PlateCode, request.FeedingLiaokuangCode))
                        .ConfigureAwait(false);

                case MesRingLineAction.UnloadingQianLiaocang:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishUnloadingQianLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode))
                        .ConfigureAwait(false);

                case MesRingLineAction.FeedingZhongLiaocang:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishFeedingZhongLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode))
                        .ConfigureAwait(false);

                case MesRingLineAction.UnloadingZhongLiaocang:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishUnloadingZhongLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode))
                        .ConfigureAwait(false);

                case MesRingLineAction.FeedingQingxihongganji:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishFeedingQingxihongganjiAsync(request.PlateCode))
                        .ConfigureAwait(false);

                case MesRingLineAction.FeedingHouLiaocang:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishFeedingHouLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode))
                        .ConfigureAwait(false);

                case MesRingLineAction.UnloadingHouLiaocang:
                    return await HandlePublishOnlyAsync(
                        request,
                        () => MesManager.Instance().PublishUnloadingHouLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode))
                        .ConfigureAwait(false);

                default:
                    return new MesRingLineFeedback
                    {
                        CorrelationId = request.CorrelationId,
                        Action = request.Action,
                        Success = false,
                        Message = "未知的 MES RingLine 请求类型"
                    };
            }
        }

        private void ProcessPostRequest(MesRingLineRequest request)
        {
            if (request == null)
            {
                return;
            }

            if (request.CorrelationId != Guid.Empty)
            {
                return;
            }

            _ = Task.Run(() => ProcessPostRequestAsync(request));
        }

        private async Task ProcessPostRequestAsync(MesRingLineRequest request)
        {
            try
            {
                string error;
                if (!EnsureMesReady(out error))
                {
                    _uiLogger.WarnRaw("MES Post请求跳过: {0}", error);
                    return;
                }

                switch (request.Action)
                {
                    case MesRingLineAction.FeedingQianLiaocangSuccess:
                        await MesManager.Instance().PublishFeedingQianLiaocangSuccessAsync(
                                new FeedingQianLiaocangSuccessData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.UnloadingQianLiaocang:
                        await MesManager.Instance().PublishUnloadingQianLiaocangAsync(
                                new UnloadingQianLiaocangData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.FeedingZhongLiaocang:
                        await MesManager.Instance().PublishFeedingZhongLiaocangAsync(
                                new FeedingZhongLiaocangData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.UnloadingZhongLiaocang:
                        await MesManager.Instance().PublishUnloadingZhongLiaocangAsync(
                                new UnloadingZhongLiaocangData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.FeedingQingxihongganji:
                        await MesManager.Instance().PublishFeedingQingxihongganjiAsync(
                                new FeedingQingxihongganjiData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.FeedingHouLiaocang:
                        await MesManager.Instance().PublishFeedingHouLiaocangAsync(
                                new FeedingHouLiaocangData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.UnloadingHouLiaocang:
                        await MesManager.Instance().PublishUnloadingHouLiaocangAsync(
                                new UnloadingHouLiaocangData
                                {
                                    DeviceCode = MesManager.Instance().RingLineDeviceCode,
                                    PlateCode = request.PlateCode,
                                    FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                                    Timestamp = DateTime.Now
                                })
                            .ConfigureAwait(false);
                        break;

                    case MesRingLineAction.FeedingQianLiaocang:
                        _uiLogger.WarnRaw("MES Post请求不支持等待响应的动作: {0}", request.Action);
                        break;

                    default:
                        _uiLogger.WarnRaw("MES Post请求未知动作: {0}", request.Action);
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("MES Post请求异常: {0}", ex.Message);
            }
        }

        private bool EnsureMesReady(out string error)
        {
            error = null;

            var mesManager = MesManager.Instance();
            if (!mesManager.IsConnected)
            {
                if (!mesManager.ConfigureFromSystemParameters(connect: false) || !mesManager.Connect())
                {
                    error = "MES 未连接或配置失败";
                    return false;
                }
            }

            if (!mesManager.IsRingLineInitialized)
            {
                if (!mesManager.InitializeRingLineFromSystemParameters())
                {
                    error = "RingLine 服务未初始化（请检查 MesRingLineDeviceId/MesRingLineDeviceCode）";
                    return false;
                }
            }

            return true;
        }

        private async Task<MesRingLineFeedback> HandleFeedingQianLiaocangAsync(MesRingLineRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlateCode))
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "PlateCode 不能为空"
                };
            }

            EnsureFeedingQianLiaocangResponseSubscription();

            var timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : DefaultTimeoutMs;
            var tcs = new TaskCompletionSource<FeedingQianLiaocangResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_ringLineResponseLock)
            {
                if (_pendingFeedingQianLiaocangResponse != null)
                {
                    return new MesRingLineFeedback
                    {
                        CorrelationId = request.CorrelationId,
                        Action = request.Action,
                        Success = false,
                        Message = "上一个 FeedingQianLiaocang 请求仍在等待响应"
                    };
                }

                _pendingFeedingQianLiaocangResponse = tcs;
            }

            ushort publishId = 0;
            try
            {
                publishId = await MesManager.Instance()
                    .PublishFeedingQianLiaocangAsync(request.PlateCode, request.BillNoWip ?? string.Empty)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ClearPendingFeedingQianLiaocangResponse(tcs);
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "发送失败: " + ex.Message,
                    PublishMessageId = publishId
                };
            }

            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)).ConfigureAwait(false);

                if (completed != tcs.Task)
                {
                    ClearPendingFeedingQianLiaocangResponse(tcs);
                    return new MesRingLineFeedback
                    {
                        CorrelationId = request.CorrelationId,
                        Action = request.Action,
                        Success = false,
                        Message = $"等待 MES 响应超时({timeoutMs}ms)",
                        PublishMessageId = publishId
                    };
                }

                var response = await tcs.Task.ConfigureAwait(false);
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = response.Success,
                    Message = response.Message,
                    PublishMessageId = publishId,
                    Data = response
                };
            }
            catch (Exception ex)
            {
                ClearPendingFeedingQianLiaocangResponse(tcs);
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "等待 MES 响应失败: " + ex.Message,
                    PublishMessageId = publishId
                };
            }
        }

        private void EnsureFeedingQianLiaocangResponseSubscription()
        {
            lock (_ringLineResponseLock)
            {
                if (_feedingQianLiaocangResponseSubscription != null)
                {
                    return;
                }

                _feedingQianLiaocangResponseSubscription = MesManager.Instance().OnFeedingQianLiaocangResponse(ctx =>
                {
                    TaskCompletionSource<FeedingQianLiaocangResponseData> tcsToComplete = null;
                    FeedingQianLiaocangResponseData payload = null;

                    lock (_ringLineResponseLock)
                    {
                        tcsToComplete = _pendingFeedingQianLiaocangResponse;
                        _pendingFeedingQianLiaocangResponse = null;
                    }

                    try
                    {
                        payload = ctx?.Payload;
                    }
                    catch
                    {
                    }

                    if (tcsToComplete == null)
                    {
                        return;
                    }

                    if (payload == null)
                    {
                        tcsToComplete.TrySetException(new InvalidOperationException("MES 响应为空"));
                        return;
                    }

                    tcsToComplete.TrySetResult(payload);
                });
            }
        }

        private void ClearPendingFeedingQianLiaocangResponse(TaskCompletionSource<FeedingQianLiaocangResponseData> tcs)
        {
            if (tcs == null)
            {
                return;
            }

            lock (_ringLineResponseLock)
            {
                if (ReferenceEquals(_pendingFeedingQianLiaocangResponse, tcs))
                {
                    _pendingFeedingQianLiaocangResponse = null;
                }
            }
        }

        private async Task<MesRingLineFeedback> HandlePublishOnlyAsync(MesRingLineRequest request, Func<Task<ushort>> publishAsync)
        {
            if (string.IsNullOrWhiteSpace(request.PlateCode))
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "PlateCode 不能为空"
                };
            }

            if (NeedsFeedingLiaokuangCode(request.Action) && string.IsNullOrWhiteSpace(request.FeedingLiaokuangCode))
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "FeedingLiaokuangCode 不能为空"
                };
            }

            ushort publishId = 0;
            try
            {
                publishId = await publishAsync().ConfigureAwait(false);

                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = true,
                    Message = "已发送",
                    PublishMessageId = publishId
                };
            }
            catch (Exception ex)
            {
                return new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "发送失败: " + ex.Message,
                    PublishMessageId = publishId
                };
            }
        }

        private static bool NeedsFeedingLiaokuangCode(MesRingLineAction action)
        {
            switch (action)
            {
                case MesRingLineAction.FeedingQianLiaocangSuccess:
                case MesRingLineAction.UnloadingQianLiaocang:
                case MesRingLineAction.FeedingZhongLiaocang:
                case MesRingLineAction.UnloadingZhongLiaocang:
                case MesRingLineAction.FeedingHouLiaocang:
                case MesRingLineAction.UnloadingHouLiaocang:
                    return true;
                default:
                    return false;
            }
        }
    }
}
