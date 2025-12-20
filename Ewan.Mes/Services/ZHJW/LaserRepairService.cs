using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ewan.Mes.Devices;
using Ewan.Mes.Devices.ZHJW.LaserRepair;
using Ewan.Mes.Models.Domain.ZHJW.LaserRepair;
using Ewan.Mes.Models.Dto.ZHJW.LaserRepair;
using Ewan.Mes.Transport;
using Ewan.Mes.Diagnostics;
using RepairMappers = Ewan.Mes.Mappers.ZHJW.LaserRepair;

namespace Ewan.Mes.Services.ZHJW
{
    /// <summary>
    /// 镭修机服务实现（协议无关）
    /// 使用 Domain 模型与业务交互，内部使用 Dto 进行传输
    /// </summary>
    public class LaserRepairService : ILaserRepairService, IDisposable
    {
        private const string DeviceType_LaserRepair = "LaserRepair";
        private const string LogSource = "LaserRepairService";
        
        private readonly IMessageTransport _transport;
        private readonly string _deviceId;
        private readonly string _deviceCode;
        private readonly IDictionary<string, string> _tokens;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _disposed = false;

        public LaserRepairService(
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

        public string DeviceType => DeviceType_LaserRepair;
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

        #region 上行请求 - 上料

        public ushort PublishFeeding(FeedingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.FeedingMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.Feeding, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeeding 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishFeedingAsync(FeedingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.FeedingMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.Feeding, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishFeedingAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 确认上料

        public ushort PublishConfirmFeeding(ConfirmFeedingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ConfirmFeedingMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.ConfirmFeeding, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishConfirmFeeding 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishConfirmFeedingAsync(ConfirmFeedingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ConfirmFeedingMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.ConfirmFeeding, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishConfirmFeedingAsync 失败", ex, payload);
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
                var dto = RepairMappers.ScanNgMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.ScanNg, dto, _tokens, false);
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
                var dto = RepairMappers.ScanNgMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.ScanNg, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanNgAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 扫描板号

        public ushort PublishScanPlate(ScanPlateData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ScanPlateMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.ScanPlate, dto, _tokens, false);
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
                var dto = RepairMappers.ScanPlateMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.ScanPlate, dto, _tokens, false);
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
                var dto = RepairMappers.RequestModelMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.RequestModel, dto, _tokens, false);
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
                var dto = RepairMappers.RequestModelMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.RequestModel, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishRequestModelAsync 失败", ex, payload);
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
                var dto = RepairMappers.AlarmMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.Alarm, dto, _tokens, false);
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
                var dto = RepairMappers.AlarmMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.Alarm, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishAlarmAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 报工

        public ushort PublishReportData(ReportWorkData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ReportWorkMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.ReportData, dto, _tokens, false);
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
                var dto = RepairMappers.ReportWorkMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.ReportData, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishReportDataAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 完成镂空

        public ushort PublishCompleteLiaokuang(CompleteLiaokuangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.CompleteLiaokuangMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.CompleteLiaokuang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishCompleteLiaokuang 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishCompleteLiaokuangAsync(CompleteLiaokuangData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.CompleteLiaokuangMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.CompleteLiaokuang, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishCompleteLiaokuangAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 卸料

        public ushort PublishUnloading(UnloadingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.UnloadingMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.Unloading, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloading 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishUnloadingAsync(UnloadingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.UnloadingMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.Unloading, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishUnloadingAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 确认卸料

        public ushort PublishConfirmUnloading(ConfirmUnloadingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ConfirmUnloadingMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.ConfirmUnloading, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishConfirmUnloading 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishConfirmUnloadingAsync(ConfirmUnloadingData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ConfirmUnloadingMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.ConfirmUnloading, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishConfirmUnloadingAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 配方

        public ushort PublishFormula(FormulaData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.FormulaMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.Formula, dto, _tokens, false);
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
                var dto = RepairMappers.FormulaMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.Formula, dto, _tokens, false);
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
                var dto = RepairMappers.DeviceStateMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.DeviceState, dto, _tokens, false);
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
                var dto = RepairMappers.DeviceStateMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.DeviceState, dto, _tokens, false);
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
                var dto = RepairMappers.HeartbeatMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.Heartbeat, dto, _tokens, false);
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
                var dto = RepairMappers.HeartbeatMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.Heartbeat, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishHeartbeatAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 上行请求 - 扫描探针卡

        public ushort PublishScanProbecard(ScanProbecardData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ScanProbecardMapper.Instance.ToDto(payload);
                return _transport.Publish(LaserRepairTopics.Up.ScanProbecard, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanProbecard 失败", ex, payload);
                throw;
            }
        }

        public async Task<ushort> PublishScanProbecardAsync(ScanProbecardData payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            try
            {
                var dto = RepairMappers.ScanProbecardMapper.Instance.ToDto(payload);
                return await _transport.PublishAsync(LaserRepairTopics.Up.ScanProbecard, dto, _tokens, false);
            }
            catch (Exception ex)
            {
                MesErrorHandler.Error(LogSource, "PublishScanProbecardAsync 失败", ex, payload);
                throw;
            }
        }

        #endregion

        #region 下行响应 - 上料响应

        public IDisposable OnFeedingResponse(Action<MessageContext<FeedingResponseData>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = _transport.Subscribe<FeedingResponse>(LaserRepairTopics.Down.FeedingResponse, ctx =>
            {
                try
                {
                    var domain = RepairMappers.FeedingResponseMapper.Instance.ToDomain(ctx.Payload);
                    var domainContext = new MessageContext<FeedingResponseData>(
                        ctx.Topic,
                        domain,
                        ctx.RawPayload,
                        ctx.Metadata);
                    handler(domainContext);
                }
                catch (Exception ex)
                {
                    MesErrorHandler.Error(LogSource, "OnFeedingResponse 处理失败", ex);
                }
            }, _tokens);

            _subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region 下行响应 - 规格型号响应

        public IDisposable OnResponseModel(Action<MessageContext<ResponseModelData>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = _transport.Subscribe<ResponseModel>(LaserRepairTopics.Down.ResponseModel, ctx =>
            {
                try
                {
                    var domain = RepairMappers.ResponseModelMapper.Instance.ToDomain(ctx.Payload);
                    var domainContext = new MessageContext<ResponseModelData>(
                        ctx.Topic,
                        domain,
                        ctx.RawPayload,
                        ctx.Metadata);
                    handler(domainContext);
                }
                catch (Exception ex)
                {
                    MesErrorHandler.Error(LogSource, "OnResponseModel 处理失败", ex);
                }
            }, _tokens);

            _subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region 下行响应 - 卸料响应

        public IDisposable OnUnloadingResponse(Action<MessageContext<UnloadingResponseData>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = _transport.Subscribe<UnloadingResponse>(LaserRepairTopics.Down.UnloadingResponse, ctx =>
            {
                try
                {
                    var domain = RepairMappers.UnloadingResponseMapper.Instance.ToDomain(ctx.Payload);
                    var domainContext = new MessageContext<UnloadingResponseData>(
                        ctx.Topic,
                        domain,
                        ctx.RawPayload,
                        ctx.Metadata);
                    handler(domainContext);
                }
                catch (Exception ex)
                {
                    MesErrorHandler.Error(LogSource, "OnUnloadingResponse 处理失败", ex);
                }
            }, _tokens);

            _subscriptions.Add(subscription);
            return subscription;
        }

        #endregion

        #region 下行响应 - 探针卡响应

        public IDisposable OnScanProbecardResponse(Action<MessageContext<ScanProbecardResponseData>> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = _transport.Subscribe<ScanProbecardResponse>(LaserRepairTopics.Down.ScanProbecardResponse, ctx =>
            {
                try
                {
                    var domain = RepairMappers.ScanProbecardResponseMapper.Instance.ToDomain(ctx.Payload);
                    var domainContext = new MessageContext<ScanProbecardResponseData>(
                        ctx.Topic,
                        domain,
                        ctx.RawPayload,
                        ctx.Metadata);
                    handler(domainContext);
                }
                catch (Exception ex)
                {
                    MesErrorHandler.Error(LogSource, "OnScanProbecardResponse 处理失败", ex);
                }
            }, _tokens);

            _subscriptions.Add(subscription);
            return subscription;
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
    }
}
