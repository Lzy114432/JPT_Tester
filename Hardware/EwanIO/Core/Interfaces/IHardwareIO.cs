using System;

namespace EwanIO.Core.Interfaces
{
    /// <summary>
    /// IO硬件接口 - 纯硬件层，只负责基本的读写操作
    /// 边缘检测由上层 EdgeDetector 统一处理
    /// 继承 IDisposable 确保硬件资源正确释放
    /// </summary>
    public interface IHardwareIO : IDisposable
    {
        /// <summary>
        /// 硬件类型标识 ("PLC", "IOC0640", "SMC606", "Simulate" 等)
        /// </summary>
        string HardwareType { get; }

        /// <summary>
        /// 连接信息
        /// </summary>
        string ConnectionInfo { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 输入点数量
        /// </summary>
        int InputCount { get; }

        /// <summary>
        /// 输出点数量
        /// </summary>
        int OutputCount { get; }

        /// <summary>
        /// 连接到硬件
        /// </summary>
        bool Connect(string connectionString);

        /// <summary>
        /// 断开连接
        /// </summary>
        bool Disconnect();

        /// <summary>
        /// 数据同步（刷新输入输出缓存）
        /// </summary>
        void DataSync();

        /// <summary>
        /// 读取输入位
        /// </summary>
        bool ReadInBit(int bit);

        /// <summary>
        /// 读取输出位
        /// </summary>
        bool ReadOutBit(int bit);

        /// <summary>
        /// 写入输出位
        /// </summary>
        bool WriteOutBit(int bit, bool value);
    }
}