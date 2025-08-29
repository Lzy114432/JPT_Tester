using IOLibrary.Core.Factory;
using IOLibrary.Core.Layered;
using IOLibrary.Core.Models;
using PlcCommunication.Implementations.MCProtocol;
using System;

namespace IOLibrary.Examples
{
    /// <summary>
    /// 工厂模式使用示例
    /// </summary>
    public class FactoryPatternExamples
    {
        /// <summary>
        /// 示例1: 使用构建器模式创建三菱PLC
        /// </summary>
        public static LayeredIO CreateMitsubishiPLCWithBuilder()
        {
            Console.WriteLine("=== 使用构建器模式创建三菱PLC ===");
            
            var mcPlc = new MCProtocolPlc();
            
            var layeredIO = LayeredIOBuilder.Create()
                .WithMitsubishiPLC(mcPlc, 64)          // 设置硬件类型
                .WithConnection("192.168.1.100:6000")  // 设置连接字符串
                .WithName("三菱PLC控制系统")           
                .WithLogging(true)                     // 启用日志
                .WithMappingConfig("plc_mapping.json") // 加载映射配置
                .BuildAndConnect();                    // 构建并连接
            
            return layeredIO;
        }
        
        /// <summary>
        /// 示例2: 使用工厂直接创建IOC0640
        /// </summary>
        public static LayeredIO CreateIOC0640WithFactory()
        {
            Console.WriteLine("=== 使用工厂直接创建IOC0640 ===");
            
            // 配置硬件
            var config = new HardwareIOConfig
            {
                Type = HardwareType.IOC0640,
                ConnectionString = "boards=2"  // 2块板卡
            };
            
            // 使用工厂创建硬件
            var hardware = HardwareIOFactory.CreateAndConnect(config);
            
            // 创建LayeredIO
            var layeredIO = new LayeredIO(hardware)
            {
                Name = "IOC0640板卡系统",
                EnableLogging = false
            };
            
            // 添加一些映射
            layeredIO.AddInputMapping(0, 5, "启动按钮", true);
            layeredIO.AddInputMapping(1, 6, "停止按钮", false);
            layeredIO.AddOutputMapping(0, 10, "运行指示灯", true);
            
            return layeredIO;
        }
        
        /// <summary>
        /// 示例3: 使用构建器链式调用
        /// </summary>
        public static LayeredIO CreateWithFluentAPI()
        {
            Console.WriteLine("=== 使用流式API创建 ===");
            
            return LayeredIOBuilder.Create()
                .WithIOC0640()                         // 选择IOC0640硬件
                .WithConnection("boards=1")            // 1块板卡
                .WithName("测试系统")
                .WithLogging()                         // 启用日志（默认true）
                .Build();                               // 只构建，不连接
        }
        
        /// <summary>
        /// 示例4: 动态选择硬件类型
        /// </summary>
        public static LayeredIO CreateDynamicHardware(string hardwareTypeStr)
        {
            Console.WriteLine($"=== 动态创建硬件: {hardwareTypeStr} ===");
            
            HardwareType hardwareType;
            HardwareIOConfig config;
            
            switch (hardwareTypeStr.ToLower())
            {
                case "plc":
                case "mitsubishi":
                    hardwareType = HardwareType.MitsubishiPLC;
                    config = new HardwareIOConfig
                    {
                        Type = hardwareType,
                        PlcInstance = new MCProtocolPlc(),
                        IOCount = 64,
                        ConnectionString = "127.0.0.1:6000"
                    };
                    break;
                    
                case "ioc0640":
                case "board":
                    hardwareType = HardwareType.IOC0640;
                    config = new HardwareIOConfig
                    {
                        Type = hardwareType,
                        ConnectionString = "boards=1"
                    };
                    break;
                    
                default:
                    throw new NotSupportedException($"不支持的硬件类型: {hardwareTypeStr}");
            }
            
            // 创建硬件
            var hardware = HardwareIOFactory.Create(config);
            
            // 创建LayeredIO
            var layeredIO = new LayeredIO(hardware)
            {
                Name = $"{hardwareType} 系统",
                EnableLogging = true
            };
            
            // 连接
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                layeredIO.Open(config.ConnectionString);
            }
            
            return layeredIO;
        }
        
        /// <summary>
        /// 示例5: 注册自定义硬件类型
        /// </summary>
        public static void RegisterCustomHardware()
        {
            Console.WriteLine("=== 注册自定义硬件类型 ===");
            
            // 注册自定义硬件创建器
            HardwareIOFactory.Register(HardwareType.Custom, config =>
            {
                // 这里返回你的自定义硬件实现
                // return new CustomHardwareIO(config.CustomParameter);
                throw new NotImplementedException("请实现自定义硬件类");
            });
            
            // 使用自定义硬件
            var customConfig = new HardwareIOConfig
            {
                Type = HardwareType.Custom,
                CustomParameter = new { /* 自定义参数 */ }
            };
            
            // var customHardware = HardwareIOFactory.Create(customConfig);
        }
        
        /// <summary>
        /// 示例6: 批量创建不同类型的硬件
        /// </summary>
        public static void CreateMultipleHardware()
        {
            Console.WriteLine("=== 批量创建不同硬件 ===");
            
            // 获取所有已注册的硬件类型
            var registeredTypes = HardwareIOFactory.GetRegisteredTypes();
            
            foreach (var type in registeredTypes)
            {
                Console.WriteLine($"已注册硬件类型: {type}");
                
                if (HardwareIOFactory.IsTypeRegistered(type))
                {
                    try
                    {
                        // 根据类型创建配置
                        HardwareIOConfig config = type switch
                        {
                            HardwareType.MitsubishiPLC => new HardwareIOConfig
                            {
                                Type = type,
                                PlcInstance = new MCProtocolPlc(),
                                IOCount = 32
                            },
                            HardwareType.IOC0640 => new HardwareIOConfig
                            {
                                Type = type
                            },
                            _ => new HardwareIOConfig { Type = type }
                        };
                        
                        var hardware = HardwareIOFactory.Create(config);
                        Console.WriteLine($"  创建成功: {hardware.HardwareType}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  创建失败: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 运行所有示例
        /// </summary>
        public static void RunAllExamples()
        {
            try
            {
                // 示例1: 构建器模式
                var plcSystem = CreateMitsubishiPLCWithBuilder();
                Console.WriteLine($"创建成功: {plcSystem.Name}");
                plcSystem.Close();
                Console.WriteLine();
                
                // 示例2: 工厂模式
                var iocSystem = CreateIOC0640WithFactory();
                Console.WriteLine($"创建成功: {iocSystem.Name}");
                iocSystem.Close();
                Console.WriteLine();
                
                // 示例3: 流式API
                var testSystem = CreateWithFluentAPI();
                Console.WriteLine($"创建成功: {testSystem.Name}");
                testSystem.Close();
                Console.WriteLine();
                
                // 示例4: 动态创建
                var dynamicSystem = CreateDynamicHardware("plc");
                Console.WriteLine($"创建成功: {dynamicSystem.Name}");
                dynamicSystem.Close();
                Console.WriteLine();
                
                // 示例5: 注册自定义
                RegisterCustomHardware();
                Console.WriteLine();
                
                // 示例6: 批量创建
                CreateMultipleHardware();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"示例运行出错: {ex.Message}");
            }
        }
    }
}