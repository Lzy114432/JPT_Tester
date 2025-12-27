using Ewan.Core.Axis;
using EwanCommon.Logging;
using Ewan.Core.Security;
using Ewan.Model.Config;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace MarkingMachineFeeder.Viewmodel
{
    public class AxisControlViewModel : BindableBase
    {
        private readonly SecurityManager _securityManager = SecurityManager.Instance();
        private readonly AxisManager _axisManager = AxisManager.Instance();
        private readonly UILogger _uiLogger = new UILogger();
        private DispatcherTimer _statusTimer;
        
        #region Properties
        
        private ObservableCollection<AxisStatusInfo> _axisConfigs;
        public ObservableCollection<AxisStatusInfo> AxisConfigs
        {
            get => _axisConfigs;
            set => SetProperty(ref _axisConfigs, value);
        }

        private AxisStatusInfo _selectedAxis;
        public AxisStatusInfo SelectedAxis
        {
            get => _selectedAxis;
            set 
            { 
                if (SetProperty(ref _selectedAxis, value))
                {
                    // 更新UI可见性
                    RaisePropertyChanged(nameof(HasSelectedAxis));
                    RaisePropertyChanged(nameof(HasNoSelectedAxis));
                    
                    // 刷新命令状态
                    RefreshAxisCommands();
                }
            }
        }

        /// <summary>
        /// 是否有选中的轴
        /// </summary>
        public bool HasSelectedAxis => SelectedAxis != null;

        /// <summary>
        /// 是否没有选中轴
        /// </summary>
        public bool HasNoSelectedAxis => SelectedAxis == null;

        private double _jogSpeed = 100;
        public double JogSpeed
        {
            get => _jogSpeed;
            set => SetProperty(ref _jogSpeed, value);
        }

        private double _targetPosition = 0;
        public double TargetPosition
        {
            get => _targetPosition;
            set => SetProperty(ref _targetPosition, value);
        }

        #region UI Texts
        
        private string _windowTitle = "轴手动控制";
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        private string _axisStatusText = "轴状态信息";
        public string AxisStatusText
        {
            get => _axisStatusText;
            set => SetProperty(ref _axisStatusText, value);
        }

        private string _axisIDHeaderText = "轴号";
        public string AxisIDHeaderText
        {
            get => _axisIDHeaderText;
            set => SetProperty(ref _axisIDHeaderText, value);
        }

        private string _enabledHeaderText = "使能";
        public string EnabledHeaderText
        {
            get => _enabledHeaderText;
            set => SetProperty(ref _enabledHeaderText, value);
        }

        #endregion

        #endregion

        #region Commands
        
        // 全局控制命令
        public DelegateCommand EnableAllCommand { get; private set; }
        public DelegateCommand DisableAllCommand { get; private set; }
        public DelegateCommand StopAllCommand { get; private set; }
        public DelegateCommand EmergencyStopCommand { get; private set; }
        
        // 单轴控制命令
        public DelegateCommand EnableAxisCommand { get; private set; }
        public DelegateCommand DisableAxisCommand { get; private set; }
        public DelegateCommand JogPositiveCommand { get; private set; }
        public DelegateCommand JogNegativeCommand { get; private set; }
        public DelegateCommand JogStopCommand { get; private set; }
        public DelegateCommand AbsMoveCommand { get; private set; }
        public DelegateCommand MoveToZeroCommand { get; private set; }
        public DelegateCommand MoveToMinCommand { get; private set; }
        public DelegateCommand MoveToMaxCommand { get; private set; }
        
        // 其他命令
        public DelegateCommand RefreshStatusCommand { get; private set; }
        public DelegateCommand CloseCommand { get; private set; }

        #endregion

        public AxisControlViewModel()
        {
            InitializeCommands();
            InitializeData();
            InitializeTimer();
            
            // Check if in design mode
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                InitializeDesignTimeData();
            }
            else
            {
                UpdateUITexts();
                
                // 监听轴配置更新事件
                _axisManager.ConfigurationUpdated += OnAxisConfigurationUpdated;
            }
        }

        private void InitializeCommands()
        {
            // 全局控制命令
            EnableAllCommand = new DelegateCommand(ExecuteEnableAll);
            DisableAllCommand = new DelegateCommand(ExecuteDisableAll);
            StopAllCommand = new DelegateCommand(ExecuteStopAll);
            EmergencyStopCommand = new DelegateCommand(ExecuteEmergencyStop);
            
            // 单轴控制命令
            EnableAxisCommand = new DelegateCommand(ExecuteEnableAxis, CanExecuteAxisCommand);
            DisableAxisCommand = new DelegateCommand(ExecuteDisableAxis, CanExecuteAxisCommand);
            JogPositiveCommand = new DelegateCommand(ExecuteJogPositive, CanExecuteAxisCommand);
            JogNegativeCommand = new DelegateCommand(ExecuteJogNegative, CanExecuteAxisCommand);
            JogStopCommand = new DelegateCommand(ExecuteJogStop, CanExecuteAxisCommand);
            AbsMoveCommand = new DelegateCommand(ExecuteAbsMove, CanExecuteAxisCommand);
            MoveToZeroCommand = new DelegateCommand(ExecuteMoveToZero, CanExecuteAxisCommand);
            MoveToMinCommand = new DelegateCommand(ExecuteMoveToMin, CanExecuteAxisCommand);
            MoveToMaxCommand = new DelegateCommand(ExecuteMoveToMax, CanExecuteAxisCommand);
            
            // 其他命令
            RefreshStatusCommand = new DelegateCommand(ExecuteRefreshStatus);
            CloseCommand = new DelegateCommand(ExecuteClose);
        }

        private void InitializeData()
        {
            AxisConfigs = new ObservableCollection<AxisStatusInfo>();
            LoadAxisConfigs();
        }

        private void InitializeTimer()
        {
            // 创建定时器用于刷新轴状态
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromMilliseconds(500); // 500ms刷新一次
            _statusTimer.Tick += OnStatusTimerTick;
            _statusTimer.Start();
        }

        private void InitializeDesignTimeData()
        {
            // 设计时数据
            WindowTitle = "轴手动控制";
            AxisStatusText = "轴状态信息";
            AxisIDHeaderText = "轴号";
            EnabledHeaderText = "使能";
            
            // 创建示例数据
            AxisConfigs.Add(new AxisStatusInfo { AxisID = 0, IsUsing = true, Position = 125.50, Speed = 1000, IsAlarm = false, IsBusy = false });
            AxisConfigs.Add(new AxisStatusInfo { AxisID = 1, IsUsing = false, Position = -5.25, Speed = 800, IsAlarm = false, IsBusy = false });
            AxisConfigs.Add(new AxisStatusInfo { AxisID = 2, IsUsing = true, Position = 0.00, Speed = 1200, IsAlarm = true, IsBusy = true });
            
            SelectedAxis = AxisConfigs.FirstOrDefault();
        }

        private void LoadAxisConfigs()
        {
            try
            {
                var allConfigs = _axisManager.GetAllAxisConfigs();
                AxisConfigs.Clear();
                
                foreach (var config in allConfigs)
                {
                    var statusInfo = new AxisStatusInfo
                    {
                        AxisID = config.AxisID,
                        IsUsing = config.IsUsing,
                        Speed = config.Speed,
                        MaxPos = config.MaxPos,
                        MinPos = config.MinPos,
                        Step = config.Step,
                        MotionDir = config.MotionDir,
                        Acc = config.Acc,
                        Dec = config.Dec,
                        Position = 0, // 初始位置
                        IsAlarm = false,
                        IsBusy = false
                    };
                    
                    AxisConfigs.Add(statusInfo);
                }
                
                // 自动选择第一个启用的轴
                SelectedAxis = AxisConfigs.FirstOrDefault(x => x.IsUsing);
                
                _uiLogger.Info("轴配置已加载：{0}", $"{AxisConfigs.Count}个轴");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("加载轴配置失败: {0}", ex.Message);
            }
        }

        private void OnStatusTimerTick(object sender, EventArgs e)
        {
            // 定时刷新轴状态
            RefreshAxisStatus();
        }

        private void RefreshAxisStatus()
        {
            try
            {
                foreach (var axisInfo in AxisConfigs)
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisInfo.AxisID);
                    if (axisConfig != null)
                    {
                        // 更新状态信息
                        axisInfo.Position = _axisManager.Position(axisConfig);
                        axisInfo.IsAlarm = _axisManager.IsAlarm(axisConfig);
                        axisInfo.IsBusy = _axisManager.IsBusy(axisConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理状态刷新错误，避免干扰用户操作
                System.Diagnostics.Debug.WriteLine($"刷新轴状态出错: {ex.Message}");
            }
        }

        private void UpdateUITexts()
        {
            WindowTitle = "轴手动控制";
            AxisStatusText = "轴状态信息";
            AxisIDHeaderText = "轴号";
            EnabledHeaderText = "使能";
        }

        private void RefreshAxisCommands()
        {
            // 刷新所有轴相关命令的状态
            EnableAxisCommand?.RaiseCanExecuteChanged();
            DisableAxisCommand?.RaiseCanExecuteChanged();
            JogPositiveCommand?.RaiseCanExecuteChanged();
            JogNegativeCommand?.RaiseCanExecuteChanged();
            JogStopCommand?.RaiseCanExecuteChanged();
            AbsMoveCommand?.RaiseCanExecuteChanged();
            MoveToZeroCommand?.RaiseCanExecuteChanged();
            MoveToMinCommand?.RaiseCanExecuteChanged();
            MoveToMaxCommand?.RaiseCanExecuteChanged();
        }

        #region Command Implementations

        private bool CanExecuteAxisCommand()
        {
            return SelectedAxis != null && SelectedAxis.IsUsing;
        }

        // 全局控制命令实现
        private void ExecuteEnableAll()
        {
            try
            {
                foreach (var axisInfo in AxisConfigs.Where(x => x.IsUsing))
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisInfo.AxisID);
                    if (axisConfig != null)
                    {
                        _axisManager.EnableAxis(axisConfig);
                    }
                }
                _uiLogger.Info("所有轴使能完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("所有轴使能失败: {0}", ex.Message);
            }
        }

        private void ExecuteDisableAll()
        {
            try
            {
                foreach (var axisInfo in AxisConfigs.Where(x => x.IsUsing))
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisInfo.AxisID);
                    if (axisConfig != null)
                    {
                        _axisManager.DisableAxis(axisConfig);
                    }
                }
                _uiLogger.Info("所有轴禁用完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("所有轴禁用失败: {0}", ex.Message);
            }
        }

        private void ExecuteStopAll()
        {
            try
            {
                foreach (var axisInfo in AxisConfigs.Where(x => x.IsUsing))
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisInfo.AxisID);
                    if (axisConfig != null)
                    {
                        _axisManager.DecStop(axisConfig);
                    }
                }
                _uiLogger.Info("所有轴停止完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("所有轴停止失败: {0}", ex.Message);
            }
        }

        private void ExecuteEmergencyStop()
        {
            try
            {
                foreach (var axisInfo in AxisConfigs.Where(x => x.IsUsing))
                {
                    var axisConfig = _axisManager.GetAxisConfig(axisInfo.AxisID);
                    if (axisConfig != null)
                    {
                        _axisManager.EmgStop(axisConfig);
                    }
                }
                _uiLogger.Info("紧急停止已激活");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("紧急停止失败: {0}", ex.Message);
            }
        }

        // 单轴控制命令实现
        private void ExecuteEnableAxis()
        {
            if (SelectedAxis == null) return;
            
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(SelectedAxis.AxisID);
                if (axisConfig != null)
                {
                    _axisManager.EnableAxis(axisConfig);
                    _uiLogger.Info($"轴 {SelectedAxis.AxisID} 使能完成");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"轴 {SelectedAxis.AxisID} 使能失败: {ex.Message}");
            }
        }

        private void ExecuteDisableAxis()
        {
            if (SelectedAxis == null) return;
            
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(SelectedAxis.AxisID);
                if (axisConfig != null)
                {
                    _axisManager.DisableAxis(axisConfig);
                    _uiLogger.Info($"轴 {SelectedAxis.AxisID} 禁用完成");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"轴 {SelectedAxis.AxisID} 禁用失败: {ex.Message}");
            }
        }

        private void ExecuteJogPositive()
        {
            if (SelectedAxis == null) return;
            
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(SelectedAxis.AxisID);
                if (axisConfig != null)
                {
                    _axisManager.Jog(axisConfig, JogSpeed);
                    _uiLogger.Info($"轴 {SelectedAxis.AxisID} 正向点动开始，速度: {JogSpeed}");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"轴 {SelectedAxis.AxisID} 点动失败: {ex.Message}");
            }
        }

        private void ExecuteJogNegative()
        {
            if (SelectedAxis == null) return;
            
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(SelectedAxis.AxisID);
                if (axisConfig != null)
                {
                    _axisManager.Jog(axisConfig, -JogSpeed);
                    _uiLogger.Info($"轴 {SelectedAxis.AxisID} 负向点动开始，速度: {JogSpeed}");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"轴 {SelectedAxis.AxisID} 点动失败: {ex.Message}");
            }
        }

        private void ExecuteJogStop()
        {
            if (SelectedAxis == null) return;
            
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(SelectedAxis.AxisID);
                if (axisConfig != null)
                {
                    _axisManager.JogStop(axisConfig);
                    _uiLogger.Info($"轴 {SelectedAxis.AxisID} 点动停止");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"轴 {SelectedAxis.AxisID} 点动停止失败: {ex.Message}");
            }
        }

        private void ExecuteAbsMove()
        {
            if (SelectedAxis == null) return;
            
            try
            {
                var axisConfig = _axisManager.GetAxisConfig(SelectedAxis.AxisID);
                if (axisConfig != null)
                {
                    // 检查位置限制
                    if (TargetPosition < SelectedAxis.MinPos || TargetPosition > SelectedAxis.MaxPos)
                    {
                        _uiLogger.Info($"轴 {SelectedAxis.AxisID} 目标位置 {TargetPosition} 超出范围 [{SelectedAxis.MinPos}, {SelectedAxis.MaxPos}]");
                        return;
                    }
                    
                    _axisManager.AbsMove(axisConfig, TargetPosition);
                    _uiLogger.Info($"轴 {SelectedAxis.AxisID} 开始移动到位置 {TargetPosition}");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"轴 {SelectedAxis.AxisID} 移动失败: {ex.Message}");
            }
        }

        private void ExecuteMoveToZero()
        {
            TargetPosition = 0;
            ExecuteAbsMove();
        }

        private void ExecuteMoveToMin()
        {
            if (SelectedAxis != null)
            {
                TargetPosition = SelectedAxis.MinPos;
                ExecuteAbsMove();
            }
        }

        private void ExecuteMoveToMax()
        {
            if (SelectedAxis != null)
            {
                TargetPosition = SelectedAxis.MaxPos;
                ExecuteAbsMove();
            }
        }

        private void ExecuteRefreshStatus()
        {
            LoadAxisConfigs();
            RefreshAxisStatus();
            _uiLogger.Info("轴状态已刷新");
        }

        private void ExecuteClose()
        {
            CloseWindow();
        }

        private void CloseWindow()
        {
            // 停止定时器
            _statusTimer?.Stop();
            
            // 找到并关闭窗口
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

        private void OnAxisConfigurationUpdated(object sender, EventArgs e)
        {
            // 轴配置更新后重新加载轴配置
            LoadAxisConfigs();
            _uiLogger.Info("轴控制界面已更新配置");
        }

        // 清理资源
        public void Dispose()
        {
            _statusTimer?.Stop();
            _axisManager.ConfigurationUpdated -= OnAxisConfigurationUpdated;
        }
    }

    /// <summary>
    /// 轴状态信息类 - 扩展AxisConfig以包含实时状态
    /// </summary>
    public class AxisStatusInfo : AxisConfig
    {
        private double _position;
        public new double Position
        {
            get => _position;
            set
            {
                if (Math.Abs(_position - value) > 0.001) // 避免频繁更新
                {
                    _position = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isAlarm;
        public bool IsAlarm
        {
            get => _isAlarm;
            set
            {
                if (_isAlarm != value)
                {
                    _isAlarm = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
