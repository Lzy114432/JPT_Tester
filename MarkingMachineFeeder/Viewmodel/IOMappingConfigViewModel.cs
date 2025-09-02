using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using MarkingMachineFeeder.Model;
using Ewan.Core.Culture;
using Newtonsoft.Json;

namespace MarkingMachineFeeder.Viewmodel
{
    public class IOMappingConfigViewModel : BindableBase
    {
        private readonly CultureManager _cultureManager = CultureManager.Instance();
        
        #region Properties
        
        private ObservableCollection<IOMapping> _inputMappings;
        public ObservableCollection<IOMapping> InputMappings
        {
            get => _inputMappings;
            set => SetProperty(ref _inputMappings, value);
        }

        private ObservableCollection<IOMapping> _outputMappings;
        public ObservableCollection<IOMapping> OutputMappings
        {
            get => _outputMappings;
            set => SetProperty(ref _outputMappings, value);
        }

        private ObservableCollection<StatusOption> _statusOptions;
        public ObservableCollection<StatusOption> StatusOptions
        {
            get => _statusOptions;
            set => SetProperty(ref _statusOptions, value);
        }

        #region UI Texts
        
        private string _windowTitle = "IO映射配置";
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        private string _mappingSettingsText = "映射设置";
        public string MappingSettingsText
        {
            get => _mappingSettingsText;
            set => SetProperty(ref _mappingSettingsText, value);
        }

        private string _inputTabText = "输入";
        public string InputTabText
        {
            get => _inputTabText;
            set => SetProperty(ref _inputTabText, value);
        }

        private string _outputTabText = "输出";
        public string OutputTabText
        {
            get => _outputTabText;
            set => SetProperty(ref _outputTabText, value);
        }

        private string _indexHeaderText = "序号";
        public string IndexHeaderText
        {
            get => _indexHeaderText;
            set => SetProperty(ref _indexHeaderText, value);
        }

        private string _nameHeaderText = "名称";
        public string NameHeaderText
        {
            get => _nameHeaderText;
            set => SetProperty(ref _nameHeaderText, value);
        }

        private string _mappingHeaderText = "映射";
        public string MappingHeaderText
        {
            get => _mappingHeaderText;
            set => SetProperty(ref _mappingHeaderText, value);
        }

        private string _statusHeaderText = "状态";
        public string StatusHeaderText
        {
            get => _statusHeaderText;
            set => SetProperty(ref _statusHeaderText, value);
        }

        private string _inputTabHeaderText = "输入";
        public string InputTabHeaderText
        {
            get => _inputTabHeaderText;
            set => SetProperty(ref _inputTabHeaderText, value);
        }

        private string _outputTabHeaderText = "输出";
        public string OutputTabHeaderText
        {
            get => _outputTabHeaderText;
            set => SetProperty(ref _outputTabHeaderText, value);
        }

        private string _applyText = "应用";
        public string ApplyText
        {
            get => _applyText;
            set => SetProperty(ref _applyText, value);
        }

        private string _okText = "确定";
        public string OKText
        {
            get => _okText;
            set => SetProperty(ref _okText, value);
        }

        private string _cancelText = "取消";
        public string CancelText
        {
            get => _cancelText;
            set => SetProperty(ref _cancelText, value);
        }

        #endregion

        #endregion

        #region Commands
        
        public DelegateCommand ApplyCommand { get; private set; }
        public DelegateCommand OKCommand { get; private set; }
        public DelegateCommand CancelCommand { get; private set; }

        #endregion

        public IOMappingConfigViewModel()
        {
            // Debug output
            System.Diagnostics.Debug.WriteLine("IOMappingConfigViewModel Constructor Called");
            
            InitializeCommands();
            InitializeData();
            
            // Check if in design mode
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                // Set design-time default values
                InitializeDesignTimeData();
            }
            else
            {
                UpdateUITexts();
                
                // Subscribe to culture change
                _cultureManager.CultureChanged += OnCultureChanged;
            }
            
