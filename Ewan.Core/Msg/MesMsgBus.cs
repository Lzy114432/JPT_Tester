using System;
using System.Threading;
using System.Threading.Tasks;
using Ewan.Core.Attribute;

namespace Ewan.Core.Msg
{
    /// <summary>
    /// MES 请求/反馈总线：对外提供 RequestAsync，内部用 MsgManager 分发 + RequestAwaiter 等待反馈。
    /// </summary>
    [Manager(Priority = 2)]
    public class MesMsgBus : BaseManager<MesMsgBus>
    {
        private readonly RequestAwaiter<Guid, MesRingLineFeedback> _awaiter = new RequestAwaiter<Guid, MesRingLineFeedback>();
        private MsgListener _feedbackListener;
        private int _initialized;

        public override bool Init()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
            {
                return true;
            }

            _feedbackListener = new MsgListener(MsgSubject.MesFeedback, OnMesFeedback);
            MsgManager.Instance().RegisterListener(_feedbackListener);
            _uiLogger.InfoRaw("Module initialized: {0}", "MesMsgBus");
            return base.Init();
        }

        public override void Destroy()
        {
            try
            {
                if (_feedbackListener != null)
                {
                    MsgManager.Instance().UnRegisterListener(_feedbackListener);
                    _feedbackListener = null;
                }

                _awaiter.Dispose();
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("MesMsgBus destroy error: {0}", ex.Message);
            }

            base.Destroy();
        }

        public Task<MesRingLineFeedback> RequestAsync(
            MesRingLineRequest request,
            int timeoutMs = 30000,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.CorrelationId == Guid.Empty)
            {
                request.CorrelationId = Guid.NewGuid();
            }

            var effectiveTimeoutMs = request.TimeoutMs > 0 ? request.TimeoutMs : timeoutMs;
            request.TimeoutMs = effectiveTimeoutMs;

            var task = _awaiter.Register(request.CorrelationId, effectiveTimeoutMs, cancellationToken);
            MsgManager.Instance().PushMsg(new MessageModel(MsgSubject.MesRequest, request));
            return task;
        }

        /// <summary>
        /// 仅发送（不等待反馈）。
        /// </summary>
        public Guid Post(MesRingLineRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.CorrelationId == Guid.Empty)
            {
                request.CorrelationId = Guid.NewGuid();
            }

            MsgManager.Instance().PushMsg(new MessageModel(MsgSubject.MesRequest, request));
            return request.CorrelationId;
        }

        private void OnMesFeedback(MessageModel msg)
        {
            try
            {
                var feedback = msg.GetData<MesRingLineFeedback>();
                if (feedback == null || feedback.CorrelationId == Guid.Empty)
                {
                    return;
                }

                _awaiter.TrySetResult(feedback.CorrelationId, feedback);
            }
            catch (Exception ex)
            {
                _uiLogger.WarnRaw("MesMsgBus feedback handle failed: {0}", ex.Message);
            }
        }
    }
}
