/*===================================================
 * 类名称: AxisCardBase
 * 类描述: 轴卡抽象基类，提供通用实现
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴卡抽象基类
    /// 子类需要实现具体的硬件通信
    /// </summary>
    public abstract class AxisCardBase : IAxisCard
    {
        protected readonly List<IAxis> _axes = new List<IAxis>();
        protected string? _configPath;
        protected bool _disposed;

        public string CardName { get; set; } = "";
        public abstract int CardIndex { get; }
        public bool IsInitialized { get; protected set; }
        public bool IsConnected { get; protected set; }
        public int AxisCount => _axes.Count;
        public IReadOnlyList<IAxis> Axes => _axes.AsReadOnly();

        public IAxis this[int index]
        {
            get
            {
                if (index < 0 || index >= _axes.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _axes[index];
            }
        }

        public IAxis? GetAxisByName(string name)
        {
            return _axes.FirstOrDefault(a => a.Name == name);
        }

        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<AxisAlarmEventArgs>? AxisAlarm;

        #region 生命周期

        public virtual bool Initialize(string configPath)
        {
            if (IsInitialized) return true;

            _configPath = configPath;

            if (File.Exists(configPath))
            {
                LoadConfig(configPath);
            }
            else
            {
                CreateDefaultConfig();
                // 如果配置文件不存在，则生成默认配置文件，方便后续直接编辑。
                SaveConfig(configPath);
            }

            IsInitialized = true;
            return true;
        }

        public abstract bool Connect();
        public abstract bool Disconnect();

        public virtual bool SaveConfig(string? configPath = null)
        {
            var path = configPath ?? _configPath;
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var config = new AxisCardConfig
                {
                    CardName = CardName,
                    Axes = _axes.Select(a => a.Parameter).ToList()
                };

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected virtual void LoadConfig(string configPath)
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<AxisCardConfig>(json);
                if (config != null && config.Axes.Count > 0)
                {
                    CardName = config.CardName;
                    foreach (var param in config.Axes)
                    {
                        var axis = CreateAxis(param);
                        _axes.Add(axis);
                    }

                    return;
                }

                // Backward compatible: support legacy axis_config.json schema (AxisConfigs[])
                // used by older machine projects.
                var legacy = JsonConvert.DeserializeObject<LegacyAxisConfigManager>(json);
                if (legacy?.AxisConfigs?.Count > 0)
                {
                    foreach (var legacyAxis in legacy.AxisConfigs)
                    {
                        if (!legacyAxis.IsUsing)
                        {
                            continue;
                        }

                        var step = legacyAxis.Step <= 0 ? 1 : legacyAxis.Step;

                        var parameter = new AxisParameter
                        {
                            Name = string.IsNullOrWhiteSpace(legacyAxis.Name) ? $"Axis{legacyAxis.AxisID}" : legacyAxis.Name!,
                            AxisNum = legacyAxis.AxisID,
                            Step = step,
                            Dir = legacyAxis.MotionDir.HasValue && legacyAxis.MotionDir.Value != 1,
                            Speed = legacyAxis.Speed,
                            Acc = NormalizeLegacyTimeSeconds(legacyAxis.Acc),
                            Dec = NormalizeLegacyTimeSeconds(legacyAxis.Dec),
                            HomeDir = legacyAxis.HomingDir == 1,
                            HomeMode = legacyAxis.HomeMode ?? 0,
                            HomeSpeed = legacyAxis.HomeSpeed > 0
                                ? legacyAxis.HomeSpeed
                                : (legacyAxis.Speed > 0 ? legacyAxis.Speed : 10),
                            HomeIO = legacyAxis.HomeIO ?? 0,
                            SoftLimitEnable = legacyAxis.MaxPos > legacyAxis.MinPos,
                            SoftLimitPositive = legacyAxis.MaxPos,
                            SoftLimitNegative = legacyAxis.MinPos
                        };

                        var axis = CreateAxis(parameter);
                        _axes.Add(axis);
                    }

                    return;
                }

                CreateDefaultConfig();
            }
            catch
            {
                CreateDefaultConfig();
            }
        }

        private static double NormalizeLegacyTimeSeconds(double value)
        {
            // Heuristic: legacy configs often store accel/decel in milliseconds (e.g. 6500).
            // Treat large values as ms and convert to seconds to match EwanAxis' API.
            if (value <= 0) return 0.1;
            return value > 10 ? value / 1000.0 : value;
        }

        protected virtual void CreateDefaultConfig()
        {
            // 子类可以重写此方法创建默认配置
        }

        /// <summary>
        /// 创建轴实例（子类必须实现）
        /// </summary>
        protected abstract IAxis CreateAxis(AxisParameter parameter);

        #endregion

        #region 全局操作

        public virtual void EmgStopAll()
        {
            foreach (var axis in _axes)
            {
                axis.EmgStop();
            }
        }

        public virtual void ServoOnAll(bool enable)
        {
            foreach (var axis in _axes)
            {
                axis.ServoOn = enable;
            }
        }

        public virtual void ClearAllErrors()
        {
            foreach (var axis in _axes)
            {
                axis.ClearError();
            }
        }

        #endregion

        #region 事件触发

        protected void OnConnectionChanged(bool isConnected, string? message = null)
        {
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(isConnected, message));
        }

        protected void OnAxisAlarm(int axisIndex, string axisName, string alarmMessage)
        {
            AxisAlarm?.Invoke(this, new AxisAlarmEventArgs(axisIndex, axisName, alarmMessage));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                if (IsConnected)
                {
                    Disconnect();
                }
            }

            _disposed = true;
        }

        ~AxisCardBase()
        {
            Dispose(false);
        }

        #endregion
    }

    /// <summary>
    /// 轴卡配置文件结构
    /// </summary>
    internal class AxisCardConfig
    {
        public string CardName { get; set; } = "";
        public List<AxisParameter> Axes { get; set; } = new List<AxisParameter>();
    }

    internal sealed class LegacyAxisConfigManager
    {
        public List<LegacyAxisConfig> AxisConfigs { get; set; } = new List<LegacyAxisConfig>();
    }

    internal sealed class LegacyAxisConfig
    {
        public int AxisID { get; set; }
        public string? Name { get; set; }
        public bool IsUsing { get; set; }
        public double MaxPos { get; set; }
        public double MinPos { get; set; }
        public double Step { get; set; }
        public int HomingDir { get; set; }
        public double Speed { get; set; }
        public double Acc { get; set; }
        public double Dec { get; set; }
        public int? MotionDir { get; set; }
        public int? HomeMode { get; set; }
        public double HomeSpeed { get; set; }
        public int? HomeIO { get; set; }
    }
}
