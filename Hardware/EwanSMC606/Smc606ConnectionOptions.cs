using System;

namespace EwanSMC606
{
    /// <summary>
    /// SMC606 连接参数。
    /// </summary>
    public sealed class Smc606ConnectionOptions
    {
        /// <summary>
        /// 板卡号（ConnectNo）。
        /// </summary>
        public ushort CardNo { get; set; } = 0;

        /// <summary>
        /// 连接类型：0=PCI, 1=PCI-E, 2=Ethernet。
        /// </summary>
        public ushort ConnectType { get; set; } = 2;

        /// <summary>
        /// 连接字符串（如 IP 地址）。
        /// </summary>
        public string ConnectString { get; set; } = string.Empty;

        /// <summary>
        /// 通讯参数（SDK 的 baud 参数，具体含义由厂商 SDK 决定）。
        /// </summary>
        public uint BaudRate { get; set; } = 115200;

        public Smc606ConnectionOptions Clone()
        {
            return new Smc606ConnectionOptions
            {
                CardNo = CardNo,
                ConnectType = ConnectType,
                ConnectString = ConnectString,
                BaudRate = BaudRate
            };
        }

        internal void Validate()
        {
            if (ConnectType == 2 && string.IsNullOrWhiteSpace(ConnectString))
            {
                throw new ArgumentException("ConnectString is required for Ethernet connections.", nameof(ConnectString));
            }
        }
    }
}

