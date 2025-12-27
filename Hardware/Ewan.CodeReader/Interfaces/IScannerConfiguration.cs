using System;

namespace Ewan.CodeReader.Interfaces
{
    /// <summary>
    /// 扫码器配置接口
    /// </summary>
    public interface IScannerConfiguration
    {
        /// <summary>
        /// 扫码器类型
        /// </summary>
        ScannerType ScannerType { get; }

        /// <summary>
        /// IP 地址
        /// </summary>
        string IpAddress { get; set; }

        /// <summary>
        /// 端口（TCP 类型使用）
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        int ConnectionTimeoutMs { get; set; }

        /// <summary>
        /// 接收超时（毫秒）
        /// </summary>
        int ReceiveTimeoutMs { get; set; }

        /// <summary>
        /// 创建设备信息
        /// </summary>
        ScannerDeviceInfo CreateDeviceInfo();

        /// <summary>
        /// 克隆配置
        /// </summary>
        IScannerConfiguration Clone();
    }
}
