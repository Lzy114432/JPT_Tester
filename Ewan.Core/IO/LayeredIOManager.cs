using System;
using System.IO;
using Ewan.Core.Attribute;
using Ewan.Model.IO;
using EwanIO.Core.Context;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Simulation;
using EwanIO.Hardware.IOC0640;
using EwanIO.Hardware.Mitsubishi;
using EwanIO.Hardware.SMC606IO;
using Newtonsoft.Json.Linq;
using PlcCommunication.Implementations.MCProtocol;

namespace Ewan.Core.IO
{
    /// <summary>
    /// IO管理器 - 统一管理 IO 上下文与硬件连接（EwanIO V2）
    /// Priority = 1 表示在基础配置（日志、权限等）之后，业务模块（StreamController）之前初始化
    /// </summary>
    [Manager(Priority = 1)]
    public class LayeredIOManager : BaseManager<LayeredIOManager>
    {
        private enum IoHardwareType
        {
            MitsubishiPLC,
            IOC0640,
            SMC606IO
        }

        private readonly object _lockObject = new object();

        private IoContext<MarkingMachineFeederIOModel> _ctx;
        private IHardwareIO _hardware;

        private IoHardwareType _hardwareType = IoHardwareType.SMC606IO;
        private string _connectionString = string.Empty;
        private string _mappingConfigPath = string.Empty;
        private int _inputPhysicalCount;
        private int _outputPhysicalCount;

        public IoContext<MarkingMachineFeederIOModel> Ctx
        {
            get
            {
                lock (_lockObject)
                {
                    return _ctx;
                }
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (_lockObject)
                {
                    return _hardware != null && _hardware.IsConnected;
                }
            }
        }

