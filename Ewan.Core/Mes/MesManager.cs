using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ewan.Core.Attribute;
using Ewan.Mes.Devices.ZHJW.RingLine;
using Ewan.Mes.Models.Domain.ZHJW.RingLine;
using Ewan.Mes.Mqtt;
using Ewan.Mes.Services;
using Ewan.Mes.Transport;
using Ewan.Model.System;
using Newtonsoft.Json;

namespace Ewan.Core.Mes
{
    [Manager(Priority = 1)]
    public class MesManager : BaseManager<MesManager>
    {
        private readonly object _connectionLock = new object();
        private MqttClientWrapper _client;
        private MqttMessageTransport _transport;
        private MqttConnectionOptions _options;
        private readonly object _ringLineLock = new object();
        private IRingLineService _ringLineService;
        private string _ringLineDeviceId;
        private string _ringLineDeviceCode; 

        public MqttClientWrapper Client => _client;
        public bool IsConnected => _transport != null && _transport.IsConnected;

        public MqttConnectionOptions ConnectionOptions => _options;
        public string BrokerEndpointText => GetBrokerEndpointText();
        public bool IsRingLineInitialized => _ringLineService != null;
        public string RingLineDeviceId => _ringLineDeviceId;
        public string RingLineDeviceCode => _ringLineDeviceCode;

        public void Configure(MqttConnectionOptions options, bool connect = false)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            lock (_ringLineLock)
            {
                lock (_connectionLock)
                {
                    try
                    {
                        _transport?.Disconnect();
                    }
                    catch
                    {
                    }

                    DisposeRingLineService();
                    DisposeClient();

                    _options = options;
                    EnsureOptionCallbacks(_options);

                    _client = new MqttClientWrapper(_options)
                    {
                        AutoReconnect = true
                    };

                    _transport = new MqttMessageTransport(_client, disposeClient: false);
                }
            }

            if (connect)
            {
                Connect();
            }
        }

        public bool ConfigureFromSystemParameters(bool connect = false)
        {
            try
            {
                var parameters = SystemParametersManager.Instance.Parameters;
                if (parameters == null || !parameters.MesEnabled)
                {
                    _uiLogger.WarnRaw("MES configure skipped: MesEnabled=false.");
                    return false;
                }

                var keepAliveSeconds = parameters.MesKeepAliveSeconds;
                if (keepAliveSeconds <= 0)
                {
                    keepAliveSeconds = 30;
                }

                if (keepAliveSeconds > ushort.MaxValue)
                {
                    keepAliveSeconds = ushort.MaxValue;
                }

                var options = new MqttConnectionOptions
                {
                    BrokerHost = parameters.MesBrokerHost,
                    BrokerPort = parameters.MesBrokerPort,
                    CleanSession = parameters.MesCleanSession,
                    KeepAliveSeconds = (ushort)keepAliveSeconds,
                    UserName = parameters.MesUserName,
                    Password = parameters.MesPassword
                };

                if (!string.IsNullOrWhiteSpace(parameters.MesClientId))
                {
                    options.ClientId = parameters.MesClientId.Trim();
                }

                Configure(options, connect);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("MES configure from system parameters failed: {0}", ex.Message);
                return false;
            }
        }

        public bool InitializeRingLineFromSystemParameters()
        {
            try
            {
                var parameters = SystemParametersManager.Instance.Parameters;
                if (parameters == null)
                {
                    _uiLogger.ErrorRaw("RingLine init failed: {0}", "system parameters not loaded.");
                    return false;
                }

                return InitializeRingLine(parameters.MesRingLineDeviceId, parameters.MesRingLineDeviceCode);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("RingLine init from system parameters failed: {0}", ex.Message);
                return false;
            }
        }

