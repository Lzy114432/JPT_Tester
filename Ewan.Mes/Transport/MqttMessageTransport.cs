using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ewan.Mes.Mqtt;
using Ewan.Mes.Diagnostics;

namespace Ewan.Mes.Transport
{
    /// <summary>
    /// MQTT 传输层实现
    /// 将 MqttClientWrapper 适配为通用的 IMessageTransport 接口
    /// </summary>
    public class MqttMessageTransport : IMessageTransport
    {
        private const string LogSource = "MqttMessageTransport";

        private readonly MqttClientWrapper _client;
        private readonly IMqttPayloadSerializer _serializer;
        private readonly bool _disposeClient;
        private bool _wasConnected = false;

        public MqttMessageTransport(MqttClientWrapper client, IMqttPayloadSerializer serializer = null, bool disposeClient = false)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serializer = serializer ?? new NewtonsoftPayloadSerializer();
            _disposeClient = disposeClient;

            // 订阅连接关闭事件
            _client.ConnectionClosed += OnConnectionClosed;
        }

        public bool IsConnected => _client.IsConnected;

        public void Connect()
        {
            try
            {
                MesConnectionMonitor.RaiseConnecting(LogSource);
                _client.Connect();
                _wasConnected = true;
                MesConnectionMonitor.RaiseConnected(LogSource);
            }
            catch (Exception ex)
            {
                MesConnectionMonitor.RaiseFailed(LogSource, "连接失败", ex);
                MesErrorHandler.Error(LogSource, "Connect 失败", ex);
                throw;
            }
        }

        public void Disconnect()
        {
            _client.Disconnect();
            _wasConnected = false;
            MesConnectionMonitor.RaiseDisconnected(LogSource, "主动断开连接");
        }

        #region 基于字符串 topic 的发布/订阅（旧接口，默认 QoS=1）

        public ushort Publish(string topic, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            var topicTemplate = new MqttTopicTemplate(topic);
            return _client.Publish(topicTemplate, payload, tokens, retain);
        }

        public Task<ushort> PublishAsync(string topic, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            var topicTemplate = new MqttTopicTemplate(topic);
            return _client.PublishAsync(topicTemplate, payload, tokens, retain);
        }

        public IDisposable Subscribe<T>(string topic, Action<MessageContext<T>> handler, IDictionary<string, string> tokens = null)
        {
            var topicTemplate = new MqttTopicTemplate(topic);
            return SubscribeInternal<T>(topicTemplate, handler, tokens);
        }

        #endregion

        #region 基于 EndpointTemplate 的发布/订阅（新接口，保留 QoS）

        public ushort Publish(EndpointTemplate endpoint, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var topic = endpoint.Resolve(tokens);
            var topicTemplate = new MqttTopicTemplate(topic, endpoint.QosLevel);
            return _client.Publish(topicTemplate, payload, null, retain);
        }

        public Task<ushort> PublishAsync(EndpointTemplate endpoint, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var topic = endpoint.Resolve(tokens);
            var topicTemplate = new MqttTopicTemplate(topic, endpoint.QosLevel);
            return _client.PublishAsync(topicTemplate, payload, null, retain);
        }

        public IDisposable Subscribe<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler, IDictionary<string, string> tokens = null)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var topic = endpoint.Resolve(tokens);
            var topicTemplate = new MqttTopicTemplate(topic, endpoint.QosLevel);
            return SubscribeInternal<T>(topicTemplate, handler, null);
        }

        #endregion

        private IDisposable SubscribeInternal<T>(MqttTopicTemplate topicTemplate, Action<MessageContext<T>> handler, IDictionary<string, string> tokens)
        {
            return _client.Subscribe<T>(topicTemplate, mqttContext =>
            {
                var genericContext = new MessageContext<T>(
                    mqttContext.Topic,
                    mqttContext.Payload,
                    mqttContext.RawPayload,
                    new MqttMetadata
                    {
                        QosLevel = mqttContext.QosLevel,
                        Retained = mqttContext.Retained,
                        Duplicate = mqttContext.Duplicate
                    }
                );
                handler(genericContext);
            }, tokens);
        }

        private void OnConnectionClosed(object sender, EventArgs e)
        {
            if (_wasConnected)
            {
                MesConnectionMonitor.RaiseDisconnected(LogSource, "连接断开");
                MesErrorHandler.Warning(LogSource, "MQTT 连接断开");

                // 如果启用了自动重连，触发重连中事件
                if (_client.AutoReconnect)
                {
                    MesConnectionMonitor.RaiseReconnecting(LogSource, "正在尝试重连...");
                }
            }
        }

        public void Dispose()
        {
            _client.ConnectionClosed -= OnConnectionClosed;

            if (_disposeClient)
            {
                _client.Dispose();
            }
        }

        /// <summary>
        /// MQTT 特定的元数据
        /// </summary>
        public class MqttMetadata
        {
            public byte QosLevel { get; set; }
            public bool Retained { get; set; }
            public bool Duplicate { get; set; }
        }
    }
}
