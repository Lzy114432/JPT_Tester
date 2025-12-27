using System;
using Ewan.CodeReader.Interfaces;
using Ewan.CodeReader.Scanners;

namespace Ewan.CodeReader.Configuration
{
    /// <summary>
    /// 得利捷扫码器配置
    /// </summary>
    public class DatalogicConfiguration : IScannerConfiguration
    {
        /// <summary>
        /// 扫码器类型
        /// </summary>
        public ScannerType ScannerType => ScannerType.Datalogic;

        /// <summary>
        /// IP 地址
        /// </summary>
        public string IpAddress { get; set; } = "192.168.3.11";

        /// <summary>
        /// 端口
        /// </summary>
        public int Port { get; set; } = 51236;

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        public int ConnectionTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// 接收超时（毫秒）
        /// </summary>
        public int ReceiveTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// 触发命令
        /// </summary>
        public string TriggerCommand { get; set; } = "T";

        /// <summary>
        /// 创建设备信息
        /// </summary>
        public ScannerDeviceInfo CreateDeviceInfo()
        {
            return DatalogicScanner.CreateDeviceInfo(IpAddress, Port);
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public IScannerConfiguration Clone()
        {
            return new DatalogicConfiguration
            {
                IpAddress = IpAddress,
                Port = Port,
                ConnectionTimeoutMs = ConnectionTimeoutMs,
                ReceiveTimeoutMs = ReceiveTimeoutMs,
                TriggerCommand = TriggerCommand
            };
        }
    }
}
