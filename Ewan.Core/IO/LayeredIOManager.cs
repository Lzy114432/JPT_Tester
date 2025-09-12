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
            //_hardwareType = HardwareType.MitsubishiPLC;
            //_connectionString = "127.0.0.1:6000";
            _hardwareType = HardwareType.SMC606IO;
            _connectionString = "192.168.5.11";
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
                    case HardwareType.SMC606IO:
                        CreateSMC606IO();
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
        /// 创建SMC606IO的LayeredIO
        /// </summary>
        private void CreateSMC606IO()
        {
            var config = new HardwareIOConfig
            {
                Type = HardwareType.SMC606IO,
                ConnectionString = _connectionString ?? ""
            };
            
            var hardware = HardwareIOFactory.Create(config);
            _layeredIO = new LayeredIO(hardware)
            {
                Name = "MarkingMachine IO System",
                EnableLogging = _enableLogging
            };
            
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.IOHardwareCreated, "SMC606IO");
        }

        /// <summary>
        /// 检查并创建SMC606IO映射配置
        /// </summary>
        private void CheckAndCreateSMC606IOMappings()
        {
            try
            {
                // 获取硬件实际检测到的IO数量
                int actualInputCount = _layeredIO.InputCount;
                int actualOutputCount = _layeredIO.OutputCount;
                
                // 如果映射配置文件存在，先尝试加载
                bool configFileExists = File.Exists(_mappingConfigPath);
                if (configFileExists)
                {
                    _layeredIO.LoadMappingConfiguration(_mappingConfigPath);
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConfigFileLoaded, _mappingConfigPath);
                    
                    // 检查现有映射数量是否与实际IO数量匹配
                    int existingInputMappings = 0;
                    int existingOutputMappings = 0;
                    
                    // 计算现有输入映射数量
                    for (int i = 0; i < actualInputCount; i++)
                    {
                        if (_layeredIO.GetInputMapping(i) != null)
                            existingInputMappings++;
                    }
                    
                    // 计算现有输出映射数量
                    for (int i = 0; i < actualOutputCount; i++)
                    {
                        if (_layeredIO.GetOutputMapping(i) != null)
                            existingOutputMappings++;
                    }
                    
                    // 如果映射数量匹配，直接使用现有映射
                    if (existingInputMappings == actualInputCount && existingOutputMappings == actualOutputCount)
                    {
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConfigurationLoaded, 
                            $"使用现有映射: 输入={existingInputMappings}, 输出={existingOutputMappings}");
                        return;
                    }
                    
                    // 映射数量不匹配，重新创建
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConfigurationLoaded, 
                        $"映射数量不匹配，重新创建: 现有(输入={existingInputMappings}, 输出={existingOutputMappings}) vs 实际(输入={actualInputCount}, 输出={actualOutputCount})");
                    
                    // 清除现有映射，重新创建
                    CreateSMC606IODefaultMappings();
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IODefaultMappingsCreated);
                    return;
                }

                // 配置文件不存在，尝试自动查找配置文件
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
                
                // 如果还是没有映射，根据硬件实际IO数量创建默认映射
                if (!hasMappings)
                {
                    CreateSMC606IODefaultMappings();
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IODefaultMappingsCreated);
                }
                else
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IOAutoConfigLoaded);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOConfigFileSaveError, _mappingConfigPath, ex.Message);
                
                // 加载失败时，创建默认映射
                try
                {
                    CreateSMC606IODefaultMappings();
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IODefaultMappingsCreated);
                }
                catch (Exception createEx)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOConfigFileSaveError, "DefaultMappings", createEx.Message);
                }
            }
        }

        /// <summary>
        /// 为SMC606IO创建默认映射，根据硬件实际检测到的IO数量
        /// </summary>
        private void CreateSMC606IODefaultMappings()
        {
            // 获取硬件实际检测到的IO数量
            int actualInputCount = _layeredIO.InputCount;
            int actualOutputCount = _layeredIO.OutputCount;
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.IOConfigurationLoaded, 
                $"SMC606IO检测到输入点数: {actualInputCount}, 输出点数: {actualOutputCount}");

            // 根据实际输入点数创建映射
            for (int i = 0; i < actualInputCount; i++)
            {
                _layeredIO.AddInputMapping(i, i, $"X{i}", true);
            }
            _uiLogger.Info(() => Ewan.Resources.LogMessages.IOInputMappingsCreated, actualInputCount);
            
            // 根据实际输出点数创建映射
            for (int i = 0; i < actualOutputCount; i++)
            {
                _layeredIO.AddOutputMapping(i, i, $"Y{i}", true);
            }
            _uiLogger.Info(() => Ewan.Resources.LogMessages.IOOutputMappingsCreated, actualOutputCount);
            
            // 保存默认映射到配置文件
            SaveDefaultMappingConfiguration();
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
                        
                        // SMC606IO连接成功后，检查并创建映射配置
                        if (_hardwareType == HardwareType.SMC606IO)
                        {
                            CheckAndCreateSMC606IOMappings();
                        }
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
        /// 写入输出点位值
        /// </summary>
        /// <param name="index">输出点索引 (0-63)</param>
        /// <param name="value">输出值</param>
        /// <param name="useMapping">是否使用映射 (true: 使用映射索引, false: 使用物理索引)</param>
        /// <returns>写入是否成功</returns>
        public bool WriteOutput(int index, bool value, bool useMapping = true)
        {
            lock (_lockObject)
            {
                if (_layeredIO == null || !_layeredIO.IsOpen)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IONotConnected);
                    return false;
                }

                try
                {
                    // 使用LayeredIO的WriteOutBit方法
                    // useMapping参数决定是否使用映射
                    bool result = _layeredIO.WriteOutBit(index, value, useMapping);
                    
                    if (result)
                    {
                        _uiLogger.Debug(() => Ewan.Resources.LogMessages.IOWriteSuccess, $"Y{index}", value);
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.IOWriteFailed, $"Y{index}");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOWriteError, $"Y{index}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取输出点位值
        /// </summary>
        /// <param name="index">输出点索引 (0-63)</param>
        /// <param name="useMapping">是否使用映射 (true: 使用映射索引, false: 使用物理索引)</param>
        /// <returns>输出点位值</returns>
        public bool ReadOutput(int index, bool useMapping = true)
        {
            lock (_lockObject)
            {
                if (_layeredIO == null || !_layeredIO.IsOpen)
                {
                    return false;
                }

                try
                {
                    return _layeredIO.ReadOutBit(index, useMapping);
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOReadError, $"Y{index}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 设置输入点模拟状态
        /// </summary>
        /// <param name="index">输入点索引 (0-63)</param>
        /// <param name="mode">模拟模式 (SimulateMode枚举)</param>
        /// <param name="useMapping">是否使用映射 (true: 使用映射索引, false: 使用物理索引)</param>
        /// <returns>设置是否成功</returns>
        public bool SetInputSimulate(int index, SimulateMode mode, bool useMapping = true)
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
                    // 使用LayeredIO的SetInputSimulate方法
                    _layeredIO.SetInputSimulate(index, mode, useMapping);
                    
                    string modeName;
                    switch ((int)mode)
                    {
                        case 1:
                            modeName = "ForceOn";
                            break;
                        case 2:
                            modeName = "ForceOff";
                            break;
                        default:
                            modeName = "None";
                            break;
                    }
                    
                    _uiLogger.Debug(() => Ewan.Resources.LogMessages.IOSimulateSet, $"X{index}", modeName);
                    return true;
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOSimulateError, $"X{index}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取输入点模拟状态
        /// </summary>
        /// <param name="index">输入点索引 (0-63)</param>
        /// <param name="useMapping">是否使用映射 (true: 使用映射索引, false: 使用物理索引)</param>
        /// <returns>模拟模式</returns>
        public SimulateMode GetInputSimulate(int index, bool useMapping = true)
        {
            lock (_lockObject)
            {
                if (_layeredIO == null)
                {
                    return SimulateMode.None;
                }

                try
                {
                    return _layeredIO.GetInputSimulate(index, useMapping);
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOSimulateError, $"X{index}", ex.Message);
                    return SimulateMode.None;
                }
            }
        }

        /// <summary>
        /// 清除所有输入点模拟状态
        /// </summary>
        /// <param name="useMapping">是否使用映射</param>
        /// <returns>清除是否成功</returns>
        public bool ClearAllSimulations(bool useMapping = true)
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
                    // 清除所有输入点的模拟状态
                    for (int i = 0; i < InputCount; i++)
                    {
                        _layeredIO.SetInputSimulate(i, SimulateMode.None, useMapping);
                    }
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.IOSimulateCleared);
                    return true;
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOSimulateError, "Clear", ex.Message);
                    return false;
                }
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
        /// 强制重新创建IO映射配置（根据实际检测到的IO数量）
        /// </summary>
        /// <returns>重新创建是否成功</returns>
        public bool ForceRecreateIOMapping()
        {
            lock (_lockObject)
            {
                try
                {
                    if (_layeredIO == null || !_layeredIO.IsOpen)
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.IONotConnected);
                        return false;
                    }

                    if (_hardwareType == HardwareType.SMC606IO)
                    {
                        CreateSMC606IODefaultMappings();
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.IODefaultMappingsCreated);
                        return true;
                    }
                    else
                    {
                        CreateDefaultMappings();
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.IODefaultMappingsCreated);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.IOConfigFileSaveError, "ForceRecreateIOMapping", ex.Message);
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