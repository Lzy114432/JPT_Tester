using Ewan.Core.Attribute;
using Ewan.Model.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.UI.WebControls;

namespace Ewan.Core.Axis
{
    [Manager(Priority = 1)]
    public class AxisManager : BaseManager<AxisManager>
    {
        ushort card = 0;
        private AxisConfigManager _configManager;
        private readonly string _configFilePath;


        public AxisManager()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "axis_config.json");
        }


        public override bool Init()
        {
            // 加载轴配置
            LoadAxisConfiguration();
            
            //string ip = "192.168.5.11";

            //short res = LTSMC.smc_board_init(card, 2, ip, 115200);//ec则认为只有一个卡
            //if (res != 0)
            //{
            //    _uiLogger.Error("轴管理器初始化失败: {0}", res);
            //    return false;
            //}


            return base.Init();
        }

        public override void Destroy()
        {
            //short res = LTSMC.smc_board_close(card);
            //if (res != 0)
            //{
            //    _uiLogger.Error("轴管理器销毁失败: {0}", res);
            //}
            base.Destroy();
        }
        


        #region 轴配置文件操作

        /// <summary>
        /// 加载轴配置
        /// </summary>
        /// <returns>加载是否成功</returns>
        public bool LoadAxisConfiguration()
        {
            try
            {
                // 检查配置文件是否存在
                bool fileExists = File.Exists(_configFilePath);
                
                _configManager = AxisConfigManager.LoadFromFile(_configFilePath);
                
                if (_configManager?.AxisConfigs?.Count > 0)
                {
                    // 如果文件不存在但成功加载了默认配置，需要保存到文件
                    if (!fileExists)
                    {
                        _uiLogger.Info("加载轴配置失败: {0}", "配置文件不存在，创建默认配置");
                        SaveAxisConfiguration();
                    }
                    else
                    {
                        _uiLogger.Info("轴配置已从以下位置加载: {0}", $"{_configManager.AxisConfigs.Count}个轴配置");
                    }
                    return true;
                }
                else
                {
                    _uiLogger.Info("加载轴配置失败: {0}", "配置为空，创建默认配置");
                    _configManager = AxisConfigManager.CreateDefault();
                    SaveAxisConfiguration();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error("加载轴配置失败: {0}", ex.Message);
                _configManager = AxisConfigManager.CreateDefault();
                SaveAxisConfiguration(); // 保存默认配置到文件
                return true; // 虽然加载失败，但创建了默认配置，返回true
            }
        }

        /// <summary>
        /// 保存轴配置
        /// </summary>
        /// <returns>保存是否成功</returns>
        public bool SaveAxisConfiguration()
        {
            try
            {
                if (_configManager?.SaveToFile(_configFilePath) == true)
                {
                    _uiLogger.Info("轴配置已保存到: {0}", _configFilePath);
                    return true;
                }
                else
                {
                    _uiLogger.Error("保存轴配置失败: {0}", "保存失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error("保存轴配置失败: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 重新加载轴配置
        /// </summary>
        /// <returns>重新加载是否成功</returns>
        public bool ReloadAxisConfiguration()
        {
            bool result = LoadAxisConfiguration();
            if (result)
            {
                _uiLogger.Info("轴配置已从以下位置加载: {0}", "轴配置已重新加载");
                
                // 触发配置更新事件
                ConfigurationUpdated?.Invoke(this, EventArgs.Empty);
            }
            return result;
        }

        /// <summary>
        /// 配置更新事件
        /// </summary>
        public event EventHandler ConfigurationUpdated;

        #endregion

        #region 轴配置访问接口

        /// <summary>
        /// 获取所有轴配置
        /// </summary>
        /// <returns>轴配置列表</returns>
        public List<AxisConfig> GetAllAxisConfigs()
        {
            return _configManager?.AxisConfigs ?? new List<AxisConfig>();
        }

        /// <summary>
        /// 根据轴号获取轴配置
        /// </summary>
        /// <param name="axisId">轴号</param>
        /// <returns>轴配置，未找到返回null</returns>
        public AxisConfig GetAxisConfig(int axisId)
        {
            return _configManager?.GetAxisConfig(axisId);
        }

        #endregion

        #region 轴操作方法 - AxisConfig版本

        public bool Jog(AxisConfig axisConfig, double speed)
        {
            double vel = Math.Abs(speed);
            int dir = speed > 0 ? 1 : 0; // 1正，0负


            LTSMC.smc_set_s_profile(card, (ushort)axisConfig.AxisID, 0, axisConfig.Dec/3);//设置S段时间（0-1s)
            
            // 使用配置中的速度参数，或者使用传入的速度
            double maxSpeed = speed * axisConfig.Step; // 使用配置中的最大速度
            double startSpeed = maxSpeed * 0.1; // 起始速度为最大速度的10%
            double stopSpeed = maxSpeed * 0.05; // 停止速度为最大速度的5%
            
            LTSMC.smc_set_profile_unit(card, (ushort)axisConfig.AxisID
            , startSpeed, maxSpeed, axisConfig.Acc, axisConfig.Dec, stopSpeed);//设置起始速度、运行速度、停止速度、加速时间、减速时间
            LTSMC.smc_vmove(card, (ushort)axisConfig.AxisID, (ushort)dir);
            return true;
        }

        public bool JogDown(AxisConfig axisConfig)
        {
            double vel = Math.Abs(-axisConfig.Speed);
            int dir = -axisConfig.Speed > 0 ? 1 : 0; // 1正，0负


            LTSMC.smc_set_s_profile(card, (ushort)axisConfig.AxisID, 0, axisConfig.Dec / 3);//设置S段时间（0-1s)

            // 使用配置中的速度参数，或者使用传入的速度
            double maxSpeed = -axisConfig.Speed * axisConfig.Step; // 使用配置中的最大速度
            double startSpeed = maxSpeed * 0.1; // 起始速度为最大速度的10%
            double stopSpeed = maxSpeed * 0.05; // 停止速度为最大速度的5%

            LTSMC.smc_set_profile_unit(card, (ushort)axisConfig.AxisID
            , startSpeed, maxSpeed, axisConfig.Acc, axisConfig.Dec, stopSpeed);//设置起始速度、运行速度、停止速度、加速时间、减速时间
            LTSMC.smc_vmove(card, (ushort)axisConfig.AxisID, (ushort)dir);
            return true;
        }


        public bool JogUp(AxisConfig axisConfig)
        {
            double vel = Math.Abs(axisConfig.Speed);
            int dir = axisConfig.Speed > 0 ? 1 : 0; // 1正，0负


            LTSMC.smc_set_s_profile(card, (ushort)axisConfig.AxisID, 0, axisConfig.Dec / 3);//设置S段时间（0-1s)

            // 使用配置中的速度参数，或者使用传入的速度
            double maxSpeed = axisConfig.Speed * axisConfig.Step; // 使用配置中的最大速度
            double startSpeed = maxSpeed * 0.1; // 起始速度为最大速度的10%
            double stopSpeed = maxSpeed * 0.05; // 停止速度为最大速度的5%

            LTSMC.smc_set_profile_unit(card, (ushort)axisConfig.AxisID
            , startSpeed, maxSpeed, axisConfig.Acc, axisConfig.Dec, stopSpeed);//设置起始速度、运行速度、停止速度、加速时间、减速时间
            LTSMC.smc_vmove(card, (ushort)axisConfig.AxisID, (ushort)dir);
            return true;
        }




        public bool JogStop(AxisConfig axisConfig)
        {
            LTSMC.smc_stop(card, (ushort)axisConfig.AxisID, 0);
            return true;
        }


        public bool EnableAxis(AxisConfig axisConfig)
        {
            short ret = LTSMC.smc_write_sevon_pin(card,(ushort)axisConfig.AxisID, 0);//O为使能？
            return ret == 0;
        }


        public void DisableAxis(AxisConfig axisConfig)
        {
            short ret = LTSMC.smc_write_sevon_pin(card, (ushort)axisConfig.AxisID, 1);//O为使能
        }



        public void DecStop(AxisConfig axisConfig)
        {
            short val = LTSMC.smc_stop(card, (ushort)axisConfig.AxisID, 0);
        }

        public void EmgStop(AxisConfig axisConfig)
        {
            short val = LTSMC.smc_stop(card, (ushort)axisConfig.AxisID, 1);
        }


        public void EmgStopAll(AxisConfig axisConfig)
        {
            LTSMC.smc_emg_stop((ushort)axisConfig.AxisID);
        }

        public double Position(AxisConfig axisConfig)
        {

            double pos = 0;
            LTSMC.smc_get_position_unit(card, (ushort)axisConfig.AxisID, ref pos);

            if (axisConfig.MotionDir != MotionDir.Positive)
            {
                pos = 1;
            }
            else
            {
                pos = -1;
            }

            return pos / axisConfig.Step;
        }


        public bool IsAlarm(AxisConfig axisConfig)
        {
            uint val2 = LTSMC.smc_axis_io_status(card, (ushort)axisConfig.AxisID);
            return (val2 & 0x01) > 0;
        }

        public bool IsBusy(AxisConfig axisConfig)
        {
            short rcode = LTSMC.smc_check_done(card, (ushort)axisConfig.AxisID);
            return rcode != 1;
        }



        public bool AbsMove(AxisConfig axisConfig,double pos)
        {
            if (axisConfig.MotionDir == MotionDir.Positive)
            {
                pos =  pos * 1;
            }
            else
            {
                pos = pos * -1;
            }

            int pulse = (int)(pos * axisConfig.Step);

            ushort State_Machine = 0;
            short temp;
            temp = LTSMC.nmcs_get_axis_state_machine(card, (ushort)axisConfig.AxisID, ref State_Machine);
            if (State_Machine == 0)
            {
                //LTSMC.smc_set_pulse_outmode(card.iCardNo, (ushort)Parameter.AxisNum, 0);//设置脉冲模式
                //LTSMC.smc_set_equiv(card.iCardNo, (ushort)Parameter.AxisNum, 1);
                LTSMC.smc_set_s_profile(card, (ushort)axisConfig.AxisID, 0, axisConfig.Dec / 3);//设置S段时间（0-1s)
                LTSMC.smc_set_profile_unit(card, (ushort)axisConfig.AxisID
                , 10000, axisConfig.Speed * axisConfig.Step, axisConfig.Acc, axisConfig.Dec, 10000);//设置起始速度、运行速度、停止速度、加速时间、减速时间
                LTSMC.smc_set_dec_stop_time(card, (ushort)axisConfig.AxisID, axisConfig.Dec);

                LTSMC.smc_pmove_unit(card, (ushort)axisConfig.AxisID, pulse, 1);//定长运动

            }

            //short ret = LTSMC.smc_set_profile_unit(card.iCardNo, (ushort)Parameter.AxisNum
            //    , 10000, Parameter.Speed * Parameter.Step, Parameter.Acc, Parameter.Dec, 10000);
            //ret = LTSMC.smc_set_s_profile(card.iCardNo, (ushort)Parameter.AxisNum, 0, Parameter.Dec / 3);
            //ret = LTSMC.smc_pmove_unit(card.iCardNo, (ushort)Parameter.AxisNum, pulse, 1);//定长运动
            return true;
        }







        #endregion

    }
}