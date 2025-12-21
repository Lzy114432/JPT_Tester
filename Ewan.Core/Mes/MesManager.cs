using System;
using System.Threading.Tasks;
using Ewan.Core.Attribute;
using Ewan.Mes.Devices.ZHJW.RingLine;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Mqtt;
using Ewan.Mes.Services;
using Ewan.Mes.Transport;

namespace Ewan.Core.Mes
{
    [Manager(Priority = 1)]
    public class MesManager : BaseManager<MesManager>
    {
        private readonly object _connectionLock = new object();
        private MqttClientWrapper _client;
        private MqttConnectionOptions _options;
        private readonly object _ringLineLock = new object();
        private IRingLineService _ringLineService;
        private string _ringLineDeviceId;
        private string _ringLineDeviceCode;
        private IDisposable _ringLineFeedingQianLiaocangResponseSubscription;

        public event Action<MessageContext<FeedingQianLiaocangResponseData>> RingLineFeedingQianLiaocangResponseReceived;

        public MqttClientWrapper Client => _client;
        public bool IsConnected => _client != null && _client.IsConnected;

        public override bool Init()
        {
            _uiLogger.InfoRaw("Module initialized: {0}", "MesManager");

            try
            {
                InitializeClient();
                Connect();
                return base.Init();
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("Initialization failed: {0}", "MesManager init failed: " + ex.Message);
                return false;
            }
        }

        public override void Destroy()
        {
            _uiLogger.InfoRaw("Module destroyed: {0}", "MesManager");

            try
            {
                DisposeRingLineService();
                Disconnect();
                DisposeClient();
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("Dispose error: {0}", "MesManager destroy error: " + ex.Message);
            }

            base.Destroy();
        }

        public bool Connect()
        {
            lock (_connectionLock)
            {
                if (_client == null)
                {
                    InitializeClient();
                }

                if (_client.IsConnected)
                {
                    return true;
                }

                try
                {
                    _uiLogger.InfoRaw("MES connect starting: {0}", $"{_options.BrokerHost}:{_options.BrokerPort}");
                    _client.Connect();
                    _uiLogger.InfoRaw("MES connect succeeded: {0}", $"{_options.BrokerHost}:{_options.BrokerPort}");
                    return true;
                }
                catch (Exception ex)
                {
                    _uiLogger.ErrorRaw("MES connect failed: {0}", ex.Message);
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_connectionLock)
            {
                if (_client == null)
                {
                    return;
                }

                try
                {
                    _client.Disconnect();
                    _uiLogger.InfoRaw("MES disconnected: {0}", $"{_options.BrokerHost}:{_options.BrokerPort}");
                }
                catch (Exception ex)
                {
                    _uiLogger.ErrorRaw("MES disconnect failed: {0}", ex.Message);
                }
            }
        }

        public bool InitializeRingLine(string deviceId, string deviceCode)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(deviceCode))
            {
                _uiLogger.ErrorRaw("RingLine init failed: {0}", "deviceId/deviceCode is empty");
                return false;
            }

