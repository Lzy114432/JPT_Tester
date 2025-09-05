using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Ewan.Model.Config;
using Ewan.Core.Culture;
using Ewan.Core.Security;
using Ewan.Core;
using Ewan.Core.Logger;
using Newtonsoft.Json;
using System.ComponentModel;

namespace MarkingMachineFeeder.Viewmodel
{
    public class AxisConfigViewModel : BindableBase
    {
        private readonly CultureManager _cultureManager = CultureManager.Instance();
        private readonly SecurityManager _securityManager = SecurityManager.Instance();
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        
        #region Properties
        
        private ObservableCollection<AxisConfig> _axisParameters;
        public ObservableCollection<AxisConfig> AxisParameters
        {
            get => _axisParameters;
            set => SetProperty(ref _axisParameters, value);
        }

        private ObservableCollection<OptionItem> _directionOptions;
        public ObservableCollection<OptionItem> DirectionOptions
        {
            get => _directionOptions;
            set => SetProperty(ref _directionOptions, value);
        }

        private ObservableCollection<HomingDirOption> _homingDirOptions;
        public ObservableCollection<HomingDirOption> HomingDirOptions
        {
            get => _homingDirOptions;
            set => SetProperty(ref _homingDirOptions, value);
        }



        private AxisConfig _selectedAxis;
        public AxisConfig SelectedAxis
        {
            get => _selectedAxis;
            set 
            { 
                if (SetProperty(ref _selectedAxis, value))
                {
                    // Refresh commands when selection changes
                    RemoveAxisCommand?.RaiseCanExecuteChanged();
                    
                    // Update UI visibility property
                    RaisePropertyChanged(nameof(HasSelectedAxis));
                    RaisePropertyChanged(nameof(HasNoSelectedAxis));
                }
            }
        }

        /// <summary>
        /// 是否有选中的轴（用于控制详细配置区域的显示）
        /// </summary>
        public bool HasSelectedAxis => SelectedAxis != null;

        /// <summary>
        /// 是否没有选中轴（用于控制提示文本的显示）
        /// </summary>
        public bool HasNoSelectedAxis => SelectedAxis == null;

        #region UI Texts
        
        private string _windowTitle = "轴参数配置";
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        private string _axisParametersText = "轴参数配置";
        public string AxisParametersText
        {
            get => _axisParametersText;
            set => SetProperty(ref _axisParametersText, value);
        }

        private string _addAxisText = "添加轴";
        public string AddAxisText
        {
            get => _addAxisText;
            set => SetProperty(ref _addAxisText, value);
        }

        private string _removeAxisText = "删除轴";
        public string RemoveAxisText
        {
            get => _removeAxisText;
            set => SetProperty(ref _removeAxisText, value);
        }


        private string _axisNumHeaderText = "轴号";
        public string AxisIDHeaderText
        {
            get => _axisNumHeaderText;
            set => SetProperty(ref _axisNumHeaderText, value);
        }

        private string _maxPosHeaderText = "最大位置(mm)";
        public string MaxPosHeaderText
        {
            get => _maxPosHeaderText;
            set => SetProperty(ref _maxPosHeaderText, value);
        }

        private string _minPosHeaderText = "最小位置(mm)";
        public string MinPosHeaderText
        {
            get => _minPosHeaderText;
            set => SetProperty(ref _minPosHeaderText, value);
        }

        private string _homingDirHeaderText = "回零方向";
        public string HomingDirHeaderText
        {
            get => _homingDirHeaderText;
            set => SetProperty(ref _homingDirHeaderText, value);
        }


        private string _axisNameHeaderText = "轴名称";
        public string AxisNameHeaderText
        {
            get => _axisNameHeaderText;
            set => SetProperty(ref _axisNameHeaderText, value);
        }

        private string _directionHeaderText = "方向";
        public string DirectionHeaderText
        {
            get => _directionHeaderText;
            set => SetProperty(ref _directionHeaderText, value);
        }

        private string _speedHeaderText = "速度(mm/s)";
        public string SpeedHeaderText
        {
            get => _speedHeaderText;
            set => SetProperty(ref _speedHeaderText, value);
        }

        private string _accHeaderText = "加速度(mm/s²)";
        public string AccHeaderText
        {
            get => _accHeaderText;
            set => SetProperty(ref _accHeaderText, value);
        }

        private string _decHeaderText = "减速度(mm/s²)";
        public string DecHeaderText
        {
            get => _decHeaderText;
            set => SetProperty(ref _decHeaderText, value);
        }

        private string _enabledHeaderText = "使能";
        public string EnabledHeaderText
        {
            get => _enabledHeaderText;
            set => SetProperty(ref _enabledHeaderText, value);
        }

        private string _okText = "确定";
        public string OKText
        {
            get => _okText;
            set => SetProperty(ref _okText, value);
        }

        #endregion

        #endregion

        #region Commands
        
        public DelegateCommand AddAxisCommand { get; private set; }
        public DelegateCommand RemoveAxisCommand { get; private set; }
        public DelegateCommand OKCommand { get; private set; }

        #endregion

        public AxisConfigViewModel()
        {
            InitializeCommands();
            InitializeData();
            
            // Check if in design mode
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                InitializeDesignTimeData();
            }
            else
            {
                UpdateUITexts();
                _cultureManager.CultureChanged += OnCultureChanged;
            }
        }

        private void InitializeCommands()
        {
            AddAxisCommand = new DelegateCommand(ExecuteAddAxis);
            RemoveAxisCommand = new DelegateCommand(ExecuteRemoveAxis, CanExecuteRemoveAxis);
            OKCommand = new DelegateCommand(ExecuteOK);
        }

        private void InitializeData()
        {
            // Initialize direction options
            DirectionOptions = new ObservableCollection<OptionItem>
            {
                new OptionItem { Display = "正向", Value = true },
                new OptionItem { Display = "反向", Value = false }
            };

            // Initialize homing direction options
            HomingDirOptions = new ObservableCollection<HomingDirOption>
            {
                new HomingDirOption { Display = "正方向", Value = HomingDir.Positive },
                new HomingDirOption { Display = "负方向", Value = HomingDir.Negative }
            };

            // Try to load from JSON file
            if (!LoadFromJsonFile())
            {
                // If loading fails, initialize with default data
                InitializeDefaultData();
            }
        }

        private void InitializeDesignTimeData()
        {
            // Set design-time text values
            WindowTitle = "轴参数配置";
            AxisParametersText = "轴参数配置";
            AddAxisText = "添加轴";
            RemoveAxisText = "删除轴";
            AxisIDHeaderText = "轴号";
            AxisNameHeaderText = "轴名称";
            DirectionHeaderText = "方向";
            SpeedHeaderText = "速度(mm/s)";
            AccHeaderText = "加速度(mm/s²)";
            DecHeaderText = "减速度(mm/s²)";
            EnabledHeaderText = "使能";
            OKText = "确定";
            
            // 使用AxisConfigManager创建默认配置
            var defaultManager = AxisConfigManager.CreateDefault();
            AxisParameters = new ObservableCollection<AxisConfig>(defaultManager.AxisConfigs);
            
            // Auto-select first axis for design time
            if (AxisParameters.Count > 0)
            {
                SelectedAxis = AxisParameters[0];
            }
        }