            // Force property change notifications for all header texts
            RaisePropertyChanged(nameof(IndexHeaderText));
            RaisePropertyChanged(nameof(NameHeaderText));
            RaisePropertyChanged(nameof(MappingHeaderText));
            RaisePropertyChanged(nameof(StatusHeaderText));
            
            // Debug output to verify values
            System.Diagnostics.Debug.WriteLine($"IndexHeaderText: {IndexHeaderText}");
            System.Diagnostics.Debug.WriteLine($"NameHeaderText: {NameHeaderText}");
            System.Diagnostics.Debug.WriteLine($"MappingHeaderText: {MappingHeaderText}");
            System.Diagnostics.Debug.WriteLine($"StatusHeaderText: {StatusHeaderText}");
        }
        
        private void InitializeDesignTimeData()
        {
            // Set design-time text values
            WindowTitle = "IO映射配置";
            MappingSettingsText = "映射设置";
            InputTabText = "输入";
            OutputTabText = "输出";
            IndexHeaderText = "序号";
            NameHeaderText = "名称";
            MappingHeaderText = "映射";
            StatusHeaderText = "状态";
            ApplyText = "应用";
            OKText = "确定";
            CancelText = "取消";
            
            // Update status options for design time
            if (StatusOptions != null && StatusOptions.Count >= 2)
            {
                StatusOptions[0].Display = "常开";
                StatusOptions[1].Display = "常闭";
            }
            
            // Add some sample data for design time
            if (InputMappings != null && InputMappings.Count > 0)
            {
                // Show first few items with sample data
                for (int i = 0; i < System.Math.Min(5, InputMappings.Count); i++)
                {
                    InputMappings[i].Name = GetDefaultInputName(i);
                    InputMappings[i].LogicalIndex = i;
                    InputMappings[i].PhysicalIndex = i;
                    InputMappings[i].IsNormallyOpen = i % 2 == 0;
                }
            }
            
            if (OutputMappings != null && OutputMappings.Count > 0)
            {
                // Show first few items with sample data
                for (int i = 0; i < System.Math.Min(5, OutputMappings.Count); i++)
                {
                    OutputMappings[i].Name = GetDefaultOutputName(i);
                    OutputMappings[i].LogicalIndex = i;
                    OutputMappings[i].PhysicalIndex = i;
                    OutputMappings[i].IsNormallyOpen = i % 2 == 1;
                }
            }
        }

