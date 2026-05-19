/*****************************************************
** 命名空间: Ewan.Mes.Services.ZHJW
** 文 件 名：RingLineService
** 内容简述：环线服务实现
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/15
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using Ewan.Mes.Devices;
using Ewan.Mes.Devices.ZHJW.DicingMachine;
using Ewan.Mes.Devices.ZHJW.RingLine;
using Ewan.Mes.Diagnostics;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Models.Dto.ZHJW.RingLine;
using Ewan.Mes.Transport;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RingLineMappers = Ewan.Mes.Mappers.ZHJW.RingLine;

namespace Ewan.Mes.Services.ZHJW
{
    /// <summary>
    /// 环线服务实现
    /// </summary>
    public class RingLineService : IRingLineService, IDisposable
    {
        private const string DeviceType_RingLine = "RingLine";
        private const string LogSource = "RingLineService";
        
        private readonly IMessageTransport _transport;
        private readonly string _deviceId;
        private readonly string _deviceCode;
        private readonly IDictionary<string, string> _tokens;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _disposed = false;

        // ---- 新增：对下行响应的内部分发（单次 transport 订阅 + 多个 handler）
        private readonly object _feedingQianLiaocangRespHandlersLock = new object();
        private List<Action<MessageContext<FeedingQianLiaocangResponseData>>> _feedingQianLiaocangRespHandlers = new List<Action<MessageContext<FeedingQianLiaocangResponseData>>>();
        private IDisposable _feedingQianLiaocangRespTransportSubscription = null;


        private readonly object _respFeedingUnloadingStateHandlersLock = new object();
        private List<Action<MessageContext<FeedingUnloadingStateResponseData>>> _respFeedingUnloadingStateHandlers = new List<Action<MessageContext<FeedingUnloadingStateResponseData>>>();
        private IDisposable _respFeedingUnloadingStateTransportSubscription = null;
        // 新增：用于在后台线程刷入并维护按 Device_Code 索引的最新状态字典
        private readonly ConcurrentDictionary<string, FeedingUnloadingStateResponseData> _feedingUnloadingStateMap = new ConcurrentDictionary<string, FeedingUnloadingStateResponseData>();
        private readonly BlockingCollection<FeedingUnloadingStateResponseData> _feedingUnloadingStateQueue = new BlockingCollection<FeedingUnloadingStateResponseData>(new ConcurrentQueue<FeedingUnloadingStateResponseData>());
        private readonly CancellationTokenSource _feedingUnloadingStateCts = new CancellationTokenSource();
        private Task _feedingUnloadingStateWorkerTask;
        public RingLineService(
            IMessageTransport transport,
            string deviceId,
            string deviceCode)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            if (string.IsNullOrWhiteSpace(deviceId))
                throw new ArgumentException("deviceId 不能为空", nameof(deviceId));
            if (string.IsNullOrWhiteSpace(deviceCode))
                throw new ArgumentException("deviceCode 不能为空", nameof(deviceCode));

            _transport = transport;
            _deviceId = deviceId;
            _deviceCode = deviceCode;
            _tokens = new Dictionary<string, string>
            {
                { "设备ID", _deviceId },
                { "设备编码", _deviceCode }
            };   
            // 启动后台工作线程来更新字典
            _feedingUnloadingStateWorkerTask = Task.Run(() => ProcessFeedingUnloadingStateQueue(_feedingUnloadingStateCts.Token));
         
        }

        public string DeviceType => DeviceType_RingLine;
        public string DeviceId => _deviceId;
        public string DeviceCode => _deviceCode;
        public bool IsConnected => _transport?.IsConnected ?? false;

        #region IDeviceService 实现

        public ushort PublishMessage<T>(EndpointTemplate endpoint, T payload)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            
            try
            {
                return _transport.Publish(endpoint, payload, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, $"PublishMessage 失败: {endpoint.Template}", ex, payload);
                throw;
            }
        }

        public IDisposable RegisterHandler<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = _transport.Subscribe<T>(endpoint, handler, _tokens);

            _subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region 前料仓上料

        public ushort PublishFeedingQianLiaocang(FeedingQianLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingQianLiaocangMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.FeedingQianLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingQianLiaocang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFeedingQianLiaocangAsync(FeedingQianLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingQianLiaocangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.FeedingQianLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingQianLiaocangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 前料仓上料成功

        public ushort PublishFeedingQianLiaocangSuccess(FeedingQianLiaocangSuccessData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingQianLiaocangSuccessMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.FeedingQianLiaocangSuccess, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingQianLiaocangSuccess 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFeedingQianLiaocangSuccessAsync(FeedingQianLiaocangSuccessData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingQianLiaocangSuccessMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.FeedingQianLiaocangSuccess, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingQianLiaocangSuccessAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 前料仓卸料

        public ushort PublishUnloadingQianLiaocang(UnloadingQianLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.UnloadingQianLiaocangMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.UnloadingQianLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingQianLiaocang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishUnloadingQianLiaocangAsync(UnloadingQianLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.UnloadingQianLiaocangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.UnloadingQianLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingQianLiaocangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 中料仓上料

        public ushort PublishFeedingZhongLiaocang(FeedingZhongLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingZhongLiaocangMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.FeedingZhongLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingZhongLiaocang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFeedingZhongLiaocangAsync(FeedingZhongLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingZhongLiaocangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.FeedingZhongLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingZhongLiaocangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 中料仓卸料

        public ushort PublishUnloadingZhongLiaocang(UnloadingZhongLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.UnloadingZhongLiaocangMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.UnloadingZhongLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingZhongLiaocang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishUnloadingZhongLiaocangAsync(UnloadingZhongLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.UnloadingZhongLiaocangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.UnloadingZhongLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingZhongLiaocangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 清洗烘干机上料

        public ushort PublishFeedingQingxihongganji(FeedingQingxihongganjiData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingQingxihongganjiMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.FeedingQingxihongganji, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingQingxihongganji 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFeedingQingxihongganjiAsync(FeedingQingxihongganjiData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingQingxihongganjiMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.FeedingQingxihongganji, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingQingxihongganjiAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 后料仓上料

        public ushort PublishFeedingHouLiaocang(FeedingHouLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingHouLiaocangMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.FeedingHouLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingHouLiaocang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFeedingHouLiaocangAsync(FeedingHouLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.FeedingHouLiaocangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.FeedingHouLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingHouLiaocangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 后料仓卸料

        public ushort PublishUnloadingHouLiaocang(UnloadingHouLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.UnloadingHouLiaocangMapper.Instance.ToDto(payload);
                return _transport.Publish(RingLineTopics.Up.UnloadingHouLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingHouLiaocang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishUnloadingHouLiaocangAsync(UnloadingHouLiaocangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RingLineMappers.UnloadingHouLiaocangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(RingLineTopics.Up.UnloadingHouLiaocang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingHouLiaocangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 前料仓上料响应

        // 修改：只在服务内部创建一次 transport 订阅并分发给所有注册的 handler
        public IDisposable OnFeedingQianLiaocangResponse(Action<MessageContext<FeedingQianLiaocangResponseData>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_feedingQianLiaocangRespHandlersLock)
            {
                _feedingQianLiaocangRespHandlers.Add(handler);

                if (_feedingQianLiaocangRespTransportSubscription == null)
                {
                    _feedingQianLiaocangRespTransportSubscription = _transport.Subscribe<FeedingQianLiaocangResponse>(RingLineTopics.Down.FeedingQianLiaocangResponse, ctx =>
                    {
                        try
                        {
                            var domain = RingLineMappers.FeedingQianLiaocangResponseMapper.Instance.ToDomain(ctx.Payload);
                            var domainContext = new MessageContext<FeedingQianLiaocangResponseData>(
                                ctx.Topic,
                                domain,
                                ctx.RawPayload,
                                ctx.Metadata);

                            List<Action<MessageContext<FeedingQianLiaocangResponseData>>> handlersSnapshot;
                            lock (_feedingQianLiaocangRespHandlersLock)
                            {
                                handlersSnapshot = new List<Action<MessageContext<FeedingQianLiaocangResponseData>>>(_feedingQianLiaocangRespHandlers);
                            }

                            foreach (var h in handlersSnapshot)
                            {
                                try
                                {
                                    h(domainContext);
                                }
                                catch (Exception ex)
                                {
                                    MesErrorHandler.Error(LogSource, "OnFeedingQianLiaocangResponse handler 调用失败", ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MesErrorHandler.Error(LogSource, "OnFeedingQianLiaocangResponse 处理失败", ex);
                        }
                    }, _tokens);

                    if (_feedingQianLiaocangRespTransportSubscription != null)
                        _subscriptions.Add(_feedingQianLiaocangRespTransportSubscription);
                }
            }

            return new SubscriptionDisposable(() =>
            {
                lock (_feedingQianLiaocangRespHandlersLock)
                {
                    _feedingQianLiaocangRespHandlers.Remove(handler);
                    if (_feedingQianLiaocangRespHandlers.Count == 0)
                    {
                        try
                        {
                            _feedingQianLiaocangRespTransportSubscription?.Dispose();
                        }
                        catch { /* ignore */ }

                        if (_feedingQianLiaocangRespTransportSubscription != null)
                        {
                            _subscriptions.Remove(_feedingQianLiaocangRespTransportSubscription);
                            _feedingQianLiaocangRespTransportSubscription = null;
                        }
                    }
                }
            });
        }
        //public IDisposable OnFeedingUnloadingStateResponseModel(Action<MessageContext<FeedingUnloadingStateResponseData>> handler)
        //{
        //    if (handler == null)
        //        throw new ArgumentNullException(nameof(handler));

        //    // 添加 handler 并确保底层 transport 只订阅一次
        //    lock (_respFeedingUnloadingStateHandlersLock)
        //    {
        //        _respFeedingUnloadingStateHandlers.Add(handler);

        //        if (_respFeedingUnloadingStateTransportSubscription == null)
        //        {
        //            // 创建底层订阅，收到消息后分发到当前 handler 列表
        //            _respFeedingUnloadingStateTransportSubscription = _transport.Subscribe<FeedingUnloadingStateResponseModel>(DicingMachineTopics.Down.FeedingUnloadingStateResponse, ctx =>
        //            {
        //                try
        //                {
        //                    var domain = RingLineMappers.FeedingUnloadingStateResponseModelMapper.Instance.ToDomain(ctx.Payload);
        //                    var domainContext = new MessageContext<FeedingUnloadingStateResponseData>(
        //                        ctx.Topic,
        //                        domain,
        //                        ctx.RawPayload,
        //                        ctx.Metadata);

        //                    // 将消息放入后台处理队列，由工作线程更新字典
        //                    try
        //                    {
        //                        if (domain != null)
        //                            _feedingUnloadingStateQueue.Add(domain);
        //                    }
        //                    catch (Exception qex)
        //                    {
        //                        MesErrorHandler.Error(LogSource, "向队列添加 FeedingUnloadingStateResponseData 失败", qex);
        //                    }

        //                    // 复制当前 handlers 并逐个调用，避免在回调中修改集合导致问题
        //                    List<Action<MessageContext<FeedingUnloadingStateResponseData>>> handlersSnapshot;
        //                    lock (_respFeedingUnloadingStateHandlersLock)
        //                    {
        //                        handlersSnapshot = new List<Action<MessageContext<FeedingUnloadingStateResponseData>>>(_respFeedingUnloadingStateHandlers);
        //                    }


        //                    foreach (var h in handlersSnapshot)
        //                    {
        //                        try
        //                        {
        //                            h(domainContext);
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            MesErrorHandler.Error(LogSource, "OnFeedingUnloadingStateResponseModel handler 调用失败", ex);
        //                        }
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    MesErrorHandler.Error(LogSource, "OnFeedingUnloadingStateResponseModel 处理失败", ex);
        //                }
        //            }, _tokens);

        //            // 把底层订阅也加入 _subscriptions，这样 Dispose 时能统一释放
        //            if (_respFeedingUnloadingStateTransportSubscription != null)
        //                _subscriptions.Add(_respFeedingUnloadingStateTransportSubscription);
        //        }
        //    }

        //    // 返回一个 IDisposable，取消当前 handler 的注册；当 handler 列表为空时释放底层 transport 订阅
        //    return new SubscriptionDisposable(() =>
        //    {
        //        lock (_respFeedingUnloadingStateHandlersLock)
        //        {
        //            _respFeedingUnloadingStateHandlers.Remove(handler);
        //            if (_respFeedingUnloadingStateHandlers.Count == 0)
        //            {
        //                try
        //                {
        //                    _respFeedingUnloadingStateTransportSubscription?.Dispose();
        //                }
        //                catch { /* ignore */ }

        //                if (_respFeedingUnloadingStateTransportSubscription != null)
        //                {
        //                    // 从 _subscriptions 中移除该 transport 订阅（以便 Dispose 时不会重复 Dispose）
        //                    _subscriptions.Remove(_respFeedingUnloadingStateTransportSubscription);
        //                    _respFeedingUnloadingStateTransportSubscription = null;
        //                }
        //            }
        //        }
        //    });
        //}
        // 补充处理队列的方法
        private void ProcessFeedingUnloadingStateQueue(CancellationToken token)
        {
            try
            {
                foreach (var state in _feedingUnloadingStateQueue.GetConsumingEnumerable(token))
                {
                    if (!string.IsNullOrWhiteSpace(state.Device_Code))
                    {
                        // 在这里或者使用其他唯一键（如设备编码）向 _feedingUnloadingStateMap 赋值
                        _feedingUnloadingStateMap.AddOrUpdate(state.Device_Code, state, (key, oldVal) => state);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 线程正常取消
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "处理设备状态队列异常", ex);
            }
        }
        #endregion

        #region 资源释放

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription?.Dispose();
                }
                _subscriptions.Clear();

                if (_transport is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _disposed = true;
        }

        #endregion
        // 公共方法：获取当前字典快照或指定设备编码的最新状态
        public bool TryGetFeedingUnloadingState(string deviceCode, out FeedingUnloadingStateResponseData state)
        {
            if (string.IsNullOrWhiteSpace(deviceCode))
            {
                state = null;
                return false;
            }

            return _feedingUnloadingStateMap.TryGetValue(deviceCode, out state);
        }

        public IReadOnlyDictionary<string, FeedingUnloadingStateResponseData> GetFeedingUnloadingStateSnapshot()
        {
            return new Dictionary<string, FeedingUnloadingStateResponseData>(_feedingUnloadingStateMap);
        }

        Dictionary<string, FeedingUnloadingStateResponseData> IRingLineService.GetFeedingUnloadingStateSnapshot()
        {
            return new Dictionary<string, FeedingUnloadingStateResponseData>(_feedingUnloadingStateMap);
        }

        // 简单的 IDisposable 实现用于包装注销动作
        private class SubscriptionDisposable : IDisposable
        {
            private readonly Action _disposeAction;
            private bool _disposed = false;

            public SubscriptionDisposable(Action disposeAction)
            {
                _disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
            }

            public void Dispose()
            {
                if (_disposed) return;
                try { _disposeAction(); } catch { }
                _disposed = true;
            }


       
        }
    }
}
