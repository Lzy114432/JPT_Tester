using Ewan.BusinessBonding;
using Ewan.Core.IO;
using Ewan.Core.Logger;
using Ewan.Core.Msg;
using Ewan.Model.IO;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MarkingMachineFeeder.Viewmodel
{
    public class IOControlViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger();
        private readonly MsgManager _msgManager = MsgManager.Instance();
        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
        private DispatcherTimer _clockTimer;
        private IOStatus _realIO;
        private MsgListener _ioUpdateListener;
        
        // 动态IO数量和页数配置
        private int _actualInputCount;
        private int _actualOutputCount;
        private int _inputPageCount;
        private int _outputPageCount;
        private const int POINTS_PER_PAGE = 16; // 每页16个点（2列，每列8个）

        #region Properties

        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        private string _edgeDetectionText;
        public string EdgeDetectionText
        {
            get => _edgeDetectionText;
            set => SetProperty(ref _edgeDetectionText, value);
        }

        private string _simulateInputText;
        public string SimulateInputText
        {
            get => _simulateInputText;
            set => SetProperty(ref _simulateInputText, value);
        }

        private string _mappingConfigText;
        public string MappingConfigText
        {
            get => _mappingConfigText;
            set => SetProperty(ref _mappingConfigText, value);
        }

        private string _outputTestText;
        public string OutputTestText
        {
            get => _outputTestText;
            set => SetProperty(ref _outputTestText, value);
        }

        private string _digitalInputText;
        public string DigitalInputText
        {
            get => _digitalInputText;
            set => SetProperty(ref _digitalInputText, value);
        }

        private string _digitalOutputText;
        public string DigitalOutputText
        {
            get => _digitalOutputText;
            set => SetProperty(ref _digitalOutputText, value);
        }

        private string _page1Text;
        public string Page1Text
        {
            get => _page1Text;
            set => SetProperty(ref _page1Text, value);
        }

        private string _page2Text;
        public string Page2Text
        {
            get => _page2Text;
            set => SetProperty(ref _page2Text, value);
        }

        private string _page3Text;
        public string Page3Text
        {
            get => _page3Text;
            set => SetProperty(ref _page3Text, value);
        }

        private string _page4Text;
        public string Page4Text
        {
            get => _page4Text;
            set => SetProperty(ref _page4Text, value);
        }

        private string _inputPointsDisplayText;
        public string InputPointsDisplayText
        {
            get => _inputPointsDisplayText;
            set => SetProperty(ref _inputPointsDisplayText, value);
        }

        private string _outputPointsDisplayText;
        public string OutputPointsDisplayText
        {
            get => _outputPointsDisplayText;
            set => SetProperty(ref _outputPointsDisplayText, value);
        }

        private string _connectionStatusText;
        public string ConnectionStatusText
        {
            get => _connectionStatusText;
            set => SetProperty(ref _connectionStatusText, value);
        }

        private string _readyText;
        public string ReadyText
        {
            get => _readyText;
            set => SetProperty(ref _readyText, value);
        }

        private ObservableCollection<string> _inputPages;
        public ObservableCollection<string> InputPages
        {
            get => _inputPages;
            set => SetProperty(ref _inputPages, value);
        }

        private ObservableCollection<string> _outputPages;
        public ObservableCollection<string> OutputPages
        {
            get => _outputPages;
            set => SetProperty(ref _outputPages, value);
        }

        private string _currentTime;
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    UpdateConnectionStatus();
                }
            }
        }

        // IO点位显示集合
        private ObservableCollection<IOPointViewModel> _inputPoints;
        public ObservableCollection<IOPointViewModel> InputPoints
        {
            get => _inputPoints;
            set => SetProperty(ref _inputPoints, value);
        }

        private ObservableCollection<IOPointViewModel> _outputPoints;
        public ObservableCollection<IOPointViewModel> OutputPoints
        {
            get => _outputPoints;
            set => SetProperty(ref _outputPoints, value);
        }

        // 两列显示的IO点位
        private ObservableCollection<IOPointViewModel> _inputPointsColumn1;
        public ObservableCollection<IOPointViewModel> InputPointsColumn1
        {
            get => _inputPointsColumn1;
            set => SetProperty(ref _inputPointsColumn1, value);
        }

        private ObservableCollection<IOPointViewModel> _inputPointsColumn2;
        public ObservableCollection<IOPointViewModel> InputPointsColumn2
        {
            get => _inputPointsColumn2;
            set => SetProperty(ref _inputPointsColumn2, value);
        }

        private ObservableCollection<IOPointViewModel> _outputPointsColumn1;
        public ObservableCollection<IOPointViewModel> OutputPointsColumn1
        {
            get => _outputPointsColumn1;
            set => SetProperty(ref _outputPointsColumn1, value);
        }

        private ObservableCollection<IOPointViewModel> _outputPointsColumn2;
        public ObservableCollection<IOPointViewModel> OutputPointsColumn2
        {
            get => _outputPointsColumn2;
            set => SetProperty(ref _outputPointsColumn2, value);
        }

        private int _currentInputPage = 0;
        public int CurrentInputPage
        {
            get => _currentInputPage;
            set
            {
                if (SetProperty(ref _currentInputPage, value))
                {
                    UpdateInputPageDisplay();
                }
            }
        }

        private int _currentOutputPage = 0;
        public int CurrentOutputPage
        {
            get => _currentOutputPage;
            set
            {
                if (SetProperty(ref _currentOutputPage, value))
                {
                    UpdateOutputPageDisplay();
                }
            }
        }

        private bool _isMappingMode = true; // 默认显示映射值
        public bool IsMappingMode
        {
            get => _isMappingMode;
            set
            {
                if (SetProperty(ref _isMappingMode, value))
                {
                    // 更新按钮文本和颜色
                    UpdateMappingButtonDisplay();
                    // 映射模式已经存储在IsMappingMode属性中
                    // WriteOutput方法会使用这个值
                }
            }
        }

        // 按钮背景颜色属性
        private string _mappingButtonBackground = "#32CD32"; // 默认绿色(AccentColor)
        public string MappingButtonBackground
        {
            get => _mappingButtonBackground;
            set => SetProperty(ref _mappingButtonBackground, value);
        }

        // 输出测试模式
        private bool _isOutputTestMode = false;
        public bool IsOutputTestMode
        {
            get => _isOutputTestMode;
            set
            {
                if (SetProperty(ref _isOutputTestMode, value))
                {
                    UpdateOutputTestButtonDisplay();
                    UpdateOutputPointsClickability();
                }
            }
        }

        // 模拟输入模式
        private bool _isSimulateInputMode = false;
        public bool IsSimulateInputMode
        {
            get => _isSimulateInputMode;
            set
            {
                if (SetProperty(ref _isSimulateInputMode, value))
                {
                    UpdateSimulateInputButtonDisplay();
                    UpdateInputPointsClickability();
                }
            }
        }

        // 模拟输入按钮背景颜色
        private string _simulateInputButtonBackground = "#4682B4"; // 默认钢蓝色
        public string SimulateInputButtonBackground
        {
            get => _simulateInputButtonBackground;
            set => SetProperty(ref _simulateInputButtonBackground, value);
        }

        // 输出测试按钮背景颜色
        private string _outputTestButtonBackground = "#FF4500"; // 默认橙红色
        public string OutputTestButtonBackground
        {
            get => _outputTestButtonBackground;
            set => SetProperty(ref _outputTestButtonBackground, value);
        }

        #endregion

        #region Commands

        public DelegateCommand EdgeDetectionCommand { get; }
        public DelegateCommand SimulateInputCommand { get; }
        public DelegateCommand MappingConfigCommand { get; }
        public DelegateCommand OutputTestCommand { get; }
        public DelegateCommand MinimizeCommand { get; }
        public DelegateCommand CloseCommand { get; }
        
        // Page navigation commands
        public DelegateCommand InputPageUpCommand { get; }
        public DelegateCommand InputPageDownCommand { get; }
        public DelegateCommand OutputPageUpCommand { get; }
        public DelegateCommand OutputPageDownCommand { get; }
        
        // IO point click commands
        public DelegateCommand<IOPointViewModel> OutputPointClickCommand { get; }
        public DelegateCommand<IOPointViewModel> InputPointClickCommand { get; }

        #endregion

        public IOControlViewModel()
        {
            // Initialize commands
            EdgeDetectionCommand = new DelegateCommand(ExecuteEdgeDetection);
            SimulateInputCommand = new DelegateCommand(ExecuteSimulateInput);
            MappingConfigCommand = new DelegateCommand(ExecuteMappingConfig);
            OutputTestCommand = new DelegateCommand(ExecuteOutputTest);
            MinimizeCommand = new DelegateCommand(ExecuteMinimize);
            CloseCommand = new DelegateCommand(ExecuteClose);
            
            // Initialize page navigation commands
            InputPageUpCommand = new DelegateCommand(ExecuteInputPageUp);
            InputPageDownCommand = new DelegateCommand(ExecuteInputPageDown);
            OutputPageUpCommand = new DelegateCommand(ExecuteOutputPageUp);
            OutputPageDownCommand = new DelegateCommand(ExecuteOutputPageDown);
            
            // Initialize IO point click commands
            OutputPointClickCommand = new DelegateCommand<IOPointViewModel>(ExecuteOutputPointClick, CanExecuteOutputPointClick);
            InputPointClickCommand = new DelegateCommand<IOPointViewModel>(ExecuteInputPointClick, CanExecuteInputPointClick);

            // Initialize mapping mode (默认映射模式)
            IsMappingMode = true;
            UpdateMappingButtonDisplay();

            // Initialize IO points collections
            InputPoints = new ObservableCollection<IOPointViewModel>();
            OutputPoints = new ObservableCollection<IOPointViewModel>();
            InputPointsColumn1 = new ObservableCollection<IOPointViewModel>();
            InputPointsColumn2 = new ObservableCollection<IOPointViewModel>();
            OutputPointsColumn1 = new ObservableCollection<IOPointViewModel>();
            OutputPointsColumn2 = new ObservableCollection<IOPointViewModel>();
            
            // 获取实际IO数量并计算页数
            InitializeIOConfiguration();
            InitializeIOPoints();

            // Subscribe to messages
            _ioUpdateListener = new MsgListener(MsgSubject.IOUpdate, OnIOUpdateMessage);
            _msgManager.RegisterListener(_ioUpdateListener);

            // Initialize timer
            InitializeTimer();

            UpdateUITexts();

            // Set initial connection status
            IsConnected = false;

            // Set design-time default values
            SetDesignTimeDefaults();

            // 主动从 LayeredIOManager 获取初始状态，避免依赖消息延迟
            InitializeFromIOManager();
        }
        
        private void SetDesignTimeDefaults()
        {
            // Set default values for design-time display
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                WindowTitle = "IO 控制面板";
                EdgeDetectionText = "📊 边沿检测";
                SimulateInputText = "🔧 模拟输入";
                MappingConfigText = "🔄 映射";
                OutputTestText = "⚡ 输出测试";
                DigitalInputText = "数字输入";
                DigitalOutputText = "数字输出";
                ConnectionStatusText = "未连接";
                CurrentTime = "2025-08-29 14:20:00";
                InputPointsDisplayText = "输入点显示区域";
                OutputPointsDisplayText = "输出点显示区域";
                
                // 设计时默认页面集合（使用默认的4页）
                InputPages = new ObservableCollection<string> 
                { 
                    "1",
                    "2",
                    "3",
                    "4"
                };
                
                OutputPages = new ObservableCollection<string> 
                { 
                    "1",
                    "2",
                    "3",
                    "4"
                };
            }
        }

        private void InitializeTimer()
        {
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += ClockTimer_Tick;
            _clockTimer.Start();
            UpdateClock();
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void UpdateUITexts()
        {
            WindowTitle = "IO 控制面板";
            EdgeDetectionText = "边沿检测";
            SimulateInputText = "模拟输入";
            MappingConfigText = "映射模式";
            OutputTestText = "输出测试";
            DigitalInputText = "数字输入";
            DigitalOutputText = "数字输出";
            Page1Text = "第 1 页 (0-15)";
            Page2Text = "第 2 页 (16-31)";
            Page3Text = "第 3 页 (32-47)";
            Page4Text = "第 4 页 (48-63)";
            InputPointsDisplayText = "输入点显示区域";
            OutputPointsDisplayText = "输出点显示区域";
            ReadyText = "就绪";
            
            // Update page collections - 使用动态页数
            InputPages = new ObservableCollection<string>();
            for (int i = 1; i <= _inputPageCount; i++)
            {
                InputPages.Add(i.ToString());
            }
            
            OutputPages = new ObservableCollection<string>();
            for (int i = 1; i <= _outputPageCount; i++)
            {
                OutputPages.Add(i.ToString());
            }
            
            // Update connection status
            UpdateConnectionStatus();
            
            // Update button displays to reflect current language
            UpdateMappingButtonDisplay();
            UpdateOutputTestButtonDisplay();
            UpdateSimulateInputButtonDisplay();

            // Notify all properties changed
            RaisePropertyChanged(nameof(WindowTitle));
            RaisePropertyChanged(nameof(EdgeDetectionText));
            RaisePropertyChanged(nameof(SimulateInputText));
            RaisePropertyChanged(nameof(MappingConfigText));
            RaisePropertyChanged(nameof(OutputTestText));
            RaisePropertyChanged(nameof(DigitalInputText));
            RaisePropertyChanged(nameof(DigitalOutputText));
            RaisePropertyChanged(nameof(Page1Text));
            RaisePropertyChanged(nameof(Page2Text));
            RaisePropertyChanged(nameof(Page3Text));
            RaisePropertyChanged(nameof(Page4Text));
            RaisePropertyChanged(nameof(InputPointsDisplayText));
            RaisePropertyChanged(nameof(OutputPointsDisplayText));
            RaisePropertyChanged(nameof(ReadyText));
            RaisePropertyChanged(nameof(InputPages));
            RaisePropertyChanged(nameof(OutputPages));
        }

        private void UpdateConnectionStatus()
        {
            ConnectionStatusText = IsConnected 
                ? "已连接"
                : "未连接";
            RaisePropertyChanged(nameof(ConnectionStatusText));
        }

        #region Command Implementations

        private void ExecuteEdgeDetection()
        {
            MessageBox.Show(
                "边沿检测功能正在开发中...",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteSimulateInput()
        {
            // 切换模拟输入模式
            IsSimulateInputMode = !IsSimulateInputMode;
            
            // 显示提示信息
            string modeText = IsSimulateInputMode ? 
                "模拟输入模式已开启" : 
                "模拟输入模式已关闭";
            
            _uiLogger.Info("IO模拟模式已更改: {0}", modeText);
            
            if (IsSimulateInputMode)
            {
                // 开启模拟模式时，将所有输入点设置为灰色（模拟状态为0，但启用了模拟模式）
                for (int i = 0; i < _actualInputCount; i++)
                {
                    InputPoints[i].IsInSimulateMode = true;  // 标记为模拟模式
                }
            }
            else
            {
                // 关闭模拟模式，清除所有模拟状态
                IOController.Instance().ClearAllSimulations();
                // 更新显示
                if (_realIO != null)
                {
                    for (int i = 0; i < _actualInputCount; i++)
                    {
                        _realIO.XSimulateMode[i] = 0;
                        InputPoints[i].SimulateMode = 0;
                        InputPoints[i].IsInSimulateMode = false;  // 取消模拟模式标记
                    }
                }
            }
            
            // 更新当前页显示
            UpdateInputPageDisplay();
        }

        private void UpdateSimulateInputButtonDisplay()
        {
            // 根据模式更新按钮文本和颜色
            if (IsSimulateInputMode)
            {
                SimulateInputText = "模拟中";
                SimulateInputButtonBackground = "#00FF00"; // 亮绿色
            }
            else
            {
                SimulateInputText = "模拟输入";
                SimulateInputButtonBackground = "#4682B4"; // 钢蓝色
            }
        }

        private void UpdateInputPointsClickability()
        {
            // 通知所有输入点更新可点击状态
            foreach (var point in InputPoints)
            {
                point.IsClickable = IsSimulateInputMode;
            }
            
            // 刷新输入点击命令状态
            InputPointClickCommand?.RaiseCanExecuteChanged();
        }

        private bool CanExecuteInputPointClick(IOPointViewModel point)
        {
            // 只有在模拟输入模式下才能点击输入点
            return IsSimulateInputMode && point != null;
        }

        private void ExecuteInputPointClick(IOPointViewModel point)
        {
            if (point == null || !IsSimulateInputMode)
                return;

            try
            {
                // 循环切换模拟状态: None(0) -> ForceOn(1) -> ForceOff(2) -> None(0)
                int newMode = (point.SimulateMode + 1) % 3;
                
                // 使用IOController设置模拟状态
                IOController.Instance().SetInputSimulate(point.Index, newMode, IsMappingMode);
                
                // 更新视图模型
                point.SimulateMode = newMode;
                
                // 更新IOStatus中的模拟状态
                if (_realIO != null)
                {
                    _realIO.XSimulateMode[point.Index] = newMode;
                }
                
                string modeName;
                switch (newMode)
                {
                    case 1:
                        modeName = "ForceOn";
                        break;
                    case 2:
                        modeName = "ForceOff";
                        break;
                    default:
                        modeName = "None";
                        break;
                }
                
                _uiLogger.Info("IO模拟模式设置: {0} = {1}", point.Name, modeName);
            }
            catch (Exception ex)
            {
                _uiLogger.Error("IO模拟错误: {0} - {1}", point.Name, ex.Message);
            }
        }

        private void ExecuteMappingConfig()
        {
            // 切换映射模式
            IsMappingMode = !IsMappingMode;
            
            // 显示提示信息
            string modeText = IsMappingMode ? 
                "映射模式" : 
                "真实模式";
            
            _uiLogger.Info("IO映射模式已切换至: {0}", modeText);
            
            // 切换后重新更新显示
            if (_realIO != null)
            {
                UpdateAllIOPoints();
            }
        }

        private void ExecuteOutputTest()
        {
            // 切换输出测试模式
            IsOutputTestMode = !IsOutputTestMode;
            
            // 显示提示信息
            string modeText = IsOutputTestMode ? 
                "输出测试模式已开启" : 
                "输出测试模式已关闭";
            
            _uiLogger.Info("IO测试模式: {0}", modeText);
        }

        private void UpdateOutputTestButtonDisplay()
        {
            // 根据模式更新按钮文本和颜色
            if (IsOutputTestMode)
            {
                OutputTestText = "测试中";
                OutputTestButtonBackground = "#00FF00"; // 亮绿色
            }
            else
            {
                OutputTestText = "输出测试";
                OutputTestButtonBackground = "#FF4500"; // 橙红色
            }
        }

        private void UpdateOutputPointsClickability()
        {
            // 通知所有输出点更新可点击状态
            foreach (var point in OutputPoints)
            {
                point.IsClickable = IsOutputTestMode;
            }
            
            // 刷新输出点击命令状态
            OutputPointClickCommand?.RaiseCanExecuteChanged();
        }

        private bool CanExecuteOutputPointClick(IOPointViewModel point)
        {
            // 只有在输出测试模式下才能点击输出点
            return IsOutputTestMode && point != null;
        }

        private void ExecuteOutputPointClick(IOPointViewModel point)
        {
            if (point == null || !IsOutputTestMode)
                return;

            try
            {
                // 切换输出点状态
                bool newValue = !point.IsOn;

                // 使用IOController写入输出
                IOController.Instance().WriteOutput(point.Index, newValue, IsMappingMode);

                _uiLogger.Info("输出 {0} 设置为: {1}", point.Name, newValue ? "ON" : "OFF");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("输出控制错误: {0}", ex.Message);
            }
        }

        private void ExecuteMinimize()
        {
            Application.Current.Windows[Application.Current.Windows.Count - 1].WindowState = WindowState.Minimized;
        }

        private void ExecuteClose()
        {
            _clockTimer?.Stop();
            Application.Current.Windows[Application.Current.Windows.Count - 1].Close();
        }
        
        private void ExecuteInputPageUp()
        {
            if (CurrentInputPage > 0)
            {
                CurrentInputPage--;
            }
        }
        
        private void ExecuteInputPageDown()
        {
            // 使用动态页数，页数从0开始计数
            if (CurrentInputPage < _inputPageCount - 1)
            {
                CurrentInputPage++;
            }
        }
        
        private void ExecuteOutputPageUp()
        {
            if (CurrentOutputPage > 0)
            {
                CurrentOutputPage--;
            }
        }
        
        private void ExecuteOutputPageDown()
        {
            // 使用动态页数，页数从0开始计数
            if (CurrentOutputPage < _outputPageCount - 1)
            {
                CurrentOutputPage++;
            }
        }
        
        private void UpdateMappingButtonDisplay()
        {
            // 根据模式更新按钮文本和颜色
            if (IsMappingMode)
            {
                MappingConfigText = "映射模式";
                MappingButtonBackground = "#32CD32"; // 绿色 (AccentColor)
            }
            else
            {
                MappingConfigText = "真实模式";
                MappingButtonBackground = "#4682B4"; // 蓝色 (PrimaryColor)
            }
        }

        #endregion

        public void Cleanup()
        {
            _clockTimer?.Stop();
            _msgManager.UnRegisterListener(_ioUpdateListener);
        }

        #region IO Points Management

        /// <summary>
        /// 初始化IO配置（获取实际IO数量并计算页数）
        /// </summary>
        private void InitializeIOConfiguration()
        {
            try
            {
                // 获取实际IO数量
                _actualInputCount = _ioManager.InputCount;
                _actualOutputCount = _ioManager.OutputCount;
                
                // 计算页数（每页16个点）
                _inputPageCount = Math.Max(1, (int)Math.Ceiling((double)_actualInputCount / POINTS_PER_PAGE));
                _outputPageCount = Math.Max(1, (int)Math.Ceiling((double)_actualOutputCount / POINTS_PER_PAGE));
            }
            catch (Exception ex)
            {
                // 如果获取失败，使用默认值
                _actualInputCount = 64;
                _actualOutputCount = 64;
                _inputPageCount = 4;
                _outputPageCount = 4;
                
                _uiLogger.Warn("获取实际IO数量失败，使用默认配置: {0}", ex.Message);
            }
        }

        private void InitializeIOPoints()
        {
            // 根据实际IO数量初始化输入点和输出点的视图模型
            for (int i = 0; i < _actualInputCount; i++)
            {
                InputPoints.Add(new IOPointViewModel { Index = i, Name = $"X{i}", IsOn = false });
            }
            
            for (int i = 0; i < _actualOutputCount; i++)
            {
                OutputPoints.Add(new IOPointViewModel { Index = i, Name = $"Y{i}", IsOn = false });
            }
            
            // 初始化第一页显示
            UpdateInputPageDisplay();
            UpdateOutputPageDisplay();
        }

        private void UpdateInputPageDisplay()
        {
            InputPointsColumn1.Clear();
            InputPointsColumn2.Clear();
            
            // 每页显示16个点（两列，每列8个）
            int startIndex = CurrentInputPage * POINTS_PER_PAGE;
            
            // 左列: X0-X7 (或对应页的前8个)
            for (int i = 0; i < 8 && startIndex + i < _actualInputCount; i++)
            {
                InputPointsColumn1.Add(InputPoints[startIndex + i]);
            }
            
            // 右列: X8-X15 (或对应页的后8个)
            for (int i = 8; i < 16 && startIndex + i < _actualInputCount; i++)
            {
                InputPointsColumn2.Add(InputPoints[startIndex + i]);
            }
        }

        private void UpdateOutputPageDisplay()
        {
            OutputPointsColumn1.Clear();
            OutputPointsColumn2.Clear();
            
            // 每页显示16个点（两列，每列8个）
            int startIndex = CurrentOutputPage * POINTS_PER_PAGE;
            
            // 左列: Y0-Y7 (或对应页的前8个)
            for (int i = 0; i < 8 && startIndex + i < _actualOutputCount; i++)
            {
                OutputPointsColumn1.Add(OutputPoints[startIndex + i]);
            }
            
            // 右列: Y8-Y15 (或对应页的后8个)
            for (int i = 8; i < 16 && startIndex + i < _actualOutputCount; i++)
            {
                OutputPointsColumn2.Add(OutputPoints[startIndex + i]);
            }
        }

        private void UpdateAllIOPoints()
        {
            if (_realIO == null) return;

            // 根据模式选择数据源
            if (IsMappingMode)
            {
                // 映射模式：使用映射数据
                for (int i = 0; i < _actualInputCount; i++)
                {
                    InputPoints[i].IsOn = _realIO.XMapped[i];
                    InputPoints[i].Name = _realIO.XMappedNames[i];
                    InputPoints[i].SimulateMode = _realIO.XSimulateMode[i]; // 同步模拟状态
                    // 保持IsInSimulateMode状态不变，因为这是由按钮控制的
                }

                for (int i = 0; i < _actualOutputCount; i++)
                {
                    OutputPoints[i].IsOn = _realIO.YMapped[i];
                    OutputPoints[i].Name = _realIO.YMappedNames[i];
                }
            }
            else
            {
                // 真实模式：使用真实数据
                for (int i = 0; i < _actualInputCount; i++)
                {
                    InputPoints[i].IsOn = _realIO.XReal[i];
                    InputPoints[i].Name = _realIO.XRealNames[i];
                    InputPoints[i].SimulateMode = _realIO.XSimulateMode[i]; // 同步模拟状态
                    // 保持IsInSimulateMode状态不变，因为这是由按钮控制的
                }

                for (int i = 0; i < _actualOutputCount; i++)
                {
                    OutputPoints[i].IsOn = _realIO.YReal[i];
                    OutputPoints[i].Name = _realIO.YRealNames[i];
                }
            }

            // 更新连接状态
            IsConnected = _realIO.IsConnected;
            
            // 更新当前页显示
            UpdateInputPageDisplay();
            UpdateOutputPageDisplay();
        }

        #endregion

        #region Active Initialization

        /// <summary>
        /// 从配置文件主动加载映射名称进行初始化
        /// </summary>
        private void InitializeFromIOManager()
        {
            try
            {
                // 创建临时 IOStatus 对象并填充映射名称
                var initialStatus = new IOStatus();

                // 从配置文件读取映射名称
                LoadMappingNamesFromConfig(initialStatus);

                // 使用加载的映射名称初始化界面
                _realIO = initialStatus;
                UpdateAllIOPoints();
            }
            catch (Exception ex)
            {
                _uiLogger.Warn("模块初始化失败: {0} - {1}", "IOControlViewModel: 映射名称初始化失败，将等待消息更新", ex.Message);
                // 失败不影响，仍然可以依赖后续的消息更新
            }
        }

        /// <summary>
        /// 从配置文件加载映射名称
        /// </summary>
        private void LoadMappingNamesFromConfig(IOStatus status)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Config", "io_mapping.json");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    dynamic config = JsonConvert.DeserializeObject(json);

                    // 加载输入映射名称
                    if (config?.InputMappings != null)
                    {
                        foreach (var mapping in config.InputMappings)
                        {
                            int logicalIndex = (int)mapping.LogicalIndex;
                            string name = (string)mapping.Name;
                            if (logicalIndex >= 0 && logicalIndex < IOStatus.IO_COUNT)
                            {
                                status.XMappedNames[logicalIndex] = name;
                            }
                        }
                    }

                    // 加载输出映射名称
                    if (config?.OutputMappings != null)
                    {
                        foreach (var mapping in config.OutputMappings)
                        {
                            int logicalIndex = (int)mapping.LogicalIndex;
                            string name = (string)mapping.Name;
                            if (logicalIndex >= 0 && logicalIndex < IOStatus.IO_COUNT)
                            {
                                status.YMappedNames[logicalIndex] = name;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Warn("加载映射名称失败: {0}", ex.Message);
            }
        }

        #endregion

        #region Message Handling

        private void OnIOUpdateMessage(MessageModel message)
        {
            if (message.Subject == MsgSubject.IOUpdate && message.Data is IOStatus realIO)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _realIO = realIO;
                    UpdateAllIOPoints();
                }));
            }
        }

        #endregion
    }

    /// <summary>
    /// IO点位视图模型
    /// </summary>
    public class IOPointViewModel : BindableBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (SetProperty(ref _isOn, value))
                {
                    RaisePropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        private bool _isClickable;
        public bool IsClickable
        {
            get => _isClickable;
            set => SetProperty(ref _isClickable, value);
        }

        private int _simulateMode;
        /// <summary>
        /// 模拟模式 (0=None灰色, 1=ForceOn绿色, 2=ForceOff红色)
        /// </summary>
        public int SimulateMode
        {
            get => _simulateMode;
            set
            {
                if (SetProperty(ref _simulateMode, value))
                {
                    RaisePropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        private bool _isInSimulateMode;
        /// <summary>
        /// 是否处于模拟模式（用于控制所有点显示为灰色）
        /// </summary>
        public bool IsInSimulateMode
        {
            get => _isInSimulateMode;
            set
            {
                if (SetProperty(ref _isInSimulateMode, value))
                {
                    RaisePropertyChanged(nameof(BackgroundColor));
                }
            }
        }

        /// <summary>
        /// 根据模拟状态返回背景颜色
        /// </summary>
        public string BackgroundColor
        {
            get
            {
                // 如果处于模拟模式
                if (IsInSimulateMode)
                {
                    // 根据具体的模拟状态显示颜色
                    switch (SimulateMode)
                    {
                        case 0: // None - 显示灰色
                            return "#808080";
                        case 1: // ForceOn - 显示亮绿色
                            return "#00FF00";
                        case 2: // ForceOff - 显示亮红色
                            return "#FF0000";
                        default:
                            return "#808080";
                    }
                }
                
                // 正常模式，根据实际状态显示
                return IsOn ? "#00FF00" : "#DC143C"; // 绿色/暗红色
            }
        }
    }
}