        public int InputCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _ctx?.Meta.InputCount ?? 0;
                }
            }
        }

        public int OutputCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _ctx?.Meta.OutputCount ?? 0;
                }
            }
        }

        public override bool Init()
        {
            try
            {
                LoadConfiguration();
                CreateContext();
                _uiLogger.Info("管理器初始化成功: {0}", "LayeredIOManager");
                return base.Init();
            }
            catch (Exception ex)
            {
                _uiLogger.Error("管理器初始化失败: {0} - {1}", "LayeredIOManager", ex.Message);
                return false;
            }
        }

        private void LoadConfiguration()
        {
            _mappingConfigPath = ResolveMappingConfigPath();

            if (!File.Exists(_mappingConfigPath))
            {
                _uiLogger.Warn("IO配置文件不存在: {0}", _mappingConfigPath);
                _hardwareType = IoHardwareType.SMC606IO;
                _connectionString = "192.168.5.11";
                _inputPhysicalCount = 0;
                _outputPhysicalCount = 0;
                return;
            }

            string json = File.ReadAllText(_mappingConfigPath);
            var root = JObject.Parse(json);

            string hardwareTypeText = (string)root["HardwareType"] ?? (string)root["hardwareType"] ?? string.Empty;
            _hardwareType = ParseHardwareType(hardwareTypeText);

            _connectionString = (string)root["ConnectionString"] ?? (string)root["connectionString"] ?? string.Empty;

            var inputMappings = root["InputMappings"] ?? root["inputs"];
            var outputMappings = root["OutputMappings"] ?? root["outputs"];
            _inputPhysicalCount = GetMaxIndexPlusOne(inputMappings, "PhysicalIndex", "physical");
            _outputPhysicalCount = GetMaxIndexPlusOne(outputMappings, "PhysicalIndex", "physical");

            if (_hardwareType == IoHardwareType.SMC606IO && string.IsNullOrWhiteSpace(_connectionString))
            {
                _connectionString = "192.168.5.11";
            }

            _uiLogger.Debug("IO配置已加载: {0}",
                $"HardwareType={_hardwareType}, ConnectionString={_connectionString}, MappingPath={_mappingConfigPath}, In={_inputPhysicalCount}, Out={_outputPhysicalCount}");
        }

        private static IoHardwareType ParseHardwareType(string hardwareTypeText)
        {
            if (string.IsNullOrWhiteSpace(hardwareTypeText))
            {
                return IoHardwareType.SMC606IO;
            }

            if (string.Equals(hardwareTypeText, "MitsubishiPLC", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hardwareTypeText, "Mitsubishi", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hardwareTypeText, "PLC", StringComparison.OrdinalIgnoreCase))
            {
                return IoHardwareType.MitsubishiPLC;
            }

            if (string.Equals(hardwareTypeText, "IOC0640", StringComparison.OrdinalIgnoreCase))
            {
                return IoHardwareType.IOC0640;
            }

            if (string.Equals(hardwareTypeText, "SMC606IO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hardwareTypeText, "SMC606", StringComparison.OrdinalIgnoreCase))
            {
                return IoHardwareType.SMC606IO;
            }

            return IoHardwareType.SMC606IO;
        }

        private static int GetMaxIndexPlusOne(JToken mappingsToken, string key1, string key2)
        {
            if (mappingsToken == null)
            {
                return 0;
            }

            var arr = mappingsToken as JArray;
            if (arr == null)
            {
                return 0;
            }

            int max = -1;
            foreach (var entry in arr)
            {
                int? idx = (int?)entry[key1] ?? (int?)entry[key2];
                if (idx.HasValue && idx.Value > max)
                {
                    max = idx.Value;
                }
            }

            return max + 1;
        }

        private string ResolveMappingConfigPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] candidates =
            {
                Path.Combine(baseDir, "Config", "io_mapping.json"),
                Path.Combine(baseDir, "config", "io_mapping.json"),
                Path.Combine(Environment.CurrentDirectory, "Config", "io_mapping.json"),
                Path.Combine(Environment.CurrentDirectory, "config", "io_mapping.json"),
                Path.Combine("Config", "io_mapping.json"),
                Path.Combine("config", "io_mapping.json")
            };

            foreach (string path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
                catch
                {
                    // ignored
                }
            }

            return Path.Combine(baseDir, "Config", "io_mapping.json");
        }

        private void CreateContext()
        {
            lock (_lockObject)
            {
                DisposeContext();

                _hardware = CreateHardware();

                var builder = IoContextBuilder.For<MarkingMachineFeederIOModel>()
                    .WithId("MarkingMachineFeeder")
                    .WithHardware(_hardware);

                if (File.Exists(_mappingConfigPath))
                {
                    builder = builder.WithMapping(_mappingConfigPath);
                }

                _ctx = builder.Build();

                if (!File.Exists(_mappingConfigPath))
                {
                    _ctx.Mapping.GenerateDefaultMapping();
                }
            }
        }

        private IHardwareIO CreateHardware()
        {
            switch (_hardwareType)
            {
                case IoHardwareType.MitsubishiPLC:
                    int ioCount = Math.Max(Math.Max(_inputPhysicalCount, _outputPhysicalCount), 64);
                    return new MCPlc(new MCProtocolPlc(), ioCount);
                case IoHardwareType.IOC0640:
                    return new IOC0640DriverWrapper();
                case IoHardwareType.SMC606IO:
                default:
                    return new IOSMC606DriverWrapper();
            }
        }

        private string BuildSmc606ConnectionString(string rawConnectionString)
        {
            string conn = rawConnectionString ?? string.Empty;
            if (string.IsNullOrWhiteSpace(conn))
            {
                conn = "192.168.5.11";
            }

            int inputCount = _inputPhysicalCount > 0 ? _inputPhysicalCount : InputCount;
            int outputCount = _outputPhysicalCount > 0 ? _outputPhysicalCount : OutputCount;
            if (inputCount <= 0)
            {
                inputCount = 40;
            }

            if (outputCount <= 0)
            {
                outputCount = 34;
            }

            int inputBoards = (int)Math.Ceiling(inputCount / 32.0);
            int outputBoards = (int)Math.Ceiling(outputCount / 32.0);

            if (conn.IndexOf("|", StringComparison.Ordinal) < 0)
            {
                return $"{conn}|input={inputCount}|output={outputCount}|inputboards={inputBoards}|outputboards={outputBoards}";
            }

            string lower = conn.ToLowerInvariant();
            if (lower.IndexOf("input=", StringComparison.Ordinal) < 0)
            {
                conn += $"|input={inputCount}";
                lower = conn.ToLowerInvariant();
            }

            if (lower.IndexOf("output=", StringComparison.Ordinal) < 0)
            {
                conn += $"|output={outputCount}";
                lower = conn.ToLowerInvariant();
            }

            if (lower.IndexOf("inputboards=", StringComparison.Ordinal) < 0)
            {
                conn += $"|inputboards={inputBoards}";
                lower = conn.ToLowerInvariant();
            }

            if (lower.IndexOf("outputboards=", StringComparison.Ordinal) < 0)
            {
                conn += $"|outputboards={outputBoards}";
            }

            return conn;
        }

        public bool Connect(string connectionString = null)
        {
            lock (_lockObject)
            {
                if (_hardware == null || _ctx == null)
                {
                    _uiLogger.Error("IO未初始化");
                    return false;
                }

                if (_hardware.IsConnected)
                {
                    return true;
                }

                string connStr = connectionString ?? _connectionString ?? string.Empty;
                if (_hardwareType == IoHardwareType.SMC606IO)
                {
                    connStr = BuildSmc606ConnectionString(connStr);
                }

                try
                {
                    bool result = _hardware.Connect(connStr);
                    if (!result)
                    {
                        _uiLogger.Error("IO连接失败: {0}", connStr);
                        return false;
                    }

                    _uiLogger.Info("IO已连接: {0}", connStr);

                    try
                    {
                        _ctx.Tick();
                    }
                    catch (Exception ex)
                    {
                        _uiLogger.Warn("IO初次Tick失败: {0}", ex.Message);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _uiLogger.Error("IO连接错误: {0}", ex.Message);
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_lockObject)
            {
                if (_hardware != null && _hardware.IsConnected)
                {
                    try
                    {
                        _hardware.Disconnect();
                    }
                    catch
                    {
                        // ignored
                    }

                    _uiLogger.Info("IO已断开连接");
                }
            }
        }

        public void Tick()
        {
            IoContext<MarkingMachineFeederIOModel> ctx;
            lock (_lockObject)
            {
                ctx = _ctx;
            }

            ctx?.Tick();
        }

        public bool SetInputSimulate(int index, SimMode mode)
        {
            IoContext<MarkingMachineFeederIOModel> ctx;
            lock (_lockObject)
            {
                ctx = _ctx;
            }

            if (ctx == null)
            {
                _uiLogger.Error("IO未初始化");
                return false;
            }

            try
            {
                switch (mode)
                {
                    case SimMode.ForceOn:
                        ctx.Sim.ForceOn(index);
                        break;
                    case SimMode.ForceOff:
                        ctx.Sim.ForceOff(index);
                        break;
                    default:
                        ctx.Sim.ClearSimulate(index);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error("IO模拟错误: {0} - {1}", $"X{index}", ex.Message);
                return false;
            }
        }

        public SimMode GetInputSimulate(int index)
        {
            IoContext<MarkingMachineFeederIOModel> ctx;
            lock (_lockObject)
            {
                ctx = _ctx;
            }

            if (ctx == null)
            {
                return SimMode.None;
            }

            try
            {
                return ctx.Sim.GetMode(index);
            }
            catch
            {
                return SimMode.None;
            }
        }

        public bool ClearAllSimulations()
        {
            IoContext<MarkingMachineFeederIOModel> ctx;
            lock (_lockObject)
            {
                ctx = _ctx;
            }

            if (ctx == null)
            {
                _uiLogger.Error("IO未初始化");
                return false;
            }

            try
            {
                ctx.Sim.ClearAll();
                _uiLogger.Info("清除所有IO模拟状态");
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error("IO模拟错误: {0} - {1}", "Clear", ex.Message);
                return false;
            }
        }

        public override void Destroy()
        {
            lock (_lockObject)
            {
                try
                {
                    if (_hardware != null && _hardware.IsConnected)
                    {
                        _hardware.Disconnect();
                    }
                }
                catch
                {
                    // ignored
                }

                DisposeContext();
            }

            base.Destroy();
        }

        private void DisposeContext()
        {
            if (_ctx != null)
            {
                try
                {
                    _ctx.Dispose();
                }
                catch
                {
                    // ignored
                }

                _ctx = null;
            }

            if (_hardware != null)
            {
                try
                {
                    _hardware.Dispose();
                }
                catch
                {
                    // ignored
                }

                _hardware = null;
            }
        }
    }
}
