/*****************************************************
** 命名空间: Ewan.Mes.Mqtt
** 文 件 名：MqttClientWrapper
** 内容简述：
** 版    本：V1.0
** 创 建 人：Ewan
** 创建日期：2025/12/3 17:59:41
** 修改记录：
日期        版本      修改人    修改内容   
*****************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace Ewan.Mes.Mqtt
{
    /// <summary>
    /// MQTT 客户端包装器，封装连接、发布、订阅等功能
    /// </summary>
    public class MqttClientWrapper : IDisposable
    {
        private readonly IMqttPayloadSerializer _serializer;
        private readonly MqttConnectionOptions _options;
        private readonly ConcurrentDictionary<string, SubscriptionInfo> _subscriptions = new ConcurrentDictionary<string, SubscriptionInfo>();
        private MqttClient _client;
        private readonly object _connectLock = new object();
        private Timer _reconnectTimer;
        private bool _isDisposed;
        private bool _manualDisconnect;

        /// <summary>
        /// 是否启用自动重连（默认启用）
        /// </summary>
        public bool AutoReconnect { get; set; } = true;

        /// <summary>
        /// 自动重连间隔（毫秒，默认5000）
        /// </summary>
        public int ReconnectIntervalMs { get; set; } = 5000;

        /// <summary>
        /// 创建 MQTT 客户端包装器
        /// </summary>
        /// <param name="options">连接配置</param>
        /// <param name="serializer">消息序列化器（默认使用 NewtonsoftPayloadSerializer）</param>
        public MqttClientWrapper(MqttConnectionOptions options, IMqttPayloadSerializer serializer = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;
            _serializer = serializer ?? new NewtonsoftPayloadSerializer();

            _reconnectTimer = new Timer(ReconnectIntervalMs);
            _reconnectTimer.Elapsed += OnReconnectTimerElapsed;
            _reconnectTimer.AutoReset = false;
        }

        public bool IsConnected
        {
            get { return _client != null && _client.IsConnected; }
        }

        public void Connect()
        {
            if (IsConnected)
            {
                return;
            }

            lock (_connectLock)
            {
                if (IsConnected)
                {
                    return;
                }

                _manualDisconnect = false;

                // 在创建新客户端前，确保旧客户端彻底关闭，防止 Socket 句柄泄漏
                if (_client != null)
                {
                    _client.MqttMsgPublishReceived -= HandleMessage;
                    _client.ConnectionClosed -= OnConnectionClosed;
                    try
                    {
                        if (_client.IsConnected) _client.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        ReportLog("Disconnect old client failed: " + ex.Message);
                    }
                    _client = null;
                }

                _options.Validate();
                _client = new MqttClient(_options.BrokerHost, _options.BrokerPort, false, null, null, MqttSslProtocols.None);
                _client.MqttMsgPublishReceived += HandleMessage;
                _client.ConnectionClosed += OnConnectionClosed;

                try
                {
                    if (string.IsNullOrWhiteSpace(_options.UserName))
                    {
                        _client.Connect(_options.ClientId, null, null, _options.CleanSession, _options.KeepAliveSeconds);
                    }
                    else
                    {
                        _client.Connect(_options.ClientId, _options.UserName, _options.Password, _options.CleanSession, _options.KeepAliveSeconds);
                    }
                    ReportLog("MQTT Connected successfully to " + _options.BrokerHost + ":" + _options.BrokerPort);
                }
                catch (Exception ex)
                {
                    ReportError("Failed to connect to MQTT Broker: " + _options.BrokerHost + ":" + _options.BrokerPort, ex);
                    throw;
                }
            }
        }

        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            _manualDisconnect = true;
            _reconnectTimer.Stop();
            _client.Disconnect();
        }

        public ushort Publish<T>(MqttTopicTemplate topicTemplate, T payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            if (topicTemplate == null)
            {
                throw new ArgumentNullException("topicTemplate");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("payload");
            }

            EnsureConnected();
            var topic = topicTemplate.Resolve(tokens);
            var body = _serializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(body);
            return _client.Publish(topic, bytes, topicTemplate.QosLevel, retain);
        }

        /// <summary>
        /// 异步发布消息，防止 UI 线程阻塞
        /// </summary>
        public Task<ushort> PublishAsync<T>(MqttTopicTemplate topicTemplate, T payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            return Task.Run(() => Publish(topicTemplate, payload, tokens, retain));
        }

        public IDisposable Subscribe<T>(MqttTopicTemplate topicTemplate, Action<MqttMessageContext<T>> handler, IDictionary<string, string> tokens = null)
        {
            if (topicTemplate == null)
            {
                throw new ArgumentNullException("topicTemplate");
            }

            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            EnsureConnected();
            var topic = topicTemplate.Resolve(tokens);
            var subscription = new Subscription<T>(topic, handler, this);
            var qosLevel = topicTemplate.QosLevel;

            // 使用 CAS 循环确保并发安全
            while (true)
            {
                SubscriptionInfo currentInfo;
                if (_subscriptions.TryGetValue(topic, out currentInfo))
                {
                    var newInfo = currentInfo.AddHandler(subscription);
                    if (_subscriptions.TryUpdate(topic, newInfo, currentInfo))
                    {
                        break;
                    }
                }
                else
                {
                    var newInfo = new SubscriptionInfo(qosLevel, ImmutableArray.Create<ISubscription>(subscription));
                    if (_subscriptions.TryAdd(topic, newInfo))
                    {
                        _client.Subscribe(new[] { topic }, new[] { qosLevel });
                        break;
                    }
                }
            }

            return subscription;
        }

        public event EventHandler ConnectionClosed;

        private void HandleMessage(object sender, MqttMsgPublishEventArgs args)
        {
            SubscriptionInfo info;
            if (!_subscriptions.TryGetValue(args.Topic, out info))
            {
                return;
            }

            var payloadText = Encoding.UTF8.GetString(args.Message);

            // ImmutableArray 是线程安全的，无需 ToList()
            foreach (var subscription in info.Handlers)
            {
                try
                {
                    subscription.Forward(payloadText, args);
                }
                catch (Exception ex)
                {
                    ReportError("Error executing message handler for topic: " + args.Topic, ex);
                }
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                Connect();
            }
        }

        private void ReportError(string message, Exception ex = null)
        {
            var handler = _options.OnError;
            if (handler != null)
            {
                try { handler(message, ex); }
                catch (Exception handlerEx)
                {
                    // 错误回调本身抛异常时记录日志，避免丢失信息
                    System.Diagnostics.Debug.WriteLine("[MqttClientWrapper] OnError handler threw exception: " + handlerEx.Message);
                }
            }
        }

        private void ReportLog(string message)
        {
            var handler = _options.OnLog;
            if (handler != null)
            {
                try { handler(message); }
                catch (Exception handlerEx)
                {
                    // 日志回调本身抛异常时记录，避免丢失信息
                    System.Diagnostics.Debug.WriteLine("[MqttClientWrapper] OnLog handler threw exception: " + handlerEx.Message);
                }
            }
        }

        private void RemoveSubscription(ISubscription subscription)
        {
            // 使用 CAS 循环确保并发安全
            while (true)
            {
                SubscriptionInfo currentInfo;
                if (!_subscriptions.TryGetValue(subscription.Topic, out currentInfo))
                {
                    return;
                }

                var newHandlers = currentInfo.Handlers.Remove(subscription);

                if (newHandlers.Length == 0)
                {
                    // 最后一个处理器被移除，尝试删除整个条目
                    // 使用 TryUpdate 验证没有并发添加
                    var emptyInfo = currentInfo.RemoveHandler(subscription);
                    if (_subscriptions.TryUpdate(subscription.Topic, emptyInfo, currentInfo))
                    {
                        SubscriptionInfo removed;
                        // 再次检查确保为空后才移除
                        if (_subscriptions.TryGetValue(subscription.Topic, out removed) && removed.Handlers.Length == 0)
                        {
                            _subscriptions.TryRemove(subscription.Topic, out removed);
                            if (IsConnected)
                            {
                                _client.Unsubscribe(new[] { subscription.Topic });
                            }
                        }
                        return;
                    }
                }
                else
                {
                    var newInfo = new SubscriptionInfo(currentInfo.QosLevel, newHandlers);
                    if (_subscriptions.TryUpdate(subscription.Topic, newInfo, currentInfo))
                    {
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _manualDisconnect = true;

            if (_reconnectTimer != null)
            {
                _reconnectTimer.Stop();
                _reconnectTimer.Elapsed -= OnReconnectTimerElapsed;
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
            }

            if (_client != null)
            {
                _client.MqttMsgPublishReceived -= HandleMessage;
                _client.ConnectionClosed -= OnConnectionClosed;
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                }

                _client = null;
            }
            _subscriptions.Clear();
        }

        private void OnConnectionClosed(object sender, EventArgs e)
        {
            var handler = ConnectionClosed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }

            if (AutoReconnect && !_isDisposed && !_manualDisconnect)
            {
                _reconnectTimer.Interval = ReconnectIntervalMs;
                _reconnectTimer.Start();
            }
        }

        private void OnReconnectTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                Connect();
                ResubscribeAll();
            }
            catch
            {
                if (AutoReconnect && !_isDisposed)
                {
                    _reconnectTimer.Start();
                }
            }
        }

        private void ResubscribeAll()
        {
            if (!IsConnected || _client == null)
            {
                return;
            }

            // 使用 ToArray() 创建快照，防止迭代过程中 _subscriptions 被修改导致的竞态条件
            var currentSubscriptions = _subscriptions.ToArray();

            foreach (var kvp in currentSubscriptions)
            {
                var topic = kvp.Key;
                var info = kvp.Value;
                if (info.Handlers.Length > 0)
                {
                    try
                    {
                        // 使用保存的 QoS 级别进行重订阅
                        _client.Subscribe(new[] { topic }, new[] { info.QosLevel });
                    }
                    catch (Exception ex)
                    {
                        ReportError("Failed to resubscribe to topic: " + topic, ex);
                    }
                }
            }

            ReportLog("Auto-reconnected and resubscribed to " + currentSubscriptions.Length + " topics.");
        }

        /// <summary>
        /// 订阅信息，包含 QoS 级别和订阅处理器列表（使用不可变数组确保线程安全）
        /// </summary>
        private class SubscriptionInfo
        {
            public byte QosLevel { get; private set; }
            public ImmutableArray<ISubscription> Handlers { get; private set; }

            public SubscriptionInfo(byte qosLevel, ImmutableArray<ISubscription> handlers)
            {
                QosLevel = qosLevel;
                Handlers = handlers;
            }

            public SubscriptionInfo AddHandler(ISubscription handler)
            {
                return new SubscriptionInfo(QosLevel, Handlers.Add(handler));
            }

            public SubscriptionInfo RemoveHandler(ISubscription handler)
            {
                return new SubscriptionInfo(QosLevel, Handlers.Remove(handler));
            }
        }

        private interface ISubscription
        {
            string Topic { get; }
            void Forward(string payloadText, MqttMsgPublishEventArgs args);
        }

        private class Subscription<T> : ISubscription, IDisposable
        {
            private readonly string _topic;
            private readonly Action<MqttMessageContext<T>> _handler;
            private readonly MqttClientWrapper _wrapper;

            public Subscription(string topic, Action<MqttMessageContext<T>> handler, MqttClientWrapper wrapper)
            {
                _topic = topic;
                _handler = handler;
                _wrapper = wrapper;
            }

            public string Topic
            {
                get { return _topic; }
            }

            public void Forward(string payloadText, MqttMsgPublishEventArgs args)
            {
                T payload;
                try
                {
                    payload = _wrapper._serializer.Deserialize<T>(payloadText);
                }
                catch (Exception ex)
                {
                    _wrapper.ReportError("Failed to deserialize message for topic: " + _topic + ", Type: " + typeof(T).Name, ex);
                    return;
                }

                var context = new MqttMessageContext<T>(_topic, payload, args.Message, args.QosLevel, args.Retain, args.DupFlag);
                _handler(context);
            }

            public void Dispose()
            {
                _wrapper.RemoveSubscription(this);
            }
        }
    }
}
