using System;
using System.Collections.Generic;
using System.Linq;
using Ewan.CodeReader.Configuration;
using Ewan.CodeReader.Interfaces;
using Ewan.CodeReader.Scanners;

namespace Ewan.CodeReader
{
    /// <summary>
    /// 扫描器类型
    /// </summary>
    public enum ScannerType
    {
        /// <summary>
        /// 海康威视
        /// </summary>
        Hikvision,

        /// <summary>
        /// 得利捷
        /// </summary>
        Datalogic,

        // Cognex,    // 康耐视（预留）
        // Keyence,   // 基恩士（预留）
    }

    /// <summary>
    /// 扫描器工厂 - 支持注册式扩展
    /// </summary>
    public static class ScannerFactory
    {
        private static readonly object s_lock = new object();

        private static readonly Dictionary<ScannerType, Func<IScanner>> s_scannerCreators
            = new Dictionary<ScannerType, Func<IScanner>>
            {
                { ScannerType.Datalogic, () => new DatalogicScanner() },
                { ScannerType.Hikvision, () => new HikvisionScanner() },
            };

        private static readonly Dictionary<ScannerType, Func<IScannerConfiguration>> s_configCreators
            = new Dictionary<ScannerType, Func<IScannerConfiguration>>
            {
                { ScannerType.Datalogic, () => new DatalogicConfiguration() },
                { ScannerType.Hikvision, () => new HikvisionConfiguration() },
            };

        /// <summary>
        /// 注册扫码器创建器
        /// </summary>
        /// <param name="type">扫码器类型</param>
        /// <param name="scannerCreator">扫码器实例创建器</param>
        /// <param name="configCreator">配置实例创建器（可选）</param>
        public static void RegisterScanner(
            ScannerType type,
            Func<IScanner> scannerCreator,
            Func<IScannerConfiguration> configCreator = null)
        {
            if (scannerCreator == null)
            {
                throw new ArgumentNullException(nameof(scannerCreator));
            }

            lock (s_lock)
            {
                s_scannerCreators[type] = scannerCreator;
                if (configCreator != null)
                {
                    s_configCreators[type] = configCreator;
                }
            }
        }

        /// <summary>
        /// 取消注册扫码器
        /// </summary>
        /// <param name="type">扫码器类型</param>
        /// <returns>是否成功取消注册</returns>
        public static bool UnregisterScanner(ScannerType type)
        {
            lock (s_lock)
            {
                bool removed = s_scannerCreators.Remove(type);
                s_configCreators.Remove(type);
                return removed;
            }
        }

        /// <summary>
        /// 创建扫描器实例
        /// </summary>
        /// <param name="type">扫码器类型</param>
        /// <returns>扫码器实例</returns>
        public static IScanner CreateScanner(ScannerType type)
        {
            lock (s_lock)
            {
                if (s_scannerCreators.TryGetValue(type, out var creator))
                {
                    return creator();
                }
            }

            throw new NotSupportedException($"不支持的扫描器类型: {type}");
        }

        /// <summary>
        /// 创建配置实例
        /// </summary>
        /// <param name="type">扫码器类型</param>
        /// <returns>配置实例</returns>
        public static IScannerConfiguration CreateConfiguration(ScannerType type)
        {
            lock (s_lock)
            {
                if (s_configCreators.TryGetValue(type, out var creator))
                {
                    return creator();
                }
            }

            throw new NotSupportedException($"不支持的扫描器类型配置: {type}");
        }

        /// <summary>
        /// 检查是否支持指定扫码器类型
        /// </summary>
        /// <param name="type">扫码器类型</param>
        /// <returns>是否支持</returns>
        public static bool IsSupported(ScannerType type)
        {
            lock (s_lock)
            {
                return s_scannerCreators.ContainsKey(type);
            }
        }

        /// <summary>
        /// 获取所有已注册的扫码器类型
        /// </summary>
        /// <returns>已注册的扫码器类型列表</returns>
        public static IReadOnlyList<ScannerType> GetRegisteredTypes()
        {
            lock (s_lock)
            {
                return s_scannerCreators.Keys.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// 解析扫码器类型字符串
        /// </summary>
        /// <param name="typeText">类型字符串</param>
        /// <param name="defaultType">默认类型</param>
        /// <returns>扫码器类型</returns>
        public static ScannerType ParseType(string typeText, ScannerType defaultType = ScannerType.Datalogic)
        {
            if (string.IsNullOrWhiteSpace(typeText))
            {
                return defaultType;
            }

            if (Enum.TryParse(typeText.Trim(), ignoreCase: true, out ScannerType type))
            {
                return type;
            }

            return defaultType;
        }
    }
}
