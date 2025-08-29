using IOLibrary.Core.Interfaces;
using IOLibrary.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IOLibrary.Core.Layered
{
    /// <summary>
    /// DI 三层结构：
    /// 应用层 → 模拟层 → 映射层 → 硬件层(MCPlc)
    /// 
    /// DO 两层结构：
    /// 应用层 → 映射层 → 硬件层(MCPlc)
    /// </summary>
    public class LayeredIO
    {
        private IHardwareIO hardwareIO;
        private readonly Dictionary<int, IOMapping> inputMappings = new();
        private readonly Dictionary<int, IOMapping> outputMappings = new();
        private readonly Dictionary<int, SimulateMode> simulatedInputs = new();


        // 边沿检测相关
        private readonly Dictionary<int, bool> previousInputStates = new();
        private readonly Dictionary<int, bool> simulatedRisingEdges = new();
        private readonly Dictionary<int, bool> simulatedFallingEdges = new();

        private bool enableLogging = false;

        private bool isOpen = false;
        
        /// <summary>
        /// 获取IO是否已打开
        /// </summary>
        public bool IsOpen => isOpen;
        
        /// <summary>
        /// IO名称
        /// </summary>
        public string Name { get; set; } = "LayeredIObak";




        /// <summary>
        /// 继承自硬件的输入输出点数
        /// </summary>
        public int InputCount => hardwareIO?.InputCount ?? 0;
        public int OutputCount => hardwareIO?.OutputCount ?? 0;

        /// <summary>
        /// 是否启用日志
        /// </summary>
        public bool EnableLogging
        {
            get => enableLogging;
            set => enableLogging = value;
        }

        public LayeredIO(IHardwareIO hardware)
        {
            hardwareIO = hardware;
        }

        public bool Open(string connectionString)
        {
            if (hardwareIO == null) return false;
            
            bool result = hardwareIO.Connect(connectionString);
            if (result)
            {
                isOpen = true;
                LogInfo($"Connected to {hardwareIO.HardwareType}: {hardwareIO.ConnectionInfo}");
            }
            return result;
        }

        public void Close()
        {
            if (hardwareIO != null)
            {
                hardwareIO.Disconnect();
                isOpen = false;
                LogInfo($"Disconnected from {hardwareIO.HardwareType}");
            }
        }

        public void DataSync()
        {
            if (hardwareIO == null) return;
            
            // 硬件层已经处理了边沿检测，直接调用即可
            hardwareIO.DataSync();
        }

        /// <summary>
        /// 从应用层读取输入位
        /// </summary>
        public bool ReadInBit(int logicalIndex, bool useMap = true)
        {
            if (hardwareIO == null) return false;
            
            if (useMap && inputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                // 使用映射时，检查逻辑索引对应物理索引的模拟状态
                if (simulatedInputs.TryGetValue(mapping.PhysicalIndex, out var simMode))
                {
                    bool simulatedState = false;
                    switch (simMode)
                    {
                        case SimulateMode.ForceOn:
                            simulatedState = true;
                            break;
                        case SimulateMode.ForceOff:
                            simulatedState = false;
                            break;
                    }
                    return simulatedState;
                }
                bool state = hardwareIO.ReadInBit(mapping.PhysicalIndex);
                // 应用常开/常闭逻辑
                if (!mapping.IsNormallyOpen)
                    state = !state;
                return state;
            }
            else
            {
                // 不使用映射时，直接用物理索引
                int physicalIndex = logicalIndex;
                
                // 检查物理索引的模拟状态
                if (simulatedInputs.TryGetValue(physicalIndex, out var simMode))
                {
                    switch (simMode)
                    {
                        case SimulateMode.ForceOn:
                            return true;
                        case SimulateMode.ForceOff:
                            return false;
                    }
                }
                
                bool state = hardwareIO.ReadInBit(physicalIndex);
                return state;
            }
        }

        /// <summary>
        /// 设置输入模拟
        /// </summary>
        public void SetInputSimulate(int logicalIndex, SimulateMode mode, bool useMap = true)
        {
            int physicalIndex;
            IOMapping mapping = null;

            if (useMap && inputMappings.TryGetValue(logicalIndex, out mapping))
            {
                physicalIndex = mapping.PhysicalIndex;
            }
            else
            {
                physicalIndex = logicalIndex;
            }
            
            // 获取之前的模拟模式
            SimulateMode previousMode = simulatedInputs.ContainsKey(physicalIndex) ? simulatedInputs[physicalIndex] : SimulateMode.None;

            // 如果模式没有改变，直接返回
            if (previousMode == mode)
            {
                return;
            }

            // 清除之前的边沿标志
            simulatedRisingEdges.Remove(logicalIndex);
            simulatedFallingEdges.Remove(logicalIndex);

            // 设置新的模拟模式
            if (mode == SimulateMode.None)
            {
                simulatedInputs.Remove(physicalIndex);
            }
            else
            {
                simulatedInputs[physicalIndex] = mode;
            }

            // 固定的边沿逻辑，不考虑硬件状态和映射类型
            if ((previousMode == SimulateMode.None || previousMode == SimulateMode.ForceOff) && mode == SimulateMode.ForceOn)
            {
                if (mapping.IsNormallyOpen)
                {
                    simulatedRisingEdges[logicalIndex] = true;
                }
                else
                {
                    simulatedFallingEdges[logicalIndex] = true;
                }

                // None/ForceOff → ForceOn：上升沿
               
                LogDebug($"SetInputSimulate[L{logicalIndex}]: {previousMode} -> ForceOn, Rising edge");
            }
            else if ((previousMode == SimulateMode.ForceOn || previousMode == SimulateMode.None) && mode == SimulateMode.ForceOff)
            {
                if (mapping.IsNormallyOpen)
                {
                    simulatedFallingEdges[logicalIndex] = true;
                }
                else
                {
                    simulatedRisingEdges[logicalIndex] = true;
                }

                LogDebug($"SetInputSimulate[L{logicalIndex}]: {previousMode} -> ForceOff, Falling edge");
            }
            
            LogDebug($"SetInputSimulate[L{logicalIndex}]: {previousMode} -> {mode}");
        }

        /// <summary>
        /// 获取硬件IO接口
        /// </summary>
        public IHardwareIO GetHardwareIO()
        {
            return hardwareIO;
        }
        
        /// <summary>
        /// 获取输入模拟状态
        /// </summary>
        public SimulateMode GetInputSimulate(int logicalIndex, bool useMap = true)
        {
            int physicalIndex;
            
            if (useMap && inputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                physicalIndex = mapping.PhysicalIndex;
            }
            else
            {
                physicalIndex = logicalIndex;
            }
            
            return simulatedInputs.ContainsKey(physicalIndex) ? simulatedInputs[physicalIndex] : SimulateMode.None;
        }

        /// <summary>
        /// 从应用层写输出位
        /// </summary>
        public bool WriteOutBit(int logicalIndex, bool value, bool useMap = true, bool now = false)
        {
            if (hardwareIO == null) return false;
            
            int physicalIndex = logicalIndex;
            
            if (useMap && outputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                physicalIndex = mapping.PhysicalIndex;
                
                // 应用常开/常闭逻辑
                if (!mapping.IsNormallyOpen)
                    value = !value;
                
                LogDebug($"WriteOutBit[{logicalIndex}->P{physicalIndex}]: {value}");
            }
            else
            {
                LogDebug($"WriteOutBit[P{physicalIndex}]: {value}");
            }
            
            return hardwareIO.WriteOutBit(physicalIndex, value);
        }

        /// <summary>
        /// 从应用层读输出位
        /// </summary>
        public bool ReadOutBit(int logicalIndex, bool useMap = true)
        {
            if (hardwareIO == null) return false;
            
            int physicalIndex = logicalIndex;
            bool state;
            
            if (useMap && outputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                physicalIndex = mapping.PhysicalIndex;
                state = hardwareIO.ReadOutBit(physicalIndex);
                
                // 应用常开/常闭逻辑
                if (!mapping.IsNormallyOpen)
                    state = !state;
                
                LogDebug($"ReadOutBit[{logicalIndex}->P{physicalIndex}]: {state}");
            }
            else
            {
                state = hardwareIO.ReadOutBit(physicalIndex);
                LogDebug($"ReadOutBit[P{physicalIndex}]: {state}");
            }
            
            return state;
        }

        /// <summary>
        /// 读取上升沿（边沿检测）
        /// </summary>
        public bool ReadRisingBit(int logicalIndex, bool useMap = true)
        {
            if (hardwareIO == null) return false;
            
            if (useMap && inputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                // 检查是否是模拟输入
                if (simulatedInputs.TryGetValue(mapping.PhysicalIndex, out var simMode) && simMode != SimulateMode.None)
                {
                    bool hasRising;
                    if (mapping.IsNormallyOpen)
                    {
                        hasRising = simulatedRisingEdges.ContainsKey(logicalIndex) ? simulatedRisingEdges[logicalIndex] : false;
                    }
                    else
                    {
                        hasRising = simulatedFallingEdges.ContainsKey(logicalIndex) ? simulatedFallingEdges[logicalIndex] : false;
                    }
                    return hasRising;
                }
                
                // 硬件输入的边沿检测
                // 对于常闭映射，物理上升沿对应逻辑下降沿，物理下降沿对应逻辑上升沿
                bool physicalEdge;
                if (!mapping.IsNormallyOpen)
                {
                    // 常闭：逻辑上升沿 = 物理下降沿
                    physicalEdge = hardwareIO.ReadFallingBit(mapping.PhysicalIndex);
                }
                else
                {
                    // 常开：逻辑上升沿 = 物理上升沿
                    physicalEdge = hardwareIO.ReadRisingBit(mapping.PhysicalIndex);
                }
                return physicalEdge;
            }
            else
            {
                // 不使用映射时，检查物理索引
                int physicalIndex = logicalIndex;
                // 检查是否是模拟输入
                if (simulatedInputs.TryGetValue(physicalIndex, out var simMode) && simMode != SimulateMode.None)
                {
                    bool hasRising = simulatedRisingEdges.ContainsKey(physicalIndex) ? simulatedRisingEdges[physicalIndex] : false;
                    return hasRising;
                }
                bool rising = hardwareIO.ReadRisingBit(physicalIndex);
                return rising;
            }
        }

        /// <summary>
        /// 清除上升沿
        /// </summary>
        public void ClearRisingBit(int logicalIndex, bool useMap = true)
        {
            if (hardwareIO == null) return;
            
            if (useMap && inputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                // 清除模拟边沿
                simulatedRisingEdges.Remove(logicalIndex);
                
                // 清除硬件边沿 - 考虑常开/常闭映射
                if (!mapping.IsNormallyOpen)
                {
                    // 常闭：逻辑上升沿对应物理下降沿
                    hardwareIO.ClearFallingBit(mapping.PhysicalIndex);
                    LogDebug($"ClearRisingBit[L{logicalIndex}->P{mapping.PhysicalIndex}] NC - clearing physical falling");
                }
                else
                {
                    // 常开：逻辑上升沿对应物理上升沿
                    hardwareIO.ClearRisingBit(mapping.PhysicalIndex);
                    LogDebug($"ClearRisingBit[L{logicalIndex}->P{mapping.PhysicalIndex}] NO - clearing physical rising");
                }
            }
            else
            {
                int physicalIndex = logicalIndex;
                simulatedRisingEdges.Remove(physicalIndex);
                hardwareIO.ClearRisingBit(physicalIndex);
                LogDebug($"ClearRisingBit[P{physicalIndex}]");
            }
        }

        /// <summary>
        /// 读取下降沿
        /// </summary>
        public bool ReadFallingBit(int logicalIndex, bool useMap = true)
        {
            if (hardwareIO == null) return false;
            
            if (useMap && inputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                // 检查是否是模拟输入
                if (simulatedInputs.TryGetValue(mapping.PhysicalIndex, out var simMode) && simMode != SimulateMode.None)
                {
                    bool hasFalling;
                    if (mapping.IsNormallyOpen)
                    {
                        hasFalling = simulatedFallingEdges.ContainsKey(logicalIndex) ? simulatedFallingEdges[logicalIndex] : false;
                    }
                    else
                    {
                        //hasFalling = simulatedFallingEdges.ContainsKey(logicalIndex) ? simulatedFallingEdges[logicalIndex] : false;
                        hasFalling = simulatedRisingEdges.ContainsKey(logicalIndex) ? simulatedRisingEdges[logicalIndex] : false;
                    }
                    return hasFalling;
                }
                
                // 硬件输入的边沿检测
                // 对于常闭映射，物理上升沿对应逻辑下降沿，物理下降沿对应逻辑上升沿
                bool physicalEdge;
                if (!mapping.IsNormallyOpen)
                {
                    // 常闭：逻辑下降沿 = 物理上升沿
                    physicalEdge = hardwareIO.ReadRisingBit(mapping.PhysicalIndex);
                    LogDebug($"ReadFallingBit[L{logicalIndex}->P{mapping.PhysicalIndex}] NC mapping, physical rising: {physicalEdge}");
                }
                else
                {
                    // 常开：逻辑下降沿 = 物理下降沿
                    physicalEdge = hardwareIO.ReadFallingBit(mapping.PhysicalIndex);
                    LogDebug($"ReadFallingBit[L{logicalIndex}->P{mapping.PhysicalIndex}] NO mapping, physical falling: {physicalEdge}");
                }
                return physicalEdge;
            }
            else
            {
                // 不使用映射时，检查物理索引
                int physicalIndex = logicalIndex;
                
                // 检查是否是模拟输入
                if (simulatedInputs.TryGetValue(physicalIndex, out var simMode) && simMode != SimulateMode.None)
                {
                    bool hasFalling = simulatedFallingEdges.ContainsKey(physicalIndex) ? simulatedFallingEdges[physicalIndex] : false;
                    LogDebug($"ReadFallingBit[P{physicalIndex}] (simulated): {hasFalling}");
                    return hasFalling;
                }
                
                bool falling = hardwareIO.ReadFallingBit(physicalIndex);
                LogDebug($"ReadFallingBit[P{physicalIndex}]: {falling}");
                return falling;
            }
        }

        /// <summary>
        /// 清除下降沿
        /// </summary>
        public void ClearFallingBit(int logicalIndex, bool useMap = true)
        {
            if (hardwareIO == null) return;
            
            if (useMap && inputMappings.TryGetValue(logicalIndex, out var mapping))
            {
                // 清除模拟边沿
                simulatedFallingEdges.Remove(logicalIndex);
                
                // 清除硬件边沿 - 考虑常开/常闭映射
                if (!mapping.IsNormallyOpen)
                {
                    // 常闭：逻辑下降沿对应物理上升沿
                    hardwareIO.ClearRisingBit(mapping.PhysicalIndex);
                    LogDebug($"ClearFallingBit[L{logicalIndex}->P{mapping.PhysicalIndex}] NC - clearing physical rising");
                }
                else
                {
                    // 常开：逻辑下降沿对应物理下降沿
                    hardwareIO.ClearFallingBit(mapping.PhysicalIndex);
                    LogDebug($"ClearFallingBit[L{logicalIndex}->P{mapping.PhysicalIndex}] NO - clearing physical falling");
                }
            }
            else
            {
                int physicalIndex = logicalIndex;
                simulatedFallingEdges.Remove(physicalIndex);
                hardwareIO.ClearFallingBit(physicalIndex);
                LogDebug($"ClearFallingBit[P{physicalIndex}]");
            }
        }

        /// <summary>
        /// 保存映射配置到文件
        /// </summary>
        public void SaveMappingConfiguration(string filePath)
        {
            var config = new LayeredIOConfiguration
            {
                InputMappings = inputMappings.Values.ToList(),
                OutputMappings = outputMappings.Values.ToList()
            };
            
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(filePath, json);
            
            LogInfo($"Saved mapping configuration to {filePath}");
        }

        /// <summary>
        /// 从文件加载映射配置
        /// 如果传入空字符串，自动查找Config/IO目录下最新的配置文件
        /// </summary>
        public void LoadMappingConfiguration(string filePath)
        {
            // 如果传入空字符串，自动查找最新的配置文件
            if (string.IsNullOrEmpty(filePath))
            {
                string configDir = Path.Combine(Directory.GetCurrentDirectory(), "Config", "IO");
                if (!Directory.Exists(configDir))
                {
                    LogWarning($"Configuration directory not found: {configDir}");
                    return;
                }
                
                // 查找所有json文件，按修改时间排序，取最新的
                var files = Directory.GetFiles(configDir, "io_config_*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
                
                if (files == null)
                {
                    LogWarning($"No configuration files found in: {configDir}");
                    return;
                }
                
                filePath = files.FullName;
                LogInfo($"Auto-selected latest config file: {files.Name}");
            }
            
            if (!File.Exists(filePath))
            {
                LogWarning($"Configuration file not found: {filePath}");
                return;
            }
            
            string json = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<LayeredIOConfiguration>(json);
            
            if (config != null)
            {
                // 清空现有映射
                inputMappings.Clear();
                outputMappings.Clear();
                
                // 加载输入映射
                if (config.InputMappings != null)
                {
                    foreach (var mapping in config.InputMappings)
                    {
                        inputMappings[mapping.LogicalIndex] = mapping;
                    }
                }
                
                // 加载输出映射
                if (config.OutputMappings != null)
                {
                    foreach (var mapping in config.OutputMappings)
                    {
                        outputMappings[mapping.LogicalIndex] = mapping;
                    }
                }
                
                LogInfo($"Loaded mapping configuration from {filePath}");
                LogInfo($"Input mappings: {inputMappings.Count}, Output mappings: {outputMappings.Count}");
            }
        }

        /// <summary>
        /// 添加输入映射
        /// </summary>
        public void AddInputMapping(int logicalIndex, int physicalIndex, string name = null, bool isNormallyOpen = true)
        {
            inputMappings[logicalIndex] = new IOMapping
            {
                LogicalIndex = logicalIndex,
                PhysicalIndex = physicalIndex,
                Name = name ?? $"DI{logicalIndex}",
                IsNormallyOpen = isNormallyOpen
            };
            
            LogDebug($"Added input mapping: L{logicalIndex}->P{physicalIndex} ({name})");
        }

        /// <summary>
        /// 添加输出映射
        /// </summary>
        public void AddOutputMapping(int logicalIndex, int physicalIndex, string name = null, bool isNormallyOpen = true)
        {
            outputMappings[logicalIndex] = new IOMapping
            {
                LogicalIndex = logicalIndex,
                PhysicalIndex = physicalIndex,
                Name = name ?? $"DO{logicalIndex}",
                IsNormallyOpen = isNormallyOpen
            };
            
            LogDebug($"Added output mapping: L{logicalIndex}->P{physicalIndex} ({name})");
        }

        /// <summary>
        /// 获取输入映射
        /// </summary>
        public IOMapping GetInputMapping(int logicalIndex)
        {
            return inputMappings.ContainsKey(logicalIndex) ? inputMappings[logicalIndex] : null;
        }

        /// <summary>
        /// 获取输出映射
        /// </summary>
        public IOMapping GetOutputMapping(int logicalIndex)
        {
            return outputMappings.ContainsKey(logicalIndex) ? outputMappings[logicalIndex] : null;
        }

        private void LogDebug(string message)
        {
            if (enableLogging)
            {
                Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss.fff} {message}");
            }
        }

        private void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {message}");
        }

        private void LogWarning(string message)
        {
            Console.WriteLine($"[WARN] {DateTime.Now:HH:mm:ss} {message}");
        }
    }
}