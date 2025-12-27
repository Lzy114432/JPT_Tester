using System;
using Ewan.CodeReader.Interfaces;
using Ewan.CodeReader.Scanners;

namespace Ewan.CodeReader.Configuration
{
    /// <summary>
    /// 海康威视扫码器配置
    /// </summary>
    public class HikvisionConfiguration : IScannerConfiguration
    {
        /// <summary>
        /// 扫码器类型
        /// </summary>
        public ScannerType ScannerType => ScannerType.Hikvision;

        /// <summary>
        /// IP 地址
        /// </summary>
        public string IpAddress { get; set; } = "192.168.3.11";

        /// <summary>
        /// 端口（GigE 不使用端口，但保留以统一接口）
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// 接收超时（毫秒）
        /// </summary>
        public int ReceiveTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 创建设备信息
        /// </summary>
        public ScannerDeviceInfo CreateDeviceInfo()
        {
            return HikvisionScanner.CreateDeviceInfo(IpAddress);
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public IScannerConfiguration Clone()
        {
            return new HikvisionConfiguration
            {
                IpAddress = IpAddress,
                Port = Port,
                ConnectionTimeoutMs = ConnectionTimeoutMs,
                ReceiveTimeoutMs = ReceiveTimeoutMs
            };
        }
    }
}