        public override bool Init()
        {
            _uiLogger.InfoRaw("Module initialized: {0}", "MesManager");

            try
            {
                InitializeClient();

                if (_options == null || _transport == null)
                {
                    return base.Init();
                }

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
                if (_client == null || _transport == null)
                {
                    InitializeClient();
                }

                if (_transport == null || _options == null)
                {
                    _uiLogger.WarnRaw("MES connect skipped: MqttConnectionOptions not configured.");
                    return false;
                }

                if (_transport.IsConnected)
                {
                    return true;
                }

                try
                {
                    _uiLogger.InfoRaw("MES connect starting: {0}", $"{_options.BrokerHost}:{_options.BrokerPort}");
                    _transport.Connect();
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
                if (_transport == null)
                {
                    return;
                }

                try
                {
                    _transport.Disconnect();
                    _uiLogger.InfoRaw("MES disconnected: {0}", GetBrokerEndpointText());
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

                if (_client == null || _transport == null)
                {
                    InitializeClient();
                }

                if (_transport == null)
                {
                    _uiLogger.ErrorRaw("RingLine init failed: {0}", "MES not configured. Call MesManager.Configure first.");
                    return false;
                }

                var transport = new NonDisposingMessageTransport(_transport);
                _ringLineService = ServiceFactory.CreateRingLineService(transport, deviceId, deviceCode);

                if (!IsConnected)
                {
                    Connect();
                }

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

        public Task<ushort> PublishFeedingQianLiaocangAsync(string plateCode, string billNoWip)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishFeedingQianLiaocangAsync(new FeedingQianLiaocangData
            {
                DeviceCode = deviceCode,
                BillNoWip = billNoWip,
                PlateCode = plateCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishFeedingQianLiaocangSuccessAsync(string plateCode, string feedingLiaokuangCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishFeedingQianLiaocangSuccessAsync(new FeedingQianLiaocangSuccessData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                FeedingLiaokuangCode = feedingLiaokuangCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishUnloadingQianLiaocangAsync(string plateCode, string feedingLiaokuangCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishUnloadingQianLiaocangAsync(new UnloadingQianLiaocangData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                FeedingLiaokuangCode = feedingLiaokuangCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishFeedingZhongLiaocangAsync(string plateCode, string feedingLiaokuangCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishFeedingZhongLiaocangAsync(new FeedingZhongLiaocangData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                FeedingLiaokuangCode = feedingLiaokuangCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishUnloadingZhongLiaocangAsync(string plateCode, string feedingLiaokuangCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishUnloadingZhongLiaocangAsync(new UnloadingZhongLiaocangData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                FeedingLiaokuangCode = feedingLiaokuangCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishFeedingQingxihongganjiAsync(string plateCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishFeedingQingxihongganjiAsync(new FeedingQingxihongganjiData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishFeedingHouLiaocangAsync(string plateCode, string feedingLiaokuangCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishFeedingHouLiaocangAsync(new FeedingHouLiaocangData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                FeedingLiaokuangCode = feedingLiaokuangCode,
                Timestamp = DateTime.Now
            });
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

        public Task<ushort> PublishUnloadingHouLiaocangAsync(string plateCode, string feedingLiaokuangCode)
        {
            var deviceCode = _ringLineService?.DeviceCode ?? _ringLineDeviceCode;
            return PublishUnloadingHouLiaocangAsync(new UnloadingHouLiaocangData
            {
                DeviceCode = deviceCode,
                PlateCode = plateCode,
                FeedingLiaokuangCode = feedingLiaokuangCode,
                Timestamp = DateTime.Now
            });
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

        public IDisposable OnFeedingQianLiaocangResponseText(Action<string> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!EnsureRingLineService())
            {
                return new EmptySubscription();
            }

            return _ringLineService.OnFeedingQianLiaocangResponse(context =>
            {
                try
                {
                    var payloadJson = context?.Payload == null
                        ? "(null)"
                        : JsonConvert.SerializeObject(context.Payload);
                    handler($"Topic={context?.Topic}, Payload={payloadJson}");
                }
                catch (Exception ex)
                {
                    handler("FeedingQianLiaocangResponse parse failed: " + ex.Message);
                }
            });
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
            if (_client != null && _transport != null)
            {
                return;
            }

            if (_options == null)
            {
                var mesEnabled = TryInitializeOptionsFromSystemParameters();
                if (_options == null)
                {
                    if (mesEnabled)
                    {
                        _uiLogger.WarnRaw("MES enabled but options not loaded. Configure via ParameterSettings or call MesManager.Configure first.");
                    }

                    return;
                }
            }

            EnsureOptionCallbacks(_options);

            if (_client == null)
            {
                _client = new MqttClientWrapper(_options)
                {
                    AutoReconnect = true
                };
            }

            if (_transport == null)
            {
                _transport = new MqttMessageTransport(_client, disposeClient: false);
            }
        }

        private bool TryInitializeOptionsFromSystemParameters()
        {
            var mesEnabled = false;
            try
            {
                var parameters = SystemParametersManager.Instance.Parameters;
                mesEnabled = parameters != null && parameters.MesEnabled;
                if (!mesEnabled)
                {
                    return false;
                }

                var keepAliveSeconds = parameters.MesKeepAliveSeconds;
                if (keepAliveSeconds <= 0)
                {
                    keepAliveSeconds = 30;
                }

                if (keepAliveSeconds > ushort.MaxValue)
                {
                    keepAliveSeconds = ushort.MaxValue;
                }

                var options = new MqttConnectionOptions
                {
                    BrokerHost = parameters.MesBrokerHost,
                    BrokerPort = parameters.MesBrokerPort,
                    CleanSession = parameters.MesCleanSession,
                    KeepAliveSeconds = (ushort)keepAliveSeconds,
                    UserName = parameters.MesUserName,
                    Password = parameters.MesPassword
                };

                if (!string.IsNullOrWhiteSpace(parameters.MesClientId))
                {
                    options.ClientId = parameters.MesClientId.Trim();
                }

                EnsureOptionCallbacks(options);
                _options = options;
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("MES options load failed: {0}", ex.Message);
                return mesEnabled;
            }
        }

        private void DisposeClient()
        {
            if (_transport != null)
            {
                _transport.Dispose();
                _transport = null;
            }

            if (_client == null)
            {
                return;
            }

            _client.Dispose();
            _client = null;
            _options = null;
        }

        private void EnsureOptionCallbacks(MqttConnectionOptions options)
        {
            if (options == null)
            {
                return;
            }

            if (options.OnError == null)
            {
                options.OnError = (message, ex) =>
                {
                    if (ex != null)
                    {
                        _appLogger.Error("MES MQTT error: " + message, ex);
                    }
                    else
                    {
                        _appLogger.Error("MES MQTT error: " + message);
                    }
                };
            }

            if (options.OnLog == null)
            {
                options.OnLog = message => _appLogger.Info("MES MQTT: " + message);
            }
        }

        private string GetBrokerEndpointText()
        {
            if (_options == null)
            {
                return "(unknown)";
            }

            return $"{_options.BrokerHost}:{_options.BrokerPort}";
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

            _ringLineService.Dispose();
            _ringLineService = null;
            _ringLineDeviceId = null;
            _ringLineDeviceCode = null;
        }

        private sealed class EmptySubscription : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private sealed class NonDisposingMessageTransport : IMessageTransport
        {
            private readonly IMessageTransport _inner;

            public NonDisposingMessageTransport(IMessageTransport inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public bool IsConnected => _inner.IsConnected;

            public void Connect()
            {
                _inner.Connect();
            }

            public void Disconnect()
            {
                _inner.Disconnect();
            }

            public ushort Publish(string topic, object payload, IDictionary<string, string> tokens = null, bool retain = false)
            {
                return _inner.Publish(topic, payload, tokens, retain);
            }

            public Task<ushort> PublishAsync(string topic, object payload, IDictionary<string, string> tokens = null, bool retain = false)
            {
                return _inner.PublishAsync(topic, payload, tokens, retain);
            }

            public IDisposable Subscribe<T>(string topic, Action<MessageContext<T>> handler, IDictionary<string, string> tokens = null)
            {
                return _inner.Subscribe(topic, handler, tokens);
            }

            public ushort Publish(EndpointTemplate endpoint, object payload, IDictionary<string, string> tokens = null, bool retain = false)
            {
                return _inner.Publish(endpoint, payload, tokens, retain);
            }

            public Task<ushort> PublishAsync(EndpointTemplate endpoint, object payload, IDictionary<string, string> tokens = null, bool retain = false)
            {
                return _inner.PublishAsync(endpoint, payload, tokens, retain);
            }

            public IDisposable Subscribe<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler, IDictionary<string, string> tokens = null)
            {
                return _inner.Subscribe(endpoint, handler, tokens);
            }

            public void Dispose()
            {
            }
        }
    }
}
