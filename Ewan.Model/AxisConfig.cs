using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ewan.Model
{
    /// <summary>
    /// 轴参数配置（扩展版本，支持UI绑定和名称）
    /// </summary>
    public class AxisConfig : AxisParameter, INotifyPropertyChanged
    {
        private string _name;
        private bool _isEnabled = true;

        /// <summary>
        /// 轴名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 是否启用该轴
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 创建默认轴配置
        /// </summary>
        public static AxisConfig CreateDefault(int axisNum)
        {
            string[] defaultNames = { "X轴", "Y轴", "Z轴", "A轴", "B轴", "C轴" };
            
            return new AxisConfig
            {
                AxisNum = axisNum,
                Name = axisNum < defaultNames.Length ? defaultNames[axisNum] : $"轴{axisNum}",
                Dir = false,
                Speed = 100.0,
                Acc = 200.0,
                Dec = 200.0,
                IsEnabled = true
            };
        }

        /// <summary>
        /// 转换为基础AxisParameter
        /// </summary>
        public AxisParameter ToAxisParameter()
        {
            return new AxisParameter
            {
                AxisNum = this.AxisNum,
                Dir = this.Dir,
                Speed = this.Speed,
                Acc = this.Acc,
                Dec = this.Dec
            };
        }

        /// <summary>
        /// 从AxisParameter创建AxisConfig
        /// </summary>
        public static AxisConfig FromAxisParameter(AxisParameter parameter)
        {
            string[] defaultNames = { "X轴", "Y轴", "Z轴", "A轴", "B轴", "C轴" };
            
            return new AxisConfig
            {
                AxisNum = parameter.AxisNum,
                Name = parameter.AxisNum < defaultNames.Length ? defaultNames[parameter.AxisNum] : $"轴{parameter.AxisNum}",
                Dir = parameter.Dir,
                Speed = parameter.Speed,
                Acc = parameter.Acc,
                Dec = parameter.Dec,
                IsEnabled = true
            };
        }
    }

    /// <summary>
    /// 轴配置文件结构
    /// </summary>
    public class AxisConfiguration
    {
        /// <summary>
        /// 轴参数列表
        /// </summary>
        public List<AxisConfig> AxisParameters { get; set; } = new List<AxisConfig>();

        /// <summary>
        /// 保存时间
        /// </summary>
        public DateTime SaveTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 下拉选择项
    /// </summary>
    public class OptionItem
    {
        public string Display { get; set; }
        public bool Value { get; set; }
    }
}