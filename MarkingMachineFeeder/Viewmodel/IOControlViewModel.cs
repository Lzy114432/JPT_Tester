using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Mvvm;
using Ewan.Core.Culture;
using Ewan.Core.Logger;
using Ewan.Core.Msg;
using Ewan.Model.IO;
using Ewan.Model.Messages;
using Ewan.Model.Security;
using System.Collections.Generic;
using System.Linq;

namespace MarkingMachineFeeder.Viewmodel
{
    public class IOControlViewModel : BindableBase
    {
        private readonly CultureManager _cultureManager = CultureManager.Instance();
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly MsgManager _msgManager = MsgManager.Instance();
        private DispatcherTimer _clockTimer;
        private RealIO _realIO;
        private MsgListener _ioUpdateListener;

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

            // Initialize IO points collections
            InputPoints = new ObservableCollection<IOPointViewModel>();
            OutputPoints = new ObservableCollection<IOPointViewModel>();
            InputPointsColumn1 = new ObservableCollection<IOPointViewModel>();
            InputPointsColumn2 = new ObservableCollection<IOPointViewModel>();
            OutputPointsColumn1 = new ObservableCollection<IOPointViewModel>();
            OutputPointsColumn2 = new ObservableCollection<IOPointViewModel>();
            InitializeIOPoints();

            // Subscribe to messages
            _ioUpdateListener = new MsgListener(MsgSubject.IOUpdate, OnIOUpdateMessage);
            _msgManager.RegisterListener(_ioUpdateListener);

            // Subscribe to culture change events
            _cultureManager.CultureChanged += OnCultureChanged;

            // Initialize timer
            InitializeTimer();

            // Initial culture sync
            Ewan.Resources.IOControlStrings.Culture = _cultureManager.CurrentCulture;
            UpdateUITexts();

            // Set initial connection status
            IsConnected = false;
            
            // Set design-time default values
            SetDesignTimeDefaults();
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

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            // Sync resource culture
            Ewan.Resources.IOControlStrings.Culture = e.NewCulture;
            Ewan.Resources.LogMessages.Culture = e.NewCulture;
            
            // Update all UI texts
            UpdateUITexts();
        }

        private void UpdateUITexts()
        {
            WindowTitle = Ewan.Resources.IOControlStrings.WindowTitle;
            EdgeDetectionText = Ewan.Resources.IOControlStrings.EdgeDetection;
            SimulateInputText = Ewan.Resources.IOControlStrings.SimulateInput;
            MappingConfigText = Ewan.Resources.IOControlStrings.MappingConfig;
            OutputTestText = Ewan.Resources.IOControlStrings.OutputTest;
            DigitalInputText = Ewan.Resources.IOControlStrings.DigitalInput;
            DigitalOutputText = Ewan.Resources.IOControlStrings.DigitalOutput;
            Page1Text = Ewan.Resources.IOControlStrings.Page1;
            Page2Text = Ewan.Resources.IOControlStrings.Page2;
            Page3Text = Ewan.Resources.IOControlStrings.Page3;
            Page4Text = Ewan.Resources.IOControlStrings.Page4;
            InputPointsDisplayText = Ewan.Resources.IOControlStrings.InputPointsDisplay;
            OutputPointsDisplayText = Ewan.Resources.IOControlStrings.OutputPointsDisplay;
            ReadyText = Ewan.Resources.IOControlStrings.Ready;
            
            // Update page collections
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
            
            // Update connection status
            UpdateConnectionStatus();

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
                ? Ewan.Resources.IOControlStrings.Connected 
                : Ewan.Resources.IOControlStrings.NotConnected;
            RaisePropertyChanged(nameof(ConnectionStatusText));
        }

        #region Command Implementations

        private void ExecuteEdgeDetection()
        {
            MessageBox.Show(
                Ewan.Resources.IOControlStrings.EdgeDetectionMessage,
                Ewan.Resources.IOControlStrings.MessageBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteSimulateInput()
        {
            MessageBox.Show(
                Ewan.Resources.IOControlStrings.SimulateInputMessage,
                Ewan.Resources.IOControlStrings.MessageBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteMappingConfig()
        {
            MessageBox.Show(
                Ewan.Resources.IOControlStrings.MappingConfigMessage,
                Ewan.Resources.IOControlStrings.MessageBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ExecuteOutputTest()
        {
            MessageBox.Show(
                Ewan.Resources.IOControlStrings.OutputTestMessage,
                Ewan.Resources.IOControlStrings.MessageBoxTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            // 4 pages total (0-3), each page shows 16 points
            if (CurrentInputPage < 3)
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
            // 4 pages total (0-3), each page shows 16 points
            if (CurrentOutputPage < 3)
            {
                CurrentOutputPage++;
            }
        }

        #endregion

        public void Cleanup()
        {
            _clockTimer?.Stop();
            _cultureManager.CultureChanged -= OnCultureChanged;
            _msgManager.UnRegisterListener(_ioUpdateListener);
        }

        #region IO Points Management

        private void InitializeIOPoints()
        {
            // 初始化64个输入点和64个输出点的视图模型
            for (int i = 1; i <= 64; i++)
            {
                InputPoints.Add(new IOPointViewModel { Index = i, Name = $"X{i}", IsOn = false });
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
            int startIndex = CurrentInputPage * 16;
            
            // 左列: X0-X7 (或对应页的前8个)
            for (int i = 0; i < 8 && startIndex + i < 64; i++)
            {
                InputPointsColumn1.Add(InputPoints[startIndex + i]);
            }
            
            // 右列: X8-X15 (或对应页的后8个)
            for (int i = 8; i < 16 && startIndex + i < 64; i++)
            {
                InputPointsColumn2.Add(InputPoints[startIndex + i]);
            }
        }

        private void UpdateOutputPageDisplay()
        {
            OutputPointsColumn1.Clear();
            OutputPointsColumn2.Clear();
            
            // 每页显示16个点（两列，每列8个）
            int startIndex = CurrentOutputPage * 16;
            
            // 左列: Y0-Y7 (或对应页的前8个)
            for (int i = 0; i < 8 && startIndex + i < 64; i++)
            {
                OutputPointsColumn1.Add(OutputPoints[startIndex + i]);
            }
            
            // 右列: Y8-Y15 (或对应页的后8个)
            for (int i = 8; i < 16 && startIndex + i < 64; i++)
            {
                OutputPointsColumn2.Add(OutputPoints[startIndex + i]);
            }
        }

        private void UpdateAllIOPoints()
        {
            if (_realIO == null) return;

            // 更新所有输入点
            for (int i = 0; i < 64; i++)
            {
                InputPoints[i].IsOn = _realIO.X[i];
            }

            // 更新所有输出点
            for (int i = 0; i < 64; i++)
            {
                OutputPoints[i].IsOn = _realIO.Y[i];
            }

            // 更新连接状态
            IsConnected = _realIO.IsConnected;
            
            // 更新当前页显示
            UpdateInputPageDisplay();
            UpdateOutputPageDisplay();
        }

        #endregion

        #region Message Handling

        private void OnIOUpdateMessage(MessageModel message)
        {
            if (message.Subject == MsgSubject.IOUpdate && message.Data is RealIO realIO)
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
            set => SetProperty(ref _isOn, value);
        }
    }
}