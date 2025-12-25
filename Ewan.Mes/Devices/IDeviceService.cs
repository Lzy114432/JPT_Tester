using System;
using Ewan.Mes.Transport;

namespace Ewan.Mes.Devices
{
    /// <summary>
    /// 设备服务接口（协议无关）
    /// 所有设备服务必须实现此接口
    /// </summary>
    public interface IDeviceService : IDisposable
    {
        /// <summary>
        /// 设备类型标识
        /// </summary>
        string DeviceType { get; }

        /// <summary>
        /// 设备唯一标识 ID
        /// </summary>
        string DeviceId { get; }

        /// <summary>
        /// 设备编码
        /// </summary>
        string DeviceCode { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 发布消息到指定端点
        /// </summary>
        /// <typeparam name="T">消息载荷类型</typeparam>
        /// <param name="endpoint">端点模板</param>
        /// <param name="payload">消息载荷</param>
        /// <returns>消息 ID</returns>
        ushort PublishMessage<T>(EndpointTemplate endpoint, T payload);

        /// <summary>
        /// 注册消息处理器
        /// </summary>
        /// <typeparam name="T">消息载荷类型</typeparam>
        /// <param name="endpoint">端点模板</param>
        /// <param name="handler">消息处理回调</param>
        /// <returns>可用于取消订阅的 IDisposable 对象</returns>
        IDisposable RegisterHandler<T>(EndpointTemplate endpoint, Action<MessageContext<T>> handler);
    }
}