            lock (_ringLineLock)
            {
                if (_ringLineService != null
                    && string.Equals(_ringLineDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(_ringLineDeviceCode, deviceCode, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                DisposeRingLineService();
                _ringLineDeviceId = deviceId;
                _ringLineDeviceCode = deviceCode;

                if (_client == null)
                {
                    InitializeClient();
                }

                var transport = new MqttMessageTransport(_client, disposeClient: false);
                _ringLineService = ServiceFactory.CreateRingLineService(transport, deviceId, deviceCode);

                if (!IsConnected)
                {
                    Connect();
                }

                EnsureRingLineSubscriptions();

                _uiLogger.InfoRaw("RingLine service initialized: {0}", $"{deviceId}/{deviceCode}");
                return true;
            }
        }

        public ushort PublishFeedingQianLiaocang(FeedingQianLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishFeedingQianLiaocang(payload);
        }

        public Task<ushort> PublishFeedingQianLiaocangAsync(FeedingQianLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishFeedingQianLiaocangAsync(payload);
        }

        public ushort PublishFeedingQianLiaocangSuccess(FeedingQianLiaocangSuccessData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishFeedingQianLiaocangSuccess(payload);
        }

        public Task<ushort> PublishFeedingQianLiaocangSuccessAsync(FeedingQianLiaocangSuccessData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishFeedingQianLiaocangSuccessAsync(payload);
        }

        public ushort PublishUnloadingQianLiaocang(UnloadingQianLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishUnloadingQianLiaocang(payload);
        }

        public Task<ushort> PublishUnloadingQianLiaocangAsync(UnloadingQianLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishUnloadingQianLiaocangAsync(payload);
        }

        public ushort PublishFeedingZhongLiaocang(FeedingZhongLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishFeedingZhongLiaocang(payload);
        }

        public Task<ushort> PublishFeedingZhongLiaocangAsync(FeedingZhongLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishFeedingZhongLiaocangAsync(payload);
        }

        public ushort PublishUnloadingZhongLiaocang(UnloadingZhongLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishUnloadingZhongLiaocang(payload);
        }

        public Task<ushort> PublishUnloadingZhongLiaocangAsync(UnloadingZhongLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishUnloadingZhongLiaocangAsync(payload);
        }

        public ushort PublishFeedingQingxihongganji(FeedingQingxihongganjiData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishFeedingQingxihongganji(payload);
        }

        public Task<ushort> PublishFeedingQingxihongganjiAsync(FeedingQingxihongganjiData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishFeedingQingxihongganjiAsync(payload);
        }

        public ushort PublishFeedingHouLiaocang(FeedingHouLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishFeedingHouLiaocang(payload);
        }

        public Task<ushort> PublishFeedingHouLiaocangAsync(FeedingHouLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishFeedingHouLiaocangAsync(payload);
        }

        public ushort PublishUnloadingHouLiaocang(UnloadingHouLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishUnloadingHouLiaocang(payload);
        }

        public Task<ushort> PublishUnloadingHouLiaocangAsync(UnloadingHouLiaocangData payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return _ringLineService.PublishUnloadingHouLiaocangAsync(payload);
        }

        public IDisposable OnFeedingQianLiaocangResponse(Action<MessageContext<FeedingQianLiaocangResponseData>> handler)
        {
            if (!EnsureRingLineService())
            {
                return new EmptySubscription();
            }

            return _ringLineService.OnFeedingQianLiaocangResponse(handler);
        }

        public ushort PublishRingLineMessage<T>(EndpointTemplate endpoint, T payload)
        {
            if (!EnsureRingLineService())
            {
                return (ushort)0;
            }

            return _ringLineService.PublishMessage(endpoint, payload);
        }

        public Task<ushort> PublishRingLineMessageAsync<T>(EndpointTemplate endpoint, T payload)
        {
            if (!EnsureRingLineService())
            {
                return Task.FromResult((ushort)0);
            }

            return Task.Run(() => _ringLineService.PublishMessage(endpoint, payload));
        }

        public IDisposable RegisterRingLineHandler<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler)
        {
            if (!EnsureRingLineService())
            {
                return new EmptySubscription();
            }

            return _ringLineService.RegisterHandler(endpoint, handler);
        }

        private void InitializeClient()
        {
            if (_client != null)
            {
                return;
            }

            _options = new MqttConnectionOptions
            {
                OnError = (message, ex) =>
                {
                    if (ex != null)
                    {
                        _appLogger.Error("MES MQTT error: " + message, ex);
                    }
                    else
                    {
                        _appLogger.Error("MES MQTT error: " + message);
                    }
                },
                OnLog = message => _appLogger.Info("MES MQTT: " + message)
            };

            _client = new MqttClientWrapper(_options)
            {
                AutoReconnect = true
            };
        }

        private void DisposeClient()
        {
            if (_client == null)
            {
                return;
            }

            _client.Dispose();
            _client = null;
        }

        private bool EnsureRingLineService()
        {
            if (_ringLineService != null)
            {
                return true;
            }

            _uiLogger.ErrorRaw("RingLine service not initialized. Call InitializeRingLine first.");
            return false;
        }

        private void DisposeRingLineService()
        {
            if (_ringLineService == null)
            {
                return;
            }

            if (_ringLineFeedingQianLiaocangResponseSubscription != null)
            {
                _ringLineFeedingQianLiaocangResponseSubscription.Dispose();
                _ringLineFeedingQianLiaocangResponseSubscription = null;
            }

            _ringLineService.Dispose();
            _ringLineService = null;
            _ringLineDeviceId = null;
            _ringLineDeviceCode = null;
        }

        private void EnsureRingLineSubscriptions()
        {
            if (_ringLineService == null)
            {
                return;
            }

            if (_ringLineFeedingQianLiaocangResponseSubscription == null)
            {
                _ringLineFeedingQianLiaocangResponseSubscription = _ringLineService.OnFeedingQianLiaocangResponse(ctx =>
                {
                    var handler = RingLineFeedingQianLiaocangResponseReceived;
                    if (handler == null)
                    {
                        return;
                    }

                    try
                    {
                        handler(ctx);
                    }
                    catch (Exception ex)
                    {
                        _uiLogger.ErrorRaw("RingLine response handler failed: {0}", ex.Message);
                    }
                });
            }
        }

        private sealed class EmptySubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