        private void InitializeCommands()
        {
            ApplyCommand = new DelegateCommand(ExecuteApply);
            OKCommand = new DelegateCommand(ExecuteOK);
            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        private void InitializeData()
        {
            // Initialize status options
            StatusOptions = new ObservableCollection<StatusOption>
            {
                new StatusOption { Display = "常开", Value = true },
                new StatusOption { Display = "常闭", Value = false }
            };

            // Try to load from JSON file
            if (!LoadFromJsonFile())
            {
                // If loading fails, initialize with default mappings
                InitializeDefaultMappings();
            }
        }
        
        private bool LoadFromJsonFile()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "io_mapping.json");
                
                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"配置文件不存在: {configPath}");
                    return false;
                }
                
                string jsonContent = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<IOMappingConfig>(jsonContent);
                
                if (config == null)
                {
                    System.Diagnostics.Debug.WriteLine("反序列化配置失败");
                    return false;
                }
                
                // Load input mappings
                InputMappings = new ObservableCollection<IOMapping>(config.InputMappings ?? new System.Collections.Generic.List<IOMapping>());
                
                // Load output mappings
                OutputMappings = new ObservableCollection<IOMapping>(config.OutputMappings ?? new System.Collections.Generic.List<IOMapping>());
                
                System.Diagnostics.Debug.WriteLine($"成功加载配置: {InputMappings.Count} 个输入, {OutputMappings.Count} 个输出");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
                return false;
            }
        }
        
        private void InitializeDefaultMappings()
        {
            // Initialize default input mappings
            InputMappings = new ObservableCollection<IOMapping>();
            for (int i = 0; i < 16; i++)
            {
                InputMappings.Add(new IOMapping
                {
                    LogicalIndex = i,
                    Name = GetDefaultInputName(i),
                    PhysicalIndex = i,
                    IsNormallyOpen = true
                });
            }

            // Initialize default output mappings
            OutputMappings = new ObservableCollection<IOMapping>();
            for (int i = 0; i < 16; i++)
            {
                OutputMappings.Add(new IOMapping
                {
                    LogicalIndex = i,
                    Name = GetDefaultOutputName(i),
                    PhysicalIndex = i,
                    IsNormallyOpen = false
                });
            }
        }

        private string GetDefaultInputName(int index)
        {
            string[] defaultNames = 
            {
                "启动按钮",
                "停止按钮",
                "急停按钮",
                "复位按钮",
                "自动/手动切换",
                "料仓1传感器",
                "料仓2传感器",
                "料仓3传感器",
                "料仓4传感器",
                "上相机触发",
                "下相机触发",
                "机械手到位",
                "机械手抓取",
                "小车请求信号",
                "安全门检测",
                "备用输入"
            };

            return index < defaultNames.Length ? defaultNames[index] : $"输入{index}";
        }

        private string GetDefaultOutputName(int index)
        {
            string[] defaultNames = 
            {
                "运行指示灯",
                "停止指示灯",
                "报警指示灯",
                "自动模式灯",
                "手动模式灯",
                "料仓1电磁阀",
                "料仓2电磁阀",
                "料仓3电磁阀",
                "料仓4电磁阀",
                "机械手使能",
                "机械手抓取",
                "机械手放置",
                "传送带启动",
                "传送带停止",
                "蜂鸣器",
                "备用输出"
            };

            return index < defaultNames.Length ? defaultNames[index] : $"输出{index}";
        }

        private void UpdateUITexts()
        {
            // Update UI texts based on current culture
            if (_cultureManager.CurrentCulture.Name == "zh-CN")
            {
                WindowTitle = "IO映射配置";
                MappingSettingsText = "映射设置";
                InputTabText = "输入";
                OutputTabText = "输出";
                IndexHeaderText = "序号";
                NameHeaderText = "名称";
                MappingHeaderText = "映射";
                StatusHeaderText = "状态";
                ApplyText = "应用";
                OKText = "确定";
                CancelText = "取消";
                
                // Update status options
                if (StatusOptions != null)
                {
                    StatusOptions[0].Display = "常开";
                    StatusOptions[1].Display = "常闭";
                }
            }
            else
            {
                WindowTitle = "IO Mapping Configuration";
                MappingSettingsText = "Mapping Settings";
                InputTabText = "Input";
                OutputTabText = "Output";
                IndexHeaderText = "Index";
                NameHeaderText = "Name";
                MappingHeaderText = "Mapping";
                StatusHeaderText = "Status";
                ApplyText = "Apply";
                OKText = "OK";
                CancelText = "Cancel";
                
                // Update status options
                if (StatusOptions != null)
                {
                    StatusOptions[0].Display = "Normally Open";
                    StatusOptions[1].Display = "Normally Closed";
                }
            }

            // Notify property changes
            RaisePropertyChanged(nameof(StatusOptions));
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            UpdateUITexts();
        }

        private void ExecuteApply()
        {
            // Save configuration without closing
            if (SaveToJsonFile())
            {
                MessageBox.Show("配置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("保存配置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("保存配置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                string configPath = Path.Combine(configDir, "io_mapping.json");
                
                var config = new IOMappingConfig
                {
                    HardwareType = "MitsubishiPLC",
                    ConnectionString = "127.0.0.1:6000",
                    InputMappings = InputMappings.ToList(),
                    OutputMappings = OutputMappings.ToList(),
                    SimulatedInputs = new System.Collections.Generic.Dictionary<string, object>(),
                    SaveTime = DateTime.Now
                };
                
                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, jsonContent);
                
                System.Diagnostics.Debug.WriteLine($"配置已保存到: {configPath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
                return false;
            }
        }

        private void ExecuteCancel()
        {
            // Close without saving
            CloseWindow();
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
    }
}