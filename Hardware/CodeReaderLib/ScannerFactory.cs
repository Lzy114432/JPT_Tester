using System;
using CodeReaderLib.Interfaces;
using CodeReaderLib.Scanners;

namespace CodeReaderLib
{
    /// <summary>
    /// 扫描器类型
    /// </summary>
    public enum ScannerType
    {
        Hikvision,    // 海康威视
        Datalogic,    // 得利捷
        // Cognex,    // 康耐视（预留）
        // Keyence,   // 基恩士（预留）
    }

    /// <summary>
    /// 扫描器工厂
    /// </summary>
    public static class ScannerFactory
    {
        /// <summary>
        /// 创建扫描器实例
        /// </summary>
        public static IScanner CreateScanner(ScannerType type)
        {
            switch (type)
            {
                case ScannerType.Hikvision:
                    return new HikvisionScanner();
                case ScannerType.Datalogic:
                    return new DatalogicScanner();
                // case ScannerType.Cognex:
                //     return new CognexScanner();
                default:
                    throw new NotSupportedException($"不支持的扫描器类型: {type}");
            }
        }
    }
}
