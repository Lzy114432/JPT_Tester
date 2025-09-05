using Ewan.Core.Attribute;
using Ewan.Model;
using MotionSMC304;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace Ewan.Core.Axis
{
    [Manager(Priority = 1)]
    public class AxisManager : BaseManager<AxisManager>
    {
        ushort card = 0;
        private List<AxisParameter> _axisParameters = new List<AxisParameter>();
        private readonly string _configFilePath;

        /// <summary>
        /// 轴参数列表
        /// </summary>
        public List<AxisParameter> AxisParameters 
        { 
            get => _axisParameters; 
            private set => _axisParameters = value; 
        }

        public AxisManager()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "axis_config.json");
        }

        public bool IsBusy
        {
            get
            {
                short rcode = LTSMC.smc_check_done(card, 0);
                return rcode != 1;
            }
        }


        public bool IsAlarm
        {
            get
            {
                uint val2 = LTSMC.smc_axis_io_status(card, 0);
                return (val2 & 0x01) > 0;
            }
        }

        public double Position
        {
            get
            {
                double pos = 0;
                LTSMC.smc_get_position_unit(card, 0, ref pos);
                pos = pos * (true ? -1 : 1);
                return pos / 50;
            }
            set => LTSMC.smc_set_position_unit(card, 0, value);
        }





        public override bool Init()
        {
            // 加载轴参数配置
            LoadAxisParameters();
            
            //string ip = "192.168.5.11";

            //short res = LTSMC.smc_board_init(card, 2, ip, 115200);//ec则认为只有一个卡
            //if (res != 0)
            //{
            //    _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisManagerInitFailed, res);
            //    return false;
            //}

            // 应用加载的轴参数到硬件
            ApplyAxisParametersToHardware();

            return base.Init();
        }
       

        public override void Destroy()
        {
            short res = LTSMC.smc_board_close(card);
            if (res != 0)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisManagerDestroyFailed, res);
            }
            base.Destroy();
        }



        // 直接控制指定轴Jog - 带默认值
        public void DirectJog(double speed, double step = 50, double acc = 100, double dec = 100)
        {
            ushort axisNum = 0;
            double vel = Math.Abs(speed);
            int dir = speed > 0 ? 1 : 0;

            LTSMC.smc_set_s_profile(card, axisNum, 0, dec / 3);
            LTSMC.smc_set_profile_unit(card, axisNum, 10000, vel * step, acc, dec, 10000);
            LTSMC.smc_vmove(card, axisNum, (ushort)dir);
        }

        // 停止指定轴 - 带默认值
        public void DirectJogStop(ushort axisNum = 0)
        {
            LTSMC.smc_stop(card, axisNum, 0);
        }



        public void EmgStop(ushort axisNum = 0)
        {
            short val = LTSMC.smc_stop(card, axisNum, 1);
        }

        #region 轴参数文件操作

        /// <summary>
        /// 从JSON文件加载轴参数
        /// </summary>
        /// <returns>加载是否成功</returns>
        public bool LoadAxisParameters()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisConfigurationLoadFailed, "配置文件不存在，将创建默认配置");
                    CreateDefaultAxisParameters();
                    SaveAxisParameters();
                    return true;
                }

                string jsonContent = File.ReadAllText(_configFilePath);
                var config = JsonConvert.DeserializeObject<AxisConfiguration>(jsonContent);

                if (config?.AxisParameters != null)
                {
                    // 转换AxisConfig到AxisParameter
                    _axisParameters = config.AxisParameters.Select(ac => ac.ToAxisParameter()).ToList();
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisConfigurationLoaded, $"{_axisParameters.Count}个轴参数");
                    return true;
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisConfigurationLoadFailed, "配置文件格式错误");
                    CreateDefaultAxisParameters();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisConfigurationLoadFailed, ex.Message);
                CreateDefaultAxisParameters();
                return false;
            }
        }

        /// <summary>
        /// 保存轴参数到JSON文件
        /// </summary>
        /// <returns>保存是否成功</returns>
        public bool SaveAxisParameters()
        {
            try
            {
                // 确保Config目录存在
                string configDir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.DirectoryCreated, configDir);
                }

                // 转换AxisParameter到AxisConfig用于保存
                var axisConfigs = _axisParameters.Select(ap => AxisConfig.FromAxisParameter(ap)).ToList();
                
                var config = new AxisConfiguration
                {
                    AxisParameters = axisConfigs,
                    SaveTime = DateTime.Now
                };

                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configFilePath, jsonContent);

                _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisConfigurationSaved, _configFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisConfigurationSaveFailed, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 创建默认轴参数
        /// </summary>
        private void CreateDefaultAxisParameters()
        {
            _axisParameters = new List<AxisParameter>();
            
            // 创建默认的3个轴参数 (X, Y, Z)
            for (int i = 0; i < 3; i++)
            {
                _axisParameters.Add(new AxisParameter
                {
                    AxisNum = i,
                    Dir = false,      // 默认方向
                    Speed = 100.0,    // 默认速度 mm/s
                    Acc = 200.0,      // 默认加速度 mm/s²
                    Dec = 200.0       // 默认减速度 mm/s²
                });
            }

            _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisParametersReset);
        }

        /// <summary>
        /// 应用轴参数到硬件
        /// </summary>
        public void ApplyAxisParametersToHardware()
        {
            try
            {
                foreach (var param in _axisParameters)
                {
                    if (param.AxisNum >= 0 && param.AxisNum < 16) // 通常控制卡支持0-15轴
                    {
                        // 设置轴参数到硬件
                        // 注意：这里的API调用需要根据实际的控制卡API进行调整
                        LTSMC.smc_set_profile_unit(card, (ushort)param.AxisNum, 
                            10000,                    // 最小速度
                            param.Speed,              // 最大速度
                            param.Acc,               // 加速度
                            param.Dec,               // 减速度
                            10000);                  // 停止减速度

                        _uiLogger.Debug(() => Ewan.Resources.LogMessages.AxisAdded, 
                            $"轴{param.AxisNum}: 速度={param.Speed}, 加速度={param.Acc}, 减速度={param.Dec}");
                    }
                }

                _uiLogger.Info(() => Ewan.Resources.LogMessages.OperationCompleted, "轴参数应用到硬件");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, $"应用轴参数到硬件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定轴的参数
        /// </summary>
        /// <param name="axisNum">轴号</param>
        /// <returns>轴参数，如果未找到返回null</returns>
        public AxisParameter GetAxisParameter(int axisNum)
        {
            return _axisParameters.FirstOrDefault(ap => ap.AxisNum == axisNum);
        }

        /// <summary>
        /// 设置指定轴的参数
        /// </summary>
        /// <param name="axisParameter">轴参数</param>
        /// <returns>设置是否成功</returns>
        public bool SetAxisParameter(AxisParameter axisParameter)
        {
            try
            {
                var existingParam = _axisParameters.FirstOrDefault(ap => ap.AxisNum == axisParameter.AxisNum);
                if (existingParam != null)
                {
                    // 更新现有参数
                    existingParam.Dir = axisParameter.Dir;
                    existingParam.Speed = axisParameter.Speed;
                    existingParam.Acc = axisParameter.Acc;
                    existingParam.Dec = axisParameter.Dec;
                }
                else
                {
                    // 添加新参数
                    _axisParameters.Add(axisParameter);
                }

                // 立即应用到硬件
                if (axisParameter.AxisNum >= 0 && axisParameter.AxisNum < 16)
                {
                    LTSMC.smc_set_profile_unit(card, (ushort)axisParameter.AxisNum,
                        10000, axisParameter.Speed, axisParameter.Acc, axisParameter.Dec, 10000);
                }

                _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisAdded, axisParameter.AxisNum);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisAddFailed, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 重新加载轴参数配置
        /// </summary>
        /// <returns>重新加载是否成功</returns>
        public bool ReloadAxisParameters()
        {
            if (LoadAxisParameters())
            {
                ApplyAxisParametersToHardware();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取所有轴参数的副本
        /// </summary>
        /// <returns>轴参数列表的副本</returns>
        public List<AxisParameter> GetAllAxisParameters()
        {
            return _axisParameters.Select(ap => new AxisParameter
            {
                AxisNum = ap.AxisNum,
                Dir = ap.Dir,
                Speed = ap.Speed,
                Acc = ap.Acc,
                Dec = ap.Dec
            }).ToList();
        }

        /// <summary>
        /// 批量设置轴参数
        /// </summary>
        /// <param name="axisParameters">轴参数列表</param>
        /// <returns>设置是否成功</returns>
        public bool SetAllAxisParameters(List<AxisParameter> axisParameters)
        {
            try
            {
                _axisParameters = axisParameters.Select(ap => new AxisParameter
                {
                    AxisNum = ap.AxisNum,
                    Dir = ap.Dir,
                    Speed = ap.Speed,
                    Acc = ap.Acc,
                    Dec = ap.Dec
                }).ToList();

                // 应用到硬件
                ApplyAxisParametersToHardware();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.OperationCompleted, $"批量设置{axisParameters.Count}个轴参数");
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, $"批量设置轴参数失败: {ex.Message}");
                return false;
            }
        }

        #endregion

    }
}
