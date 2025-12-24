using System;

namespace EwanIO.Core.Interfaces
{
    /// <summary>
    /// 扩展的 IO 硬件接口 - 支持细粒度同步
    /// 继承自 IHardwareIO，添加分离的输入/输出同步方法和批量操作
    /// </summary>
    public interface IHardwareIOExtended : IHardwareIO
    {
        /// <summary>
        /// 获取硬件能力标志
        /// </summary>
        HardwareCapabilities Capabilities { get; }

        /// <summary>
        /// 仅同步输入（读取输入到缓存）
        /// 如果硬件不支持，抛出 NotSupportedException
        /// </summary>
        void InputSync();

        /// <summary>
        /// 仅同步输出（将缓存写入输出）
        /// 如果硬件不支持，抛出 NotSupportedException
        /// </summary>
        void OutputSync();

        /// <summary>
        /// 批量写入输出端口（32 位端口）
        /// portIndex: 端口索引（0, 1, 2, ...）
        /// portValue: 32 位端口值（bit 0-31）
        /// 如果硬件不支持，抛出 NotSupportedException
        /// </summary>
        void WriteBulkOutputPort(int portIndex, uint portValue);
    }

    /// <summary>
    /// 硬件能力标志
    /// </summary>
    [Flags]
    public enum HardwareCapabilities
    {
        /// <summary>
        /// 无特殊能力，仅支持 DataSync
        /// </summary>
        None = 0,

        /// <summary>
        /// 支持独立的输入同步
        /// </summary>
        SeparateInputSync = 1 << 0,

        /// <summary>
        /// 支持独立的输出同步
        /// </summary>
        SeparateOutputSync = 1 << 1,

        /// <summary>
        /// 支持批量读取输入
        /// </summary>
        BulkInputRead = 1 << 2,

        /// <summary>
        /// 支持批量写入输出（端口级）
        /// </summary>
        BulkOutputWrite = 1 << 3,

        /// <summary>
        /// 支持输入输出完全独立同步（推荐）
        /// </summary>
        FullySeparatedSync = SeparateInputSync | SeparateOutputSync,

        /// <summary>
        /// 支持完整的批量操作
        /// </summary>
        FullBulkOperations = BulkInputRead | BulkOutputWrite
    }

    /// <summary>
    /// 硬件能力扩展方法
    /// </summary>
    public static class HardwareCapabilitiesExtensions
    {
        /// <summary>
        /// 是否支持独立输入同步
        /// </summary>
        public static bool HasInputSync(this HardwareCapabilities capabilities)
        {
            return (capabilities & HardwareCapabilities.SeparateInputSync) != 0;
        }

        /// <summary>
        /// 是否支持独立输出同步
        /// </summary>
        public static bool HasOutputSync(this HardwareCapabilities capabilities)
        {
            return (capabilities & HardwareCapabilities.SeparateOutputSync) != 0;
        }

        /// <summary>
        /// 是否支持完全独立同步
        /// </summary>
        public static bool HasFullySeparatedSync(this HardwareCapabilities capabilities)
        {
            return (capabilities & HardwareCapabilities.FullySeparatedSync) == HardwareCapabilities.FullySeparatedSync;
        }

        /// <summary>
        /// 是否支持批量写入输出
        /// </summary>
        public static bool HasBulkOutputWrite(this HardwareCapabilities capabilities)
        {
            return (capabilities & HardwareCapabilities.BulkOutputWrite) != 0;
        }

        /// <summary>
        /// 是否支持批量读取输入
        /// </summary>
        public static bool HasBulkInputRead(this HardwareCapabilities capabilities)
        {
            return (capabilities & HardwareCapabilities.BulkInputRead) != 0;
        }
    }
}
