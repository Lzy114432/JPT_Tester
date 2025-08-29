using IOLibrary.Core.Interfaces;
using IOLibrary.Core.Models;
using IOLibrary.Hardware.IOC0640;
using IOLibrary.Hardware.Mitsubishi;
using System;
using System.Collections.Generic;

namespace IOLibrary.Core.Factory
{
    /// <summary>
    /// 硬件IO工厂类
    /// 使用工厂模式创建不同类型的硬件IO实例
    /// </summary>
    public class HardwareIOFactory
    {
        private static readonly Dictionary<HardwareType, Func<HardwareIOConfig, IHardwareIO>> _creators = new();
        
        /// <summary>
        /// 静态构造函数，注册默认的硬件类型
        /// </summary>
        static HardwareIOFactory()
        {
            RegisterDefaultTypes();
        }
        
        /// <summary>
        /// 注册默认支持的硬件类型
        /// </summary>
        private static void RegisterDefaultTypes()
        {
            // 注册三菱PLC
            Register(HardwareType.MitsubishiPLC, config =>
            {
                if (config.PlcInstance == null)
                {
                    throw new ArgumentException("MitsubishiPLC requires PlcInstance in config");
                }
                return new MCPlc(config.PlcInstance, config.IOCount);
            });
            
            // 注册IOC0640板卡
            Register(HardwareType.IOC0640, config =>
            {
                return new IOC0640DriverWrapper();
            });
            
            // 注册模拟器（如果有的话）
            // Register(HardwareType.Simulator, config => new SimulatorIO(config.IOCount));
        }
        
        /// <summary>
        /// 注册新的硬件类型创建器
        /// </summary>
        /// <param name="type">硬件类型</param>
        /// <param name="creator">创建器函数</param>
        public static void Register(HardwareType type, Func<HardwareIOConfig, IHardwareIO> creator)
        {
            _creators[type] = creator;
        }
        
        /// <summary>
        /// 创建硬件IO实例
        /// </summary>
        /// <param name="config">配置信息</param>
        /// <returns>硬件IO实例</returns>
        public static IHardwareIO Create(HardwareIOConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            
            if (!_creators.TryGetValue(config.Type, out var creator))
            {
                throw new NotSupportedException($"Hardware type {config.Type} is not registered");
            }
            
            return creator(config);
        }
        
        /// <summary>
        /// 创建硬件IO实例（简化版本）
        /// </summary>
        /// <param name="type">硬件类型</param>
        /// <returns>硬件IO实例</returns>
        public static IHardwareIO Create(HardwareType type)
        {
            return Create(new HardwareIOConfig { Type = type });
        }
        
        /// <summary>
        /// 创建并连接硬件IO实例
        /// </summary>
        /// <param name="config">配置信息</param>
        /// <returns>已连接的硬件IO实例</returns>
        public static IHardwareIO CreateAndConnect(HardwareIOConfig config)
        {
            var hardware = Create(config);
            
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                if (hardware.Connect(config.ConnectionString))
                {
                    Console.WriteLine($"Successfully connected to {config.Type}: {hardware.ConnectionInfo}");
                }
                else
                {
                    Console.WriteLine($"Failed to connect to {config.Type}");
                }
            }
            
            return hardware;
        }
        
        /// <summary>
        /// 获取所有已注册的硬件类型
        /// </summary>
        /// <returns>硬件类型列表</returns>
        public static IEnumerable<HardwareType> GetRegisteredTypes()
        {
            return _creators.Keys;
        }
        
        /// <summary>
        /// 检查硬件类型是否已注册
        /// </summary>
        /// <param name="type">硬件类型</param>
        /// <returns>是否已注册</returns>
        public static bool IsTypeRegistered(HardwareType type)
        {
            return _creators.ContainsKey(type);
        }
    }
}