using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using EwanIO.Core.Metadata;

namespace EwanIO.Core.Mapping
{
    /// <summary>
    /// 映射配置项
    /// </summary>
    public class MappingEntry
    {
        /// <summary>
        /// 逻辑索引
        /// </summary>
        [JsonProperty("logical")]
        public int LogicalIndex { get; set; }

        /// <summary>
        /// 物理索引
        /// </summary>
        [JsonProperty("physical")]
        public int PhysicalIndex { get; set; }

        /// <summary>
        /// 是否常闭（true=NC, false=NO）
        /// </summary>
        [JsonProperty("nc")]
        public bool IsNormallyClosed { get; set; }

        /// <summary>
        /// 备注（可选）
        /// </summary>
        [JsonProperty("comment", NullValueHandling = NullValueHandling.Ignore)]
        public string? Comment { get; set; }
    }

    /// <summary>
    /// 映射配置文件格式
    /// </summary>
    public class MappingConfigFile
    {
        /// <summary>
        /// 配置版本
        /// </summary>
        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 配置描述
        /// </summary>
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string? Description { get; set; }

        /// <summary>
        /// 输入映射
        /// </summary>
        [JsonProperty("inputs")]
        public List<MappingEntry> Inputs { get; set; } = new List<MappingEntry>();

        /// <summary>
        /// 输出映射
        /// </summary>
        [JsonProperty("outputs")]
        public List<MappingEntry> Outputs { get; set; } = new List<MappingEntry>();
    }

    /// <summary>
    /// 映射配置管理器
    /// </summary>
    public static class MappingConfigManager
    {
        /// <summary>
        /// 从文件加载映射配置
        /// </summary>
        public static MappingConfigFile Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            string resolvedPath = ResolvePath(filePath);

            if (!File.Exists(resolvedPath))
                throw new FileNotFoundException($"Mapping config file not found: {resolvedPath}");

            string json = File.ReadAllText(resolvedPath);

            // Backward compatible: support legacy io_mapping.json schema
            // (HardwareType/ConnectionString + InputMappings/OutputMappings).
            var legacyConfig = TryLoadLegacy(json);
            if (legacyConfig != null)
            {
                return legacyConfig;
            }

            var config = JsonConvert.DeserializeObject<MappingConfigFile>(json);

            if (config == null)
                throw new InvalidOperationException($"Failed to deserialize mapping config: {resolvedPath}");

            return config;
        }

        /// <summary>
        /// 保存映射配置到文件
        /// </summary>
        public static void Save(string filePath, MappingConfigFile config)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            string resolvedPath = ResolvePath(filePath);

            // 确保目录存在
            string? directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(resolvedPath, json);
        }

        /// <summary>
        /// 生成默认映射配置（1:1 映射，全部常开）
        /// </summary>
        public static MappingConfigFile GenerateDefault(int inputCount, int outputCount, string? description = null)
        {
            return GenerateDefault(inputCount, outputCount, description, null);
        }

        /// <summary>
        /// 生成默认映射配置（支持从元数据生成注释）
        /// </summary>
        public static MappingConfigFile GenerateDefault(
            int inputCount,
            int outputCount,
            string? description,
            IMetaNameProvider? meta)
        {
            var config = new MappingConfigFile
            {
                Description = description ?? "Auto-generated default mapping (1:1, NO)"
            };

            // 生成输入映射
            for (int i = 0; i < inputCount; i++)
            {
                config.Inputs.Add(new MappingEntry
                {
                    LogicalIndex = i,
                    PhysicalIndex = i,
                    IsNormallyClosed = false,
                    Comment = meta?.GetInputName(i) ?? $"Input {i}"
                });
            }

            // 生成输出映射
            for (int i = 0; i < outputCount; i++)
            {
                config.Outputs.Add(new MappingEntry
                {
                    LogicalIndex = i,
                    PhysicalIndex = i,
                    IsNormallyClosed = false,
                    Comment = meta?.GetOutputName(i) ?? $"Output {i}"
                });
            }

            return config;
        }

        /// <summary>
        /// 应用配置到 MappingCache
        /// </summary>
        public static void ApplyToCache(MappingConfigFile config, MappingCache cache)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            // 应用输入映射
            foreach (var entry in config.Inputs)
            {
                if (entry.LogicalIndex >= 0 && entry.LogicalIndex < cache.InputCount)
                {
                    cache.SetInputMapping(entry.LogicalIndex, entry.PhysicalIndex, entry.IsNormallyClosed);
                }
            }

            // 应用输出映射
            foreach (var entry in config.Outputs)
            {
                if (entry.LogicalIndex >= 0 && entry.LogicalIndex < cache.OutputCount)
                {
                    cache.SetOutputMapping(entry.LogicalIndex, entry.PhysicalIndex, entry.IsNormallyClosed);
                }
            }
        }

        /// <summary>
        /// 从 MappingCache 导出配置
        /// </summary>
        public static MappingConfigFile ExportFromCache(MappingCache cache)
        {
            return ExportFromCache(cache, null);
        }

        /// <summary>
        /// 从 MappingCache 导出配置（支持从元数据生成注释）
        /// </summary>
        public static MappingConfigFile ExportFromCache(MappingCache cache, IMetaNameProvider? meta)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            var config = new MappingConfigFile
            {
                Description = "Exported from MappingCache"
            };

            // 导出输入映射
            for (int i = 0; i < cache.InputCount; i++)
            {
                int physicalIndex = cache.GetInputPhysicalIndex(i);
                bool isNC = cache.IsInputNormallyClosed(i);

                config.Inputs.Add(new MappingEntry
                {
                    LogicalIndex = i,
                    PhysicalIndex = physicalIndex,
                    IsNormallyClosed = isNC,
                    Comment = meta?.GetInputName(i) ?? $"Input {i}"
                });
            }

            // 导出输出映射
            for (int i = 0; i < cache.OutputCount; i++)
            {
                int physicalIndex = cache.GetOutputPhysicalIndex(i);
                bool isNC = cache.IsOutputNormallyClosed(i);

                config.Outputs.Add(new MappingEntry
                {
                    LogicalIndex = i,
                    PhysicalIndex = physicalIndex,
                    IsNormallyClosed = isNC,
                    Comment = meta?.GetOutputName(i) ?? $"Output {i}"
                });
            }

            return config;
        }

        /// <summary>
        /// 解析路径（支持相对路径和绝对路径）
        /// </summary>
        private static string ResolvePath(string filePath)
        {
            if (Path.IsPathRooted(filePath))
                return filePath;

            // 相对路径：相对于应用程序目录
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, filePath);
        }

        private static MappingConfigFile? TryLoadLegacy(string json)
        {
            LegacyMappingFile? legacy;
            try
            {
                legacy = JsonConvert.DeserializeObject<LegacyMappingFile>(json);
            }
            catch
            {
                return null;
            }

            if (legacy == null)
            {
                return null;
            }

            var hasInputs = legacy.InputMappings?.Count > 0;
            var hasOutputs = legacy.OutputMappings?.Count > 0;
            if (!hasInputs && !hasOutputs)
            {
                return null;
            }

            var converted = new MappingConfigFile
            {
                Description = string.IsNullOrWhiteSpace(legacy.HardwareType)
                    ? legacy.ConnectionString
                    : $"{legacy.HardwareType} ({legacy.ConnectionString})"
            };

            if (legacy.InputMappings != null)
            {
                foreach (var entry in legacy.InputMappings)
                {
                    converted.Inputs.Add(new MappingEntry
                    {
                        LogicalIndex = entry.LogicalIndex,
                        PhysicalIndex = entry.PhysicalIndex,
                        IsNormallyClosed = !entry.IsNormallyOpen,
                        Comment = entry.Name
                    });
                }
            }

            if (legacy.OutputMappings != null)
            {
                foreach (var entry in legacy.OutputMappings)
                {
                    converted.Outputs.Add(new MappingEntry
                    {
                        LogicalIndex = entry.LogicalIndex,
                        PhysicalIndex = entry.PhysicalIndex,
                        IsNormallyClosed = !entry.IsNormallyOpen,
                        Comment = entry.Name
                    });
                }
            }

            return converted;
        }

        private sealed class LegacyMappingFile
        {
            public string? HardwareType { get; set; }
            public string? ConnectionString { get; set; }
            public List<LegacyMappingEntry>? InputMappings { get; set; }
            public List<LegacyMappingEntry>? OutputMappings { get; set; }
        }

        private sealed class LegacyMappingEntry
        {
            public int LogicalIndex { get; set; }
            public int PhysicalIndex { get; set; }
            public bool IsNormallyOpen { get; set; } = true;
            public string? Name { get; set; }
        }
    }
}
