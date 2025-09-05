using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ewan.Model.Config
{
    /// <summary>
    /// 轴配置文件管理器
    /// </summary>
    public class AxisConfigManager
    {
        /// <summary>
        /// 轴配置列表
        /// </summary>
        public List<AxisConfig> AxisConfigs { get; set; } = new List<AxisConfig>();

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime SaveTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 获取指定ID的轴配置
        /// </summary>
        /// <param name="axisID">轴ID</param>
        /// <returns>轴配置，未找到返回null</returns>
        public AxisConfig GetAxisConfig(int axisID)
        {
            return AxisConfigs.FirstOrDefault(config => config.AxisID == axisID);
        }

        /// <summary>
        /// 获取所有启用的轴配置
        /// </summary>
        /// <returns>启用的轴配置列表</returns>
        public List<AxisConfig> GetEnabledAxisConfigs()
        {
            return AxisConfigs.Where(config => config.IsUsing).ToList();
        }

        /// <summary>
        /// 添加或更新轴配置
        /// </summary>
        /// <param name="axisConfig">轴配置</param>
        public void SetAxisConfig(AxisConfig axisConfig)
        {
            var existingConfig = GetAxisConfig(axisConfig.AxisID);
            if (existingConfig != null)
            {
                // 更新现有配置
                var index = AxisConfigs.IndexOf(existingConfig);
                AxisConfigs[index] = axisConfig;
            }
            else
            {
                // 添加新配置
                AxisConfigs.Add(axisConfig);
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置管理器</returns>
        public static AxisConfigManager CreateDefault()
        {
            var manager = new AxisConfigManager();
            
            // 创建3个默认轴配置
            for (int i = 0; i < 3; i++)
            {
                var axisConfig = new AxisConfig
                {
                    AxisID = i,
                    IsUsing = i == 0, // 只有第一个轴默认启用
                    MaxPos = 700.0f,
                    MinPos = -11.0f,
                    HomingDir = HomingDir.Positive,
                    AxisSpeed = CreateDefaultSpeedProfile()
                };
                
                manager.AxisConfigs.Add(axisConfig);
            }
            
            return manager;
        }

        /// <summary>
        /// 创建默认速度配置
        /// </summary>
        /// <returns>默认速度配置</returns>
        private static AxisSpeed CreateDefaultSpeedProfile()
        {
            return new AxisSpeed
            {
                SpeedName = "HighSpd",
                Jerk = 500000,
                MaxSpeed = 1000,
                MinSpeed = 800,
                Acc = 6500,
                Dec = 6500
            };
        }

        /// <summary>
        /// 从JSON文件加载配置
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>配置管理器</returns>
        public static AxisConfigManager LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return CreateDefault();
                }

                string jsonContent = File.ReadAllText(filePath);
                var manager = JsonConvert.DeserializeObject<AxisConfigManager>(jsonContent);
                
                return manager ?? CreateDefault();
            }
            catch (Exception)
            {
                return CreateDefault();
            }
        }

        /// <summary>
        /// 保存配置到JSON文件
        /// </summary>
        /// <param name="filePath">配置文件路径</param>
        /// <returns>保存是否成功</returns>
        public bool SaveToFile(string filePath)
        {
            try
            {
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SaveTime = DateTime.Now;
                string jsonContent = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, jsonContent);
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}