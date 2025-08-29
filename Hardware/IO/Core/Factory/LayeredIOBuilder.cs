using IOLibrary.Core.Interfaces;
using IOLibrary.Core.Layered;
using IOLibrary.Core.Models;
using System;

namespace IOLibrary.Core.Factory
{
    /// <summary>
    /// LayeredIO构建器
    /// 提供流式API来构建和配置LayeredIO实例
    /// </summary>
    public class LayeredIOBuilder
    {
        private HardwareIOConfig _hardwareConfig;
        private string _name;
        private bool _enableLogging;
        private string _mappingConfigFile;
        
        /// <summary>
        /// 创建新的构建器实例
        /// </summary>
        public static LayeredIOBuilder Create()
        {
            return new LayeredIOBuilder();
        }
        
        /// <summary>
        /// 设置硬件类型和配置
        /// </summary>
        public LayeredIOBuilder WithHardware(HardwareType type, Action<HardwareIOConfig> configAction = null)
        {
            _hardwareConfig = new HardwareIOConfig { Type = type };
            configAction?.Invoke(_hardwareConfig);
            return this;
        }
        
        /// <summary>
        /// 设置三菱PLC硬件
        /// </summary>
        public LayeredIOBuilder WithMitsubishiPLC(PlcCommunication.Interfaces.IPlcBase plc, int ioCount = 64)
        {
            _hardwareConfig = new HardwareIOConfig
            {
                Type = HardwareType.MitsubishiPLC,
                PlcInstance = plc,
                IOCount = ioCount
            };
            return this;
        }
        
        /// <summary>
        /// 设置IOC0640板卡硬件
        /// </summary>
        public LayeredIOBuilder WithIOC0640()
        {
            _hardwareConfig = new HardwareIOConfig
            {
                Type = HardwareType.IOC0640
            };
            return this;
        }
        
        /// <summary>
        /// 设置连接字符串
        /// </summary>
        public LayeredIOBuilder WithConnection(string connectionString)
        {
            if (_hardwareConfig == null)
            {
                throw new InvalidOperationException("Must set hardware type before connection string");
            }
            _hardwareConfig.ConnectionString = connectionString;
            return this;
        }
        
        /// <summary>
        /// 设置名称
        /// </summary>
        public LayeredIOBuilder WithName(string name)
        {
            _name = name;
            return this;
        }
        
        /// <summary>
        /// 启用日志
        /// </summary>
        public LayeredIOBuilder WithLogging(bool enable = true)
        {
            _enableLogging = enable;
            return this;
        }
        
        /// <summary>
        /// 加载映射配置文件
        /// </summary>
        public LayeredIOBuilder WithMappingConfig(string configFile)
        {
            _mappingConfigFile = configFile;
            return this;
        }
        
        /// <summary>
        /// 构建LayeredIO实例
        /// </summary>
        public LayeredIO Build()
        {
            if (_hardwareConfig == null)
            {
                throw new InvalidOperationException("Hardware configuration is required");
            }
            
            // 使用工厂创建硬件实例
            IHardwareIO hardware = HardwareIOFactory.Create(_hardwareConfig);
            
            // 创建LayeredIO实例
            var layeredIO = new LayeredIO(hardware);
            
            // 应用配置
            if (!string.IsNullOrEmpty(_name))
            {
                layeredIO.Name = _name;
            }
            
            layeredIO.EnableLogging = _enableLogging;
            
            // 加载映射配置
            if (!string.IsNullOrEmpty(_mappingConfigFile))
            {
                layeredIO.LoadMappingConfiguration(_mappingConfigFile);
            }
            
            return layeredIO;
        }
        
        /// <summary>
        /// 构建并连接LayeredIO实例
        /// </summary>
        public LayeredIO BuildAndConnect()
        {
            var layeredIO = Build();
            
            if (!string.IsNullOrEmpty(_hardwareConfig?.ConnectionString))
            {
                if (layeredIO.Open(_hardwareConfig.ConnectionString))
                {
                    Console.WriteLine($"Successfully connected LayeredIO to {_hardwareConfig.Type}");
                }
                else
                {
                    Console.WriteLine($"Failed to connect LayeredIO to {_hardwareConfig.Type}");
                }
            }
            
            return layeredIO;
        }
    }
}