using System;
using System.Threading.Tasks;

namespace Ewan.Mes.Transport
{
    /// <summary>
    /// 消息传输层抽象接口
    /// 定义了通用的消息发送/接收行为，与具体协议无关
    /// </summary>
    public interface IMessageTransport : IDisposable
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        void Connect();

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        #region 基于字符串 topic 的发布/订阅（旧接口，保持兼容）

        /// <summary>
        /// 发布消息到指定主题/端点
        /// </summary>
        /// <param name="topic">主题/端点地址</param>
        /// <param name="payload">消息负载</param>
        /// <param name="tokens">主题占位符替换字典</param>
        /// <param name="retain">是否保留消息（仅 MQTT）</param>
        /// <returns>消息ID或状态码</returns>
        ushort Publish(string topic, object payload, System.Collections.Generic.IDictionary<string, string> tokens = null, bool retain = false);

        /// <summary>
        /// 异步发布消息
        /// </summary>
        Task<ushort> PublishAsync(string topic, object payload, System.Collections.Generic.IDictionary<string, string> tokens = null, bool retain = false);

        /// <summary>
        /// 订阅消息
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="topic">主题/端点</param>
        /// <param name="handler">消息处理器</param>
        /// <param name="tokens">主题占位符替换字典</param>
        /// <returns>订阅取消令牌</returns>
        IDisposable Subscribe<T>(string topic, Action<MessageContext<T>> handler, System.Collections.Generic.IDictionary<string, string> tokens = null);

        #endregion

        #region 基于 EndpointTemplate 的发布/订阅（新接口，保留 QoS）

        /// <summary>
        /// 发布消息到指定端点（保留 QoS 配置）
        /// </summary>
        /// <param name="endpoint">端点模板（包含 QoS 配置）</param>
        /// <param name="payload">消息负载</param>
        /// <param name="tokens">端点占位符替换字典</param>
        /// <param name="retain">是否保留消息（仅 MQTT）</param>
        /// <returns>消息ID或状态码</returns>
        ushort Publish(EndpointTemplate endpoint, object payload, System.Collections.Generic.IDictionary<string, string> tokens = null, bool retain = false);

        /// <summary>
        /// 异步发布消息到指定端点（保留 QoS 配置）
        /// </summary>
        Task<ushort> PublishAsync(EndpointTemplate endpoint, object payload, System.Collections.Generic.IDictionary<string, string> tokens = null, bool retain = false);

        /// <summary>
        /// 订阅端点消息（保留 QoS 配置）
        /// </summary>
        /// <typeparam name="T">消息类型</typeparam>
        /// <param name="endpoint">端点模板（包含 QoS 配置）</param>
        /// <param name="handler">消息处理器</param>
        /// <param name="tokens">端点占位符替换字典</param>
        /// <returns>订阅取消令牌</returns>
        IDisposable Subscribe<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler, System.Collections.Generic.IDictionary<string, string> tokens = null);

        #endregion
    }

    /// <summary>
    /// 通用消息上下文
    /// </summary>
    public class MessageContext<T>
    {
        public string Topic { get; set; }
        public T Payload { get; set; }
        public byte[] RawPayload { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public object Metadata { get; set; }  // 存储协议特定的元数据

        public MessageContext(string topic, T payload, byte[] rawPayload, object metadata = null)
        {
            Topic = topic;
            Payload = payload;
            RawPayload = rawPayload;
            ReceivedAt = DateTimeOffset.UtcNow;
            Metadata = metadata;
        }
    }
}
