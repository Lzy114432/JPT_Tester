using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ewan.Mes.Devices;
using Ewan.Mes.Devices.ZHJW.LaserCutting;
using Ewan.Mes.Models.Domain.ZHJW.LaserCutting;
using Ewan.Mes.Models.Dto.ZHJW.LaserCutting;
using Ewan.Mes.Transport;
using Ewan.Mes.Diagnostics;
using CuttingMappers = Ewan.Mes.Mappers.ZHJW.LaserCutting;

namespace Ewan.Mes.Services.ZHJW
{
    /// <summary>
    /// 激光切割机服务实现（协议无关）
    /// 使用 Domain 模型与业务交互，内部使用 Dto 进行传输
    /// </summary>
    public class LaserCuttingService : ILaserCuttingService, IDisposable
    {
        private const string DeviceType_LaserCutting = "LaserCutting";
        private const string LogSource = "LaserCuttingService";

        private readonly IMessageTransport _transport;
        private readonly string _deviceId;
        private readonly string _deviceCode;
        private readonly IDictionary<string, string> _tokens;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _disposed = false;

        // ---- 新增：对下行 ResponseModel 的内部分发（单次 transport 订阅 + 多个 handler）
        private readonly object _respHandlersLock = new object();
        private List<Action<MessageContext<ResponseModelData>>> _respModelHandlers = new List<Action<MessageContext<ResponseModelData>>>();
        private IDisposable _respModelTransportSubscription = null;
        private volatile ResponseModelData _lastResponseModelCache = null;
        public ResponseModelData LastResponseModel => _lastResponseModelCache;

        public LaserCuttingService(
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
        }

        public string DeviceType => DeviceType_LaserCutting;
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

        #region 上行请求 - 扫描板片

        public ushort PublishScanPlate(ScanPlateData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.ScanPlateMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.ScanPlate, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanPlate 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishScanPlateAsync(ScanPlateData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.ScanPlateMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.ScanPlate, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanPlateAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 请求规格型号

        public ushort PublishRequestModel(RequestModelData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.RequestModelMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.RequestModel, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishRequestModel 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishRequestModelAsync(RequestModelData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.RequestModelMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.RequestModel, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishRequestModelAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 扫描NG

        public ushort PublishScanNg(ScanNgData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.ScanNgMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.ScanNg, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanNg 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishScanNgAsync(ScanNgData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.ScanNgMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.ScanNg, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanNgAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 报警

        public ushort PublishAlarm(AlarmData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.AlarmMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.Alarm, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishAlarm 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishAlarmAsync(AlarmData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.AlarmMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.Alarm, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishAlarmAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 上报生产数据

        public ushort PublishReportData(ReportWorkData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.ReportWorkMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.ReportData, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishReportData 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishReportDataAsync(ReportWorkData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.ReportWorkMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.ReportData, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishReportDataAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 上报配方

        public ushort PublishFormula(FormulaData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.FormulaMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.Formula, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFormula 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFormulaAsync(FormulaData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.FormulaMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.Formula, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFormulaAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 设备状态

        public ushort PublishDeviceState(DeviceStateData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.DeviceStateMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.DeviceState, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishDeviceState 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishDeviceStateAsync(DeviceStateData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.DeviceStateMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.DeviceState, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishDeviceStateAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 心跳

        public ushort PublishHeartbeat(HeartbeatData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.HeartbeatMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserCuttingTopics.Up.Heartbeat, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishHeartbeat 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishHeartbeatAsync(HeartbeatData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = CuttingMappers.HeartbeatMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserCuttingTopics.Up.Heartbeat, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishHeartbeatAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 下行响应 - 规格型号响应

        // 修改：只在服务内部创建一次 transport 订阅并分发给所有注册的 handler
        public IDisposable OnResponseModel(Action<MessageContext<ResponseModelData>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            // 添加 handler 并确保底层 transport 只订阅一次
            lock (_respHandlersLock)
            {
                _respModelHandlers.Add(handler);

                if (_respModelTransportSubscription == null)
                {
                    // 创建底层订阅，收到消息后分发到当前 handler 列表
                    _respModelTransportSubscription = _transport.Subscribe<CuttingResponseModel>(LaserCuttingTopics.Down.ResponseModel, ctx =>
                    {
                        try
                        {
                            var domain = CuttingMappers.ResponseModelMapper.Instance.ToDomain(ctx.Payload);
                            var domainContext = new MessageContext<ResponseModelData>(
                                ctx.Topic,
                                domain,
                                ctx.RawPayload,
                                ctx.Metadata);

                            // 更新缓存（方便外部直接读取 Latest）
                            _lastResponseModelCache = domain;

                            // 复制当前 handlers 并逐个调用，避免在回调中修改集合导致问题
                            List<Action<MessageContext<ResponseModelData>>> handlersSnapshot;
                            lock (_respHandlersLock)
                            {
                                handlersSnapshot = new List<Action<MessageContext<ResponseModelData>>>(_respModelHandlers);
                            }

                            foreach (var h in handlersSnapshot)
                            {
                                try
                                {
                                    h(domainContext);
                                }
                                catch (Exception ex)
                                {
                                    MesErrorHandler.Error(LogSource, "OnResponseModel handler 调用失败", ex);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MesErrorHandler.Error(LogSource, "OnResponseModel 处理失败", ex);
                        }
                    }, _tokens);

                    // 把底层订阅也加入 _subscriptions，这样 Dispose 时能统一释放
                    if (_respModelTransportSubscription != null)
                        _subscriptions.Add(_respModelTransportSubscription);
                }
            }

            // 返回一个 IDisposable，取消当前 handler 的注册；当 handler 列表为空时释放底层 transport 订阅
            return new SubscriptionDisposable(() =>
            {
                lock (_respHandlersLock)
                {
                    _respModelHandlers.Remove(handler);
                    if (_respModelHandlers.Count == 0)
                    {
                        try
                        {
                            _respModelTransportSubscription?.Dispose();
                        }
                        catch { /* ignore */ }

                        if (_respModelTransportSubscription != null)
                        {
                            // 从 _subscriptions 中移除该 transport 订阅（以便 Dispose 时不会重复 Dispose）
                            _subscriptions.Remove(_respModelTransportSubscription);
                            _respModelTransportSubscription = null;
                        }
                    }
                }
            });
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
