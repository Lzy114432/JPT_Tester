using EwanCore;
using EwanCore.Attribute;
using EwanCommon.Logging;
using log4net;
using Ewan.Model.Config;
using EwanAxis.Core.Interfaces;
using EwanAxis.Hardware.SMC606;
using System;
using System.Collections.Generic;
using System.IO;

namespace Ewan.Core.Axis
{
    [Manager(Priority = 1)]
    public class AxisManager : IManager
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(AxisManager));
        private bool _disposed;

        #region 单例支持
        private static readonly Lazy<AxisManager> s_instance = new Lazy<AxisManager>(() => new AxisManager());
        public static AxisManager Instance() => s_instance.Value;
        #endregion

        private AxisConfigManager _configManager;
        private readonly string _configFilePath;
        private SMC606Card _card;

        /// <summary>
        /// 控制卡IP地址（可通过配置文件或外部设置）
        /// </summary>
        public string CardIpAddress { get; set; } = "192.168.5.11";

        /// <summary>
        /// 控制卡卡号
        /// </summary>
        public ushort CardNo { get; set; } = 0;

        /// <summary>
        /// 控制卡是否已连接
        /// </summary>
        public bool IsConnected => _card?.IsConnected ?? false;

        public AxisManager()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "axis_config.json");
        }

        public bool Init()
        {
            s_logger.Info("AxisManager 初始化开始");

            // 加载轴配置
            LoadAxisConfiguration();

            // 初始化SMC606控制卡
            if (!InitializeCard())
            {
                s_logger.Error("AxisManager 初始化失败: 控制卡连接失败");
                return false;
            }

            s_logger.Info("AxisManager 初始化完成");
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_logger.Info("AxisManager 开始销毁");

            try
            {
                _card?.Disconnect();
                _card?.Dispose();
                _card = null;
                s_logger.Info("AxisManager 销毁完成");
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("AxisManager 销毁异常: {0}", ex.Message);
            }
        }

        [Obsolete("请使用 Dispose() 方法")]
        public void Destroy() => Dispose();

        /// <summary>
        /// 初始化控制卡
        /// </summary>
        private bool InitializeCard()
        {
            try
            {
                _card = new SMC606Card(CardNo, CardIpAddress);

                // 初始化控制卡（加载配置）
                if (!_card.Initialize(_configFilePath))
                {
                    s_logger.Error("控制卡初始化失败");
                    return false;
                }

                // 连接控制卡
                if (!_card.Connect())
                {
                    s_logger.ErrorFormat("控制卡连接失败: IP={0}", CardIpAddress);
                    return false;
                }

                s_logger.InfoFormat("控制卡连接成功: IP={0}, 轴数={1}", CardIpAddress, _card.AxisCount);
                return true;
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("控制卡初始化异常: {0}", ex.Message);
                return false;
            }
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
                bool fileExists = File.Exists(_configFilePath);

                _configManager = AxisConfigManager.LoadFromFile(_configFilePath);

                if (_configManager?.AxisConfigs?.Count > 0)
                {
                    if (!fileExists)
                    {
                        s_logger.Info("轴配置文件不存在，创建默认配置");
                        SaveAxisConfiguration();
                    }
                    else
                    {
                        s_logger.InfoFormat("轴配置加载完成: {0}个轴配置", _configManager.AxisConfigs.Count);
                    }
                    return true;
                }
                else
                {
                    s_logger.Info("轴配置为空，创建默认配置");
                    _configManager = AxisConfigManager.CreateDefault();
                    SaveAxisConfiguration();
                    return true;
                }
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("加载轴配置失败: {0}", ex.Message);
                _configManager = AxisConfigManager.CreateDefault();
                SaveAxisConfiguration();
                return true;
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
                    s_logger.Info("轴配置已保存");
                    return true;
                }
                else
                {
                    s_logger.Error("保存轴配置失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("保存轴配置失败: {0}", ex.Message);
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
                s_logger.Info("轴配置已重新加载");
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

        #region 私有辅助方法

        /// <summary>
        /// 根据AxisConfig获取对应的IAxis
        /// </summary>
        private IAxis GetAxis(AxisConfig axisConfig)
        {
            if (_card == null || !_card.IsConnected)
                return null;

            if (axisConfig.AxisID < 0 || axisConfig.AxisID >= _card.AxisCount)
                return null;

            return _card[axisConfig.AxisID];
        }

        #endregion

        #region 轴操作方法

        public bool Jog(AxisConfig axisConfig, double speed)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return false;

            // 根据配置的运动方向调整速度方向
            double actualSpeed = axisConfig.MotionDir == MotionDir.Positive ? speed : -speed;
            return axis.Jog(actualSpeed);
        }

        public bool JogDown(AxisConfig axisConfig)
        {
            return Jog(axisConfig, -axisConfig.Speed);
        }

        public bool JogUp(AxisConfig axisConfig)
        {
            return Jog(axisConfig, axisConfig.Speed);
        }

        public bool JogStop(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return false;

            return axis.JogStop();
        }

        public bool EnableAxis(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return false;

            axis.ServoOn = true;
            return axis.ServoOn;
        }

        public void DisableAxis(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return;

            axis.ServoOn = false;
        }

        public void DecStop(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            axis?.DecStop();
        }

        public void EmgStop(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            axis?.EmgStop();
        }

        public void EmgStopAll(AxisConfig axisConfig)
        {
            _card?.EmgStopAll();
        }

        public double Position(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return 0;

            double pos = axis.Position;

            // 根据运动方向转换位置
            if (axisConfig.MotionDir != MotionDir.Positive)
            {
                pos = -pos;
            }

            return pos;
        }

        public bool IsAlarm(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return false;

            return axis.IsAlarm;
        }

        public bool IsBusy(AxisConfig axisConfig)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return false;

            return axis.IsBusy;
        }

        public bool AbsMove(AxisConfig axisConfig, double pos)
        {
            var axis = GetAxis(axisConfig);
            if (axis == null) return false;

            // 根据运动方向转换位置
            double actualPos = axisConfig.MotionDir == MotionDir.Positive ? pos : -pos;

            return axis.AbsMove(actualPos);
        }

        #endregion
    }
}