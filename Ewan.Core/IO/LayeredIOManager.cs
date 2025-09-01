using Ewan.Core.Attribute;
using IOLibrary.Core.Factory;
using IOLibrary.Core.Interfaces;
using IOLibrary.Core.Layered;
using IOLibrary.Core.Models;
using Newtonsoft.Json;
using PlcCommunication.Implementations.MCProtocol;
using System;
using System.IO;

namespace Ewan.Core.IO
{
    /// <summary>
    /// LayeredIO管理器 - 统一管理IO硬件层访问
    /// Priority = 1 表示在基础配置（日志、权限等）之后，业务模块（StreamController）之前初始化
    /// </summary>
    [Manager(Priority = 1)]
    public class LayeredIOManager : BaseManager<LayeredIOManager>
    {
        private LayeredIO _layeredIO;
        private readonly object _lockObject = new object();
        private HardwareType _hardwareType = HardwareType.MitsubishiPLC;
        private string _connectionString;
        private string _mappingConfigPath;
        private bool _enableLogging = true;

        /// <summary>
        /// 获取LayeredIO实例
        /// </summary>
        public LayeredIO LayeredIO
        {
            get
            {
                lock (_lockObject)
                {
                    return _layeredIO;
                }
            }
        }

        /// <summary>
        /// IO是否已连接
        /// </summary>
        public bool IsConnected => _layeredIO?.IsOpen ?? false;

        /// <summary>
        /// 输入点数
        /// </summary>
        public int InputCount => _layeredIO?.InputCount ?? 0;

        /// <summary>
        /// 输出点数
        /// </summary>
        public int OutputCount => _layeredIO?.OutputCount ?? 0;

        /// <summary>
        /// 初始化管理器
        /// </summary>
        public override bool Init()
        {
            try
            {
                // 从配置加载硬件类型和连接参数
                LoadConfiguration();
                
                // 创建LayeredIO实例
                CreateLayeredIO();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ManagerInitialized, "LayeredIOManager");
                return base.Init();
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ManagerInitializationFailed, "LayeredIOManager", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfiguration()
        {
            // TODO: 从配置文件加载设置
            // 这里先使用默认值，后续可以从配置文件读取
            _hardwareType = HardwareType.MitsubishiPLC;
            _connectionString = "127.0.0.1:6000";
            _mappingConfigPath = Path.Combine("Config", "io_mapping.json");
            _enableLogging = true;
            
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.IOConfigurationLoaded, 
                $"HardwareType={_hardwareType}, ConnectionString={_connectionString}, MappingPath={_mappingConfigPath}");
        }

        /// <summary>
        /// 创建LayeredIO实例
        /// </summary>
        private void CreateLayeredIO()
        {
            lock (_lockObject)
            {
                switch (_hardwareType)
                {
                    case HardwareType.MitsubishiPLC:
                        CreateMitsubishiPLC();
                        break;
                    case HardwareType.IOC0640:
                        CreateIOC0640();
                        break;
                    default:
                        throw new NotSupportedException($"不支持的硬件类型: {_hardwareType}");
                }
            }
        }

        /// <summary>
        /// 创建三菱PLC的LayeredIO
        /// </summary>
        private void CreateMitsubishiPLC()
        {
            var mcPlc = new MCProtocolPlc();
            
            var builder = LayeredIOBuilder.Create()
                .WithMitsubishiPLC(mcPlc, 64)
                .WithName("MarkingMachine IO System")
                .WithLogging(_enableLogging);

            // 如果映射配置文件存在，加载它
            bool configFileExists = File.Exists(_mappingConfigPath);
            if (configFileExists)
            {
                builder.WithMappingConfig(_mappingConfigPath);
                _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConfigFileLoaded, _mappingConfigPath);
            }

            _layeredIO = builder.Build();
            
            // 如果配置文件不存在，尝试自动加载或创建默认映射
            if (!configFileExists)
            {
                // 尝试自动查找配置文件
                _layeredIO.LoadMappingConfiguration("");
                
                // 检查是否成功加载了映射（检查前几个点位）
                bool hasMappings = false;
                for (int i = 0; i < 5; i++)
                {
                    if (_layeredIO.GetInputMapping(i) != null || _layeredIO.GetOutputMapping(i) != null)
                    {
                        hasMappings = true;
                        break;
                    }
                }
                
                // 如果还是没有映射，创建基本的默认映射
                if (!hasMappings)
                {
                    CreateDefaultMappings();
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IODefaultMappingsCreated);
                }
                else
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IOAutoConfigLoaded);
                }
            }
            
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.IOHardwareCreated, "MitsubishiPLC");
        }

        /// <summary>
        /// 创建IOC0640的LayeredIO
        /// </summary>
        private void CreateIOC0640()
        {
            var config = new HardwareIOConfig
            {
                Type = HardwareType.IOC0640,
                ConnectionString = "boards=2"
            };
            
            var hardware = HardwareIOFactory.Create(config);
            _layeredIO = new LayeredIO(hardware)
            {
                Name = "MarkingMachine IO System",
                EnableLogging = _enableLogging
            };
            
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.IOHardwareCreated, "IOC0640");
        }

        /// <summary>
        /// 创建默认的IO映射
        /// </summary>
        private void CreateDefaultMappings()
        {
            // 根据硬件实际的输入点数创建映射
            int inputCount = _layeredIO.InputCount;
            for (int i = 0; i < inputCount; i++)
            {
                _layeredIO.AddInputMapping(i, i, $"X{i}", true);  // 从X0开始
            }
            _uiLogger.Info(() => Ewan.Resources.LogMessages.IOInputMappingsCreated, inputCount);
            
            // 根据硬件实际的输出点数创建映射
            int outputCount = _layeredIO.OutputCount;
            for (int i = 0; i < outputCount; i++)
            {
                _layeredIO.AddOutputMapping(i, i, $"Y{i}", true);  // 从Y0开始
            }
            _uiLogger.Info(() => Ewan.Resources.LogMessages.IOOutputMappingsCreated, outputCount);
            
            // 保存默认映射到配置文件
            SaveDefaultMappingConfiguration();
        }
        
        /// <summary>
        /// 保存默认映射配置到文件
        /// </summary>
        private void SaveDefaultMappingConfiguration()
        {
            try
            {
                // 确保Config目录存在
                string configDir = Path.GetDirectoryName(_mappingConfigPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.DirectoryCreated, configDir);
                }
                
                // 先保存映射配置到文件
                _layeredIO.SaveMappingConfiguration(_mappingConfigPath);
                
                // 读取刚保存的文件并添加硬件信息
                string json = File.ReadAllText(_mappingConfigPath);
                dynamic config = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                
                // 添加硬件类型和连接字符串
                config.HardwareType = _hardwareType.ToString();
                config.ConnectionString = _connectionString;
                
                // 重新保存包含硬件信息的完整配置
                string updatedJson = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(_mappingConfigPath, updatedJson);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConfigFileSaved, _mappingConfigPath);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOConfigFileSaveError, _mappingConfigPath, ex.Message);
            }
        }

        /// <summary>
        /// 连接到硬件
        /// </summary>
        /// <param name="connectionString">连接字符串（可选，不提供则使用配置的连接字符串）</param>
        /// <returns>连接是否成功</returns>
        public bool Connect(string connectionString = null)
        {
            lock (_lockObject)
            {
                if (_layeredIO == null)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IONotInitialized);
                    return false;
                }

                try
                {
                    string connStr = connectionString ?? _connectionString  ;
                    bool result = _layeredIO.Open(connStr);
                    
                    if (result)
                    {
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConnected, connStr);
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.IOConnectionFailed, connStr);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOConnectionError, ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            lock (_lockObject)
            {
                if (_layeredIO != null && _layeredIO.IsOpen)
                {
                    _layeredIO.Close();
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IODisconnected);
                }
            }
        }

        /// <summary>
        /// 数据同步
        /// </summary>
        public void DataSync()
        {
            lock (_lockObject)
            {
                _layeredIO?.DataSync();
            }
        }

        /// <summary>
        /// 设置硬件类型（需要在Init之前调用）
        /// </summary>
        public void SetHardwareType(HardwareType type, string connectionString = null)
        {
            if (_layeredIO != null)
            {
                throw new InvalidOperationException("不能在初始化后更改硬件类型");
            }
            
            _hardwareType = type;
            if (!string.IsNullOrEmpty(connectionString))
            {
                _connectionString = connectionString;
            }
        }

        /// <summary>
        /// 重新创建LayeredIO（用于切换硬件类型）
        /// </summary>
        public bool RecreateLayeredIO(HardwareType type, string connectionString)
        {
            lock (_lockObject)
            {
                try
                {
                    // 断开现有连接
                    Disconnect();
                    
                    // 更新配置
                    _hardwareType = type;
                    _connectionString = connectionString;
                    
                    // 重新创建
                    CreateLayeredIO();
                    
                    // 连接
                    return Connect();
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IORecreationFailed, ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 销毁管理器
        /// </summary>
        public override void Destroy()
        {
            Disconnect();
            _layeredIO = null;
            base.Destroy();
        }
    }
}