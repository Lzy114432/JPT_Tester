using System;
using System.Threading;
using System.Threading.Tasks;
using Ewan.Core.Mes;
using Ewan.Core.Msg;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;

namespace Ewan.Core.Module
{
    /// <summary>
    /// MES 后台常驻模块（消息驱动模式）：
    /// - OnInit：注册 MsgSubject.MesRequest 监听器
    /// - OnMesRequestMessage：直接在回调中处理请求，并回推 MsgSubject.MesFeedback
    /// - OnRun：仅保持模块存活
    /// </summary>
    public class MesModule : BaseModule<MesModule>
    {
        private const int DefaultTimeoutMs = 30000;
        private const int RunLoopIntervalMs = 100;

        private MsgListener _requestListener;

        private readonly object _ringLineResponseLock = new object();
        private IDisposable _feedingQianLiaocangResponseSubscription;
        private TaskCompletionSource<FeedingQianLiaocangResponseData> _pendingFeedingQianLiaocangResponse;

        protected override void OnInit()
        {
            _requestListener = new MsgListener(MsgSubject.MesRequest, OnMesRequestMessage);
            MsgManager.Instance().RegisterListener(_requestListener);

            _uiLogger.InfoRaw("模块初始化成功: {0}", "MesModule");
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
                if (_requestListener != null)
                {
                    MsgManager.Instance().UnRegisterListener(_requestListener);
                    _requestListener = null;
                }
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
        /// 消息回调：直接处理 MES 请求（消息驱动模式）
        /// </summary>
        private void OnMesRequestMessage(MessageModel msg)
        {
            MesRingLineRequest request;
            try
            {
                request = msg.GetData<MesRingLineRequest>();
            }
            catch (Exception ex)
            {
                _uiLogger.WarnRaw("MES 请求消息解析失败: {0}", ex.Message);
                return;
            }

            if (request == null)
            {
                return;
            }

            if (request.CorrelationId == Guid.Empty)
            {
                request.CorrelationId = Guid.NewGuid();
            }

            // 直接在回调中处理请求
            try
            {
                ProcessRequest(request);
            }
            catch (Exception ex)
            {
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "处理 MES 请求异常: " + ex.Message
                });
            }
        }

        private void ProcessRequest(MesRingLineRequest request)
        {
            string error;
            if (!EnsureMesReady(out error))
            {
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = error
                });
                return;
            }

            switch (request.Action)
            {
                case MesRingLineAction.FeedingQianLiaocang:
                    HandleFeedingQianLiaocang(request);
                    return;

                case MesRingLineAction.FeedingQianLiaocangSuccess:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishFeedingQianLiaocangSuccessAsync(request.PlateCode, request.FeedingLiaokuangCode));
                    return;

                case MesRingLineAction.UnloadingQianLiaocang:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishUnloadingQianLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode));
                    return;

                case MesRingLineAction.FeedingZhongLiaocang:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishFeedingZhongLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode));
                    return;

                case MesRingLineAction.UnloadingZhongLiaocang:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishUnloadingZhongLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode));
                    return;

                case MesRingLineAction.FeedingQingxihongganji:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishFeedingQingxihongganjiAsync(request.PlateCode));
                    return;

                case MesRingLineAction.FeedingHouLiaocang:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishFeedingHouLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode));
                    return;

                case MesRingLineAction.UnloadingHouLiaocang:
                    HandlePublishOnly(
                        request,
                        () => MesManager.Instance().PublishUnloadingHouLiaocangAsync(request.PlateCode, request.FeedingLiaokuangCode));
                    return;

                default:
                    PublishFeedback(new MesRingLineFeedback
                    {
                        CorrelationId = request.CorrelationId,
                        Action = request.Action,
                        Success = false,
                        Message = "未知的 MES RingLine 请求类型"
                    });
                    return;
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

        private void HandleFeedingQianLiaocang(MesRingLineRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlateCode))
            {
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "PlateCode 不能为空"
                });
                return;
            }

            EnsureFeedingQianLiaocangResponseSubscription();

            var timeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : DefaultTimeoutMs;
            var tcs = new TaskCompletionSource<FeedingQianLiaocangResponseData>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_ringLineResponseLock)
            {
                if (_pendingFeedingQianLiaocangResponse != null)
                {
                    PublishFeedback(new MesRingLineFeedback
                    {
                        CorrelationId = request.CorrelationId,
                        Action = request.Action,
                        Success = false,
                        Message = "上一个 FeedingQianLiaocang 请求仍在等待响应"
                    });
                    return;
                }

                _pendingFeedingQianLiaocangResponse = tcs;
            }

            ushort publishId = 0;
            try
            {
                publishId = MesManager.Instance()
                    .PublishFeedingQianLiaocangAsync(request.PlateCode, request.BillNoWip ?? string.Empty)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                ClearPendingFeedingQianLiaocangResponse(tcs);
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "发送失败: " + ex.Message,
                    PublishMessageId = publishId
                });
                return;
            }

            try
            {
                var completed = Task.WhenAny(tcs.Task, Task.Delay(timeoutMs))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                if (completed != tcs.Task)
                {
                    ClearPendingFeedingQianLiaocangResponse(tcs);
                    PublishFeedback(new MesRingLineFeedback
                    {
                        CorrelationId = request.CorrelationId,
                        Action = request.Action,
                        Success = false,
                        Message = $"等待 MES 响应超时({timeoutMs}ms)",
                        PublishMessageId = publishId
                    });
                    return;
                }

                var response = tcs.Task.ConfigureAwait(false).GetAwaiter().GetResult();
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = response.Success,
                    Message = response.Message,
                    PublishMessageId = publishId,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                ClearPendingFeedingQianLiaocangResponse(tcs);
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "等待 MES 响应失败: " + ex.Message,
                    PublishMessageId = publishId
                });
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

        private void HandlePublishOnly(MesRingLineRequest request, Func<Task<ushort>> publishAsync)
        {
            if (string.IsNullOrWhiteSpace(request.PlateCode))
            {
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "PlateCode 不能为空"
                });
                return;
            }

            if (NeedsFeedingLiaokuangCode(request.Action) && string.IsNullOrWhiteSpace(request.FeedingLiaokuangCode))
            {
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "FeedingLiaokuangCode 不能为空"
                });
                return;
            }

            ushort publishId = 0;
            try
            {
                publishId = publishAsync()
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = true,
                    Message = "已发送",
                    PublishMessageId = publishId
                });
            }
            catch (Exception ex)
            {
                PublishFeedback(new MesRingLineFeedback
                {
                    CorrelationId = request.CorrelationId,
                    Action = request.Action,
                    Success = false,
                    Message = "发送失败: " + ex.Message,
                    PublishMessageId = publishId
                });
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

        private void PublishFeedback(MesRingLineFeedback feedback)
        {
            MsgManager.Instance().PushMsg(new MessageModel(MsgSubject.MesFeedback, feedback));
        }
    }
}
