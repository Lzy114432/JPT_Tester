using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ewan.Mes.Transport
{
    /// <summary>
    /// HTTP 传输层实现（示例）
    /// 使用 RESTful API 替代 MQTT
    /// </summary>
    public class HttpMessageTransport : IMessageTransport
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _isConnected;

        public HttpMessageTransport(string baseUrl, HttpClient httpClient = null)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }

        public bool IsConnected => _isConnected;

        public void Connect()
        {
            // HTTP 无需显式连接，这里可以做健康检查
            try
            {
                var response = _httpClient.GetAsync("/health").ConfigureAwait(false).GetAwaiter().GetResult();
                _isConnected = response.IsSuccessStatusCode;
            }
            catch
            {
                _isConnected = false;
                throw new InvalidOperationException("Failed to connect to HTTP server");
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        #region 基于字符串 topic 的发布/订阅（旧接口）

        public ushort Publish(string topic, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            return (ushort)PublishAsync(topic, payload, tokens, retain).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<ushort> PublishAsync(string topic, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            var resolvedTopic = ResolveTopic(topic, tokens);
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(resolvedTopic, content);
            
            if (response.IsSuccessStatusCode)
            {
                return 1; // 成功返回1
            }
            else
            {
                throw new HttpRequestException($"HTTP POST failed: {response.StatusCode}");
            }
        }

        public IDisposable Subscribe<T>(string topic, Action<MessageContext<T>> handler, IDictionary<string, string> tokens = null)
        {
            // HTTP 不支持原生订阅，可以通过轮询或 WebHook 实现
            // 这里返回一个空的订阅
            return new HttpSubscription();
        }

        #endregion

        #region 基于 EndpointTemplate 的发布/订阅（新接口）

        public ushort Publish(EndpointTemplate endpoint, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var resolvedTopic = endpoint.Resolve(tokens);
            return Publish(resolvedTopic, payload, null, retain);
        }

        public Task<ushort> PublishAsync(EndpointTemplate endpoint, object payload, IDictionary<string, string> tokens = null, bool retain = false)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var resolvedTopic = endpoint.Resolve(tokens);
            return PublishAsync(resolvedTopic, payload, null, retain);
        }

        public IDisposable Subscribe<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler, IDictionary<string, string> tokens = null)
        {
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));

            var resolvedTopic = endpoint.Resolve(tokens);
            return Subscribe<T>(resolvedTopic, handler, null);
        }

        #endregion

        private string ResolveTopic(string topic, IDictionary<string, string> tokens)
        {
            if (tokens == null) return topic;

            var resolved = topic;
            foreach (var kvp in tokens)
            {
                resolved = resolved.Replace("{" + kvp.Key + "}", kvp.Value);
            }
            return resolved;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private class HttpSubscription : IDisposable
        {
            public void Dispose() { }
        }
    }
}