        private bool LoadFromJsonFile()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "axis_config.json");
                
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"轴配置文件不存在: {configPath}");
                    return false;
                }
                
                string jsonContent = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<AxisConfigManager>(jsonContent);
                
                if (config == null)
                {
                    System.Diagnostics.Debug.WriteLine("反序列化轴配置失败");
                    return false;
                }
                
                // Load axis parameters
                AxisParameters = new ObservableCollection<AxisConfig>(config.AxisConfigs ?? new System.Collections.Generic.List<AxisConfig>());
                
                // Auto-select first axis if available
                if (AxisParameters.Count > 0)
                {
                    SelectedAxis = AxisParameters[0];
                }
                
                System.Diagnostics.Debug.WriteLine($"成功加载轴配置: {AxisParameters.Count} 个轴");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载轴配置文件失败: {ex.Message}");
                return false;
            }
        }

        private void InitializeDefaultData()
        {
            AxisParameters = new ObservableCollection<AxisConfig>();
            
            // Create default axes (X, Y, Z)
            for (int i = 0; i < 3; i++)
            {
                var defaultConfig = new AxisConfig
                {
                    AxisID = i,
                    IsUsing = i == 0, // 只启用第一个轴
                    MaxPos = 700.0f,
                    MinPos = -11.0f,
                    HomingDir = HomingDir.Positive,
                    AxisSpeed = new AxisSpeed { SpeedName = "HighSpd", Jerk = 500000, MaxSpeed = 1000, MinSpeed = 800, Acc = 6500, Dec = 6500 }
                };
                AxisParameters.Add(defaultConfig);
            }
            
            // Auto-select first axis if available
            if (AxisParameters.Count > 0)
            {
                SelectedAxis = AxisParameters[0];
            }
        }

        private void UpdateUITexts()
        {
            // Update UI texts based on current culture
            if (_cultureManager.CurrentCulture.Name == "zh-CN")
            {
                WindowTitle = "轴参数配置";
                AxisParametersText = "轴参数配置";
                AddAxisText = "添加轴";
                RemoveAxisText = "删除轴";
                AxisIDHeaderText = "轴号";
                AxisNameHeaderText = "轴名称";
                DirectionHeaderText = "方向";
                HomingDirHeaderText = "回零方向";
                MaxPosHeaderText = "最大位置(mm)";
                MinPosHeaderText = "最小位置(mm)";
                SpeedHeaderText = "速度(mm/s)";
                AccHeaderText = "加速度(mm/s²)";
                DecHeaderText = "减速度(mm/s²)";
                EnabledHeaderText = "使能";
                OKText = "确定";
                
                // Update direction options
                if (DirectionOptions != null)
                {
                    DirectionOptions[0].Display = "正向";
                    DirectionOptions[1].Display = "反向";
                }
                
                // Update homing direction options
                if (HomingDirOptions != null)
                {
                    HomingDirOptions[0].Display = "正方向";
                    HomingDirOptions[1].Display = "负方向";
                }
            }
            else
            {
                WindowTitle = "Axis Parameter Configuration";
                AxisParametersText = "Axis Parameters";
                AddAxisText = "Add Axis";
                RemoveAxisText = "Remove Axis";
                AxisIDHeaderText = "Axis No.";
                AxisNameHeaderText = "Axis Name";
                DirectionHeaderText = "Direction";
                HomingDirHeaderText = "Homing Direction";
                MaxPosHeaderText = "Max Position(mm)";
                MinPosHeaderText = "Min Position(mm)";
                SpeedHeaderText = "Speed(mm/s)";
                AccHeaderText = "Acceleration(mm/s²)";
                DecHeaderText = "Deceleration(mm/s²)";
                EnabledHeaderText = "Enabled";
                OKText = "OK";
                
                // Update direction options
                if (DirectionOptions != null)
                {
                    DirectionOptions[0].Display = "Forward";
                    DirectionOptions[1].Display = "Reverse";
                }
                
                // Update homing direction options
                if (HomingDirOptions != null)
                {
                    HomingDirOptions[0].Display = "Forward";
                    HomingDirOptions[1].Display = "Reverse";
                }
            }

            // Notify property changes
            RaisePropertyChanged(nameof(DirectionOptions));
            RaisePropertyChanged(nameof(HomingDirOptions));
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            UpdateUITexts();
        }

        #region Command Implementations

        private void ExecuteAddAxis()
        {
            try
            {
                int nextAxisID = AxisParameters.Count > 0 ? AxisParameters.Max(a => a.AxisID) + 1 : 0;
                var newAxis = new AxisConfig
                {
                    AxisID = nextAxisID,
                    IsUsing = false,
                    MaxPos = 700.0f,
                    MinPos = -11.0f,
                    HomingDir = HomingDir.Positive,
                    AxisSpeed = new AxisSpeed { SpeedName = "HighSpd", Jerk = 500000, MaxSpeed = 1000, MinSpeed = 800, Acc = 6500, Dec = 6500 }
                };
                AxisParameters.Add(newAxis);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisAdded, nextAxisID);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisAddFailed, ex.Message);
            }
        }

        private bool CanExecuteRemoveAxis()
        {
            return SelectedAxis != null && AxisParameters.Count > 0;
        }

        private void ExecuteRemoveAxis()
        {
            try
            {
                if (SelectedAxis != null && AxisParameters.Contains(SelectedAxis))
                {
                    int axisNum = SelectedAxis.AxisID;
                    AxisParameters.Remove(SelectedAxis);
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisRemoved, axisNum);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisRemoveFailed, ex.Message);
            }
            finally
            {
                RemoveAxisCommand.RaiseCanExecuteChanged();
            }
        }


        private void ExecuteOK()
        {
            // Save configuration and close
            if (SaveToJsonFile())
            {
                CloseWindow();
            }
            else
            {
                MessageBox.Show("保存轴参数配置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool SaveToJsonFile()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                
                // Create directory if it doesn't exist
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                
                string configPath = Path.Combine(configDir, "axis_config.json");
                
                var config = new AxisConfigManager
                {
                    AxisConfigs = AxisParameters.ToList(),
                    SaveTime = DateTime.Now
                };
                
                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, jsonContent);
                
                System.Diagnostics.Debug.WriteLine($"轴参数配置已保存到: {configPath}");
                _uiLogger.Info(() => Ewan.Resources.LogMessages.AxisConfigurationSaved, configPath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存轴参数配置失败: {ex.Message}");
                _uiLogger.Error(() => Ewan.Resources.LogMessages.AxisConfigurationSaveFailed, ex.Message);
                return false;
            }
        }

        private void CloseWindow()
        {
            // Find and close the window
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }


        #endregion
    }

    /// <summary>
    /// 回零方向选择项
    /// </summary>
    public class HomingDirOption
    {
        public string Display { get; set; }
        public HomingDir Value { get; set; }
    }
}