using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Mvvm;
using Ewan.Core.Culture;
using Ewan.Core.Logger;
using Ewan.Model.Security;

namespace MarkingMachineFeeder.Viewmodel
{
    public class IOControlViewModel : BindableBase
    {
        private readonly CultureManager _cultureManager = CultureManager.Instance();
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private DispatcherTimer _clockTimer;

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

        #endregion

        #region Commands

        public DelegateCommand EdgeDetectionCommand { get; }
        public DelegateCommand SimulateInputCommand { get; }
        public DelegateCommand MappingConfigCommand { get; }
        public DelegateCommand OutputTestCommand { get; }
        public DelegateCommand MinimizeCommand { get; }
        public DelegateCommand CloseCommand { get; }

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

        #endregion

        public void Cleanup()
        {
            _clockTimer?.Stop();
            _cultureManager.CultureChanged -= OnCultureChanged;
        }
    }
}