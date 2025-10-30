using System;
using System.IO;
using Newtonsoft.Json;

namespace Ewan.Model.System
{
    /// <summary>
    /// 系统参数管理器
    /// </summary>
    public class SystemParametersManager
    {
        private static readonly string ConfigFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Config", 
            "system_parameters.json");

        private static SystemParametersManager _instance;
        private static readonly object _lock = new object();

        private SystemParameters _parameters;

        private SystemParametersManager()
        {
            LoadParameters();
        }

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static SystemParametersManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SystemParametersManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 当前系统参数
        /// </summary>
        public SystemParameters Parameters
        {
            get { return _parameters; }
        }

        /// <summary>
        /// 加载系统参数
        /// </summary>
        private void LoadParameters()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    _parameters = JsonConvert.DeserializeObject<SystemParameters>(json);

                    if (_parameters == null || !_parameters.Validate())
                    {
                        _parameters = SystemParameters.CreateDefault();
                    }
                }
                else
                {
                    _parameters = SystemParameters.CreateDefault();
                    SaveParameters(_parameters);
                }
            }
            catch
            {
                _parameters = SystemParameters.CreateDefault();
            }
        }

        /// <summary>
        /// 保存系统参数
        /// </summary>
        public bool SaveParameters(SystemParameters parameters)
        {
            try
            {
                if (parameters == null || !parameters.Validate())
                {
                    return false;
                }

                string configDir = Path.GetDirectoryName(ConfigFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string json = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);

                _parameters = parameters;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 重新加载参数
        /// </summary>
        public void Reload()
        {
            LoadParameters();
        }
    }
}
