using Ewan.Model.System;
using EwanCommon.Logging;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace MarkingMachineFeeder.Viewmodel
{
    public class ParameterSettingsViewModel : BindableBase
    {
        private readonly UILogger _uiLogger;
        private readonly SystemParametersManager _parametersManager;

        private readonly ObservableCollection<BinSelectionOption> _binOptions = new ObservableCollection<BinSelectionOption>();

        #region Parameter Properties
        private bool _enableLoadingModule = true;
        public bool EnableLoadingModule
        {
            get => _enableLoadingModule;
            set => SetProperty(ref _enableLoadingModule, value);
        }

        private bool _enableUnloadingModule = true;
        public bool EnableUnloadingModule
        {
            get => _enableUnloadingModule;
            set => SetProperty(ref _enableUnloadingModule, value);
        }

        private BinSelection _loadingSelectedBin = BinSelection.Bin1;
        public BinSelection LoadingSelectedBin
        {
            get => _loadingSelectedBin;
            set => SetProperty(ref _loadingSelectedBin, value);
        }

        private BinSelection _unloadingSelectedBin = BinSelection.Bin1;
        public BinSelection UnloadingSelectedBin
        {
            get => _unloadingSelectedBin;
            set => SetProperty(ref _unloadingSelectedBin, value);
        }

        private bool _highSpeedModeEnabled;
        public bool HighSpeedModeEnabled
        {
            get => _highSpeedModeEnabled;
            set => SetProperty(ref _highSpeedModeEnabled, value);
        }

        private int _resetDelayMs;
        public int ResetDelayMs
        {
            get => _resetDelayMs;
            set => SetProperty(ref _resetDelayMs, value);
        }

        private int _lowSpeedSetupDelayMs;
        public int LowSpeedSetupDelayMs
        {
            get => _lowSpeedSetupDelayMs;
            set => SetProperty(ref _lowSpeedSetupDelayMs, value);
        }

        private int _ringLineTimeoutSeconds;
        public int RingLineTimeoutSeconds
        {
            get => _ringLineTimeoutSeconds;
            set => SetProperty(ref _ringLineTimeoutSeconds, value);
        }
        private int i_小车间隔数量 = 3;
        public int I_小车间隔数量
        {
            get => i_小车间隔数量;
            set => SetProperty(ref i_小车间隔数量, value);
        }

        private int i_持续空车数量 = 3;
        public int I_持续空车数量
        {
            get => i_持续空车数量;
            set => SetProperty(ref i_持续空车数量, value);
        }


        private bool _safetyDoorAlarmBypass;
        public bool SafetyDoorAlarmBypass
        {
            get => _safetyDoorAlarmBypass;
            set => SetProperty(ref _safetyDoorAlarmBypass, value);
        }

        private int _emptyCartReserveCount;
        public int EmptyCartReserveCount
        {
            get => _emptyCartReserveCount;
            set => SetProperty(ref _emptyCartReserveCount, value);
        }

        private CartCheckMode _cartCheckMode = CartCheckMode.EmptyCart;
        public CartCheckMode CartCheckMode
        {
            get => _cartCheckMode;
            set
            {
                if (SetProperty(ref _cartCheckMode, value))
                {
                    RaisePropertyChanged(nameof(IsEmptyCartMode));
                    RaisePropertyChanged(nameof(IsCuttingBridgeCarMode));
                }
            }
        }

        public bool IsEmptyCartMode
        {
            get => _cartCheckMode == CartCheckMode.EmptyCart;
            set
            {
                if (value)
                {
                    CartCheckMode = CartCheckMode.EmptyCart;
                }
            }
        }

        public bool IsCuttingBridgeCarMode
        {
            get => _cartCheckMode == CartCheckMode.CuttingBridgeCar;
            set
            {
                if (value)
                {
                    CartCheckMode = CartCheckMode.CuttingBridgeCar;
                }
            }
        }

        private int _cuttingBridgeCarReserveCount;
        public int CuttingBridgeCarReserveCount
        {
            get => _cuttingBridgeCarReserveCount;
            set => SetProperty(ref _cuttingBridgeCarReserveCount, value);
        }

        private int _codeReaderScanRetryCount = 3;
        public int CodeReaderScanRetryCount
        {
            get => _codeReaderScanRetryCount;
            set => SetProperty(ref _codeReaderScanRetryCount, value);
        }

        private bool _mesEnabled;
        public bool MesEnabled
        {
            get => _mesEnabled;
            set => SetProperty(ref _mesEnabled, value);
        }

        private string _mesBrokerHost = "localhost";
        public string MesBrokerHost
        {
            get => _mesBrokerHost;
            set => SetProperty(ref _mesBrokerHost, value);
        }

        private int _mesBrokerPort = 1883;
        public int MesBrokerPort
        {
            get => _mesBrokerPort;
            set => SetProperty(ref _mesBrokerPort, value);
        }

        private string _mesUserName = string.Empty;
        public string MesUserName
        {
            get => _mesUserName;
            set => SetProperty(ref _mesUserName, value);
        }

        private string _mesPassword = string.Empty;
        public string MesPassword
        {
            get => _mesPassword;
            set => SetProperty(ref _mesPassword, value);
        }

        private string _mesClientId = string.Empty;
        public string MesClientId
        {
            get => _mesClientId;
            set => SetProperty(ref _mesClientId, value);
        }

        private bool _mesCleanSession = true;
        public bool MesCleanSession
        {
            get => _mesCleanSession;
            set => SetProperty(ref _mesCleanSession, value);
        }

        private int _mesKeepAliveSeconds = 30;
        public int MesKeepAliveSeconds
        {
            get => _mesKeepAliveSeconds;
            set => SetProperty(ref _mesKeepAliveSeconds, value);
        }

        private string _mesRingLineDeviceId = string.Empty;
        public string MesRingLineDeviceId
        {
            get => _mesRingLineDeviceId;
            set => SetProperty(ref _mesRingLineDeviceId, value);
        }

        private string _mesRingLineDeviceCode = string.Empty;
        public string MesRingLineDeviceCode
        {
            get => _mesRingLineDeviceCode;
            set => SetProperty(ref _mesRingLineDeviceCode, value);
        }

        private string _liaokuangCodeTemplate = "BIN{0:D2}";
        public string LiaokuangCodeTemplate
        {
            get => _liaokuangCodeTemplate;
            set => SetProperty(ref _liaokuangCodeTemplate, value);
        }
        #endregion

        public ObservableCollection<BinSelectionOption> BinOptions => _binOptions;

        #region UI Text Properties
        private string _runModeTitle = "运行模式";
        public string RunModeTitle
        {
            get => _runModeTitle;
            set => SetProperty(ref _runModeTitle, value);
        }

        private string _enableLoadingLabel = string.Empty;
        public string EnableLoadingLabel
        {
            get => _enableLoadingLabel;
            set => SetProperty(ref _enableLoadingLabel, value);
        }

        private string _enableUnloadingLabel = string.Empty;
        public string EnableUnloadingLabel
        {
            get => _enableUnloadingLabel;
            set => SetProperty(ref _enableUnloadingLabel, value);
        }

        private string _loadingBinSelectionLabel = string.Empty;
        public string LoadingBinSelectionLabel
        {
            get => _loadingBinSelectionLabel;
            set => SetProperty(ref _loadingBinSelectionLabel, value);
        }

        private string _unloadingBinSelectionLabel = string.Empty;
        public string UnloadingBinSelectionLabel
        {
            get => _unloadingBinSelectionLabel;
            set => SetProperty(ref _unloadingBinSelectionLabel, value);
        }

        private string _highSpeedModeLabel = "启用高速运行模式";
        public string HighSpeedModeLabel
        {
            get => _highSpeedModeLabel;
            set => SetProperty(ref _highSpeedModeLabel, value);
        }

        private string _highSpeedModeDesc = "启用后系统将在启动时自动切换到高速运行模式";
        public string HighSpeedModeDesc
        {
            get => _highSpeedModeDesc;
            set => SetProperty(ref _highSpeedModeDesc, value);
        }

        private string _safetyDoorBypassLabel = "安全门报警屏蔽";
        public string SafetyDoorBypassLabel
        {
            get => _safetyDoorBypassLabel;
            set => SetProperty(ref _safetyDoorBypassLabel, value);
        }

        private string _safetyDoorBypassDesc = "勾选后启动将忽略安全门状态，不触发安全门报警";
        public string SafetyDoorBypassDesc
        {
            get => _safetyDoorBypassDesc;
            set => SetProperty(ref _safetyDoorBypassDesc, value);
        }

        private string _keepEmptyCartCountLabel = "保持空车数量";
        public string KeepEmptyCartCountLabel
        {
            get => _keepEmptyCartCountLabel;
            set => SetProperty(ref _keepEmptyCartCountLabel, value);
        }

        private string _keepEmptyCartCountDesc = "当空车数量少于或等于该值时系统会自动下空车";
        public string KeepEmptyCartCountDesc
        {
            get => _keepEmptyCartCountDesc;
            set => SetProperty(ref _keepEmptyCartCountDesc, value);
        }

        private string _timingParametersTitle = "时序参数";
        public string TimingParametersTitle
        {
            get => _timingParametersTitle;
            set => SetProperty(ref _timingParametersTitle, value);
        }

        private string _systemResetDelayLabel = "系统复位延迟时间 (毫秒)";
        public string SystemResetDelayLabel
        {
            get => _systemResetDelayLabel;
            set => SetProperty(ref _systemResetDelayLabel, value);
        }

        private string _systemResetDelayDesc = "系统复位初始化完成后的等待时间";
        public string SystemResetDelayDesc
        {
            get => _systemResetDelayDesc;
            set => SetProperty(ref _systemResetDelayDesc, value);
        }

        private string _lowSpeedSetupDelayLabel = "低速模式设置延迟 (毫秒)";
        public string LowSpeedSetupDelayLabel
        {
            get => _lowSpeedSetupDelayLabel;
            set => SetProperty(ref _lowSpeedSetupDelayLabel, value);
        }

        private string _lowSpeedSetupDelayDesc = "设置低速运行模式后的等待时间";
        public string LowSpeedSetupDelayDesc
        {
            get => _lowSpeedSetupDelayDesc;
            set => SetProperty(ref _lowSpeedSetupDelayDesc, value);
        }

        private string _ringLineTimeoutLabel = "环线上料请求超时时间 (秒)";
        public string RingLineTimeoutLabel
        {
            get => _ringLineTimeoutLabel;
            set => SetProperty(ref _ringLineTimeoutLabel, value);
        }

        private string _ringLineTimeoutDesc = "环线上料请求等待响应的超时时间";
        public string RingLineTimeoutDesc
        {
            get => _ringLineTimeoutDesc;
            set => SetProperty(ref _ringLineTimeoutDesc, value);
        }



        #endregion

        #region Commands
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand<Window> CloseCommand { get; }
        #endregion

        public ParameterSettingsViewModel()
        {
            // Design-time support
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                // Initialize dummy data for designer
                _binOptions.Add(new BinSelectionOption(BinSelection.Bin1, "料仓 1 (设计时)"));
                _binOptions.Add(new BinSelectionOption(BinSelection.Bin2, "料仓 2 (设计时)"));
                _binOptions.Add(new BinSelectionOption(BinSelection.Bin3, "料仓 3 (设计时)"));
                LoadingSelectedBin = BinSelection.Bin1;
                UnloadingSelectedBin = BinSelection.Bin1;

                // Set localized strings to fallback values or dummy text
                EnableLoadingLabel = "启用上料模块";
                EnableUnloadingLabel = "启用下料模块";
                LoadingBinSelectionLabel = "装料料仓选择";
                UnloadingBinSelectionLabel = "下料料仓选择";

                MesEnabled = false;
                MesBrokerHost = "localhost";
                MesBrokerPort = 1883;
                MesUserName = string.Empty;
                MesPassword = string.Empty;
                MesClientId = string.Empty;
                MesCleanSession = true;
                MesKeepAliveSeconds = 30;
                MesRingLineDeviceId = string.Empty;
                MesRingLineDeviceCode = string.Empty;
                LiaokuangCodeTemplate = "BIN{0:D2}";
                CodeReaderScanRetryCount = 3;

                return;
            }

            _uiLogger = new UILogger();
            _parametersManager = SystemParametersManager.Instance;

            SaveCommand = new DelegateCommand(ExecuteSave);
            ApplyCommand = new DelegateCommand(ExecuteApply);
            CancelCommand = new DelegateCommand(ExecuteCancel);
            CloseCommand = new DelegateCommand<Window>(ExecuteClose);

            InitializeBinOptions();
            UpdateUITexts();
            LoadParameters();
        }

        private void LoadParameters()
        {
            var parameters = _parametersManager.Parameters;
            EnableLoadingModule = parameters.EnableLoadingModule;
            EnableUnloadingModule = parameters.EnableUnloadingModule;
            LoadingSelectedBin = parameters.LoadingBinSelection;
            UnloadingSelectedBin = parameters.UnloadingBinSelection;
            HighSpeedModeEnabled = parameters.HighSpeedModeEnabled;
            ResetDelayMs = parameters.ResetDelayMs;
            LowSpeedSetupDelayMs = parameters.LowSpeedSetupDelayMs;
            RingLineTimeoutSeconds = parameters.RingLineTimeoutSeconds;
            SafetyDoorAlarmBypass = parameters.SafetyDoorAlarmBypass;
            EmptyCartReserveCount = parameters.EmptyCartReserveCount;
            CartCheckMode = parameters.CartCheckMode;
            CuttingBridgeCarReserveCount = parameters.CuttingBridgeCarReserveCount;
            CodeReaderScanRetryCount = parameters.CodeReaderScanRetryCount;
            MesEnabled = parameters.MesEnabled;
            MesBrokerHost = parameters.MesBrokerHost;
            MesBrokerPort = parameters.MesBrokerPort;
            MesUserName = parameters.MesUserName;
            MesPassword = parameters.MesPassword;
            MesClientId = parameters.MesClientId;
            MesCleanSession = parameters.MesCleanSession;
            MesKeepAliveSeconds = parameters.MesKeepAliveSeconds;
            MesRingLineDeviceId = parameters.MesRingLineDeviceId;
            MesRingLineDeviceCode = parameters.MesRingLineDeviceCode;
            I_小车间隔数量 = parameters.I_小车间隔数量;
            LiaokuangCodeTemplate = string.IsNullOrWhiteSpace(parameters.LiaokuangCodeTemplate)
                ? "BIN{0:D2}"
                : parameters.LiaokuangCodeTemplate;
            I_持续空车数量 = parameters.I_持续空车数量;
            UpdateBinOptionDisplays();
        }

        private void UpdateUITexts()
        {
            EnableLoadingLabel = "启用装料模块";
            EnableUnloadingLabel = "启用下料模块";
            LoadingBinSelectionLabel = "装料料仓选择";
            UnloadingBinSelectionLabel = "下料料仓选择";

            UpdateBinOptionDisplays();
        }

        private void InitializeBinOptions()
        {
            if (_binOptions.Count == 0)
            {
                _binOptions.Add(new BinSelectionOption(BinSelection.Bin1, GetBinDisplayText(BinSelection.Bin1)));
                _binOptions.Add(new BinSelectionOption(BinSelection.Bin2, GetBinDisplayText(BinSelection.Bin2)));
                _binOptions.Add(new BinSelectionOption(BinSelection.Bin3, GetBinDisplayText(BinSelection.Bin3)));
            }
            else
            {
                UpdateBinOptionDisplays();
            }
        }

        private void UpdateBinOptionDisplays()
        {
            if (_binOptions.Count == 0)
            {
                InitializeBinOptions();
                return;
            }

            foreach (var option in _binOptions)
            {
                option.Display = GetBinDisplayText(option.Value);
            }
        }

        private string GetBinDisplayText(BinSelection selection)
        {
            switch (selection)
            {
                case BinSelection.Bin2:
                    return "料仓2";
                case BinSelection.Bin3:
                    return "料仓3";
                default:
                    return "料仓1";
            }
        }

        private void Cleanup()
        {
            // 国际化已移除，无需订阅/取消订阅语言变更事件
        }

        private void ExecuteSave()
        {

            if (SaveParameters())
            {
                _uiLogger.InfoRaw("系统参数已保存");
                CloseWindow();
            }
            SystemParametersManager.Instance.Reload();
        }

        private void ExecuteApply()
        {

            if (SaveParameters())
            {
                _uiLogger.InfoRaw("系统参数已应用");
            }
            SystemParametersManager.Instance.Reload();
        }

        private void ExecuteCancel()
        {
            CloseWindow();
        }

        private void ExecuteClose(Window window)
        {
            window?.Close();
        }

        public bool SaveParameters()
        {
            try
            {
                var existing = _parametersManager.Parameters;

                _parametersManager.Parameters.EnableLoadingModule = EnableLoadingModule;
                _parametersManager.Parameters.EnableUnloadingModule = EnableUnloadingModule;
                _parametersManager.Parameters.LoadingBinSelection = LoadingSelectedBin;
                _parametersManager.Parameters.UnloadingBinSelection = UnloadingSelectedBin;
                _parametersManager.Parameters.HighSpeedModeEnabled = HighSpeedModeEnabled;
                _parametersManager.Parameters.ResetDelayMs = ResetDelayMs;
                _parametersManager.Parameters.LowSpeedSetupDelayMs = LowSpeedSetupDelayMs;
                _parametersManager.Parameters.RingLineTimeoutSeconds = RingLineTimeoutSeconds;
                _parametersManager.Parameters.SafetyDoorAlarmBypass = SafetyDoorAlarmBypass;
                _parametersManager.Parameters.EmptyCartReserveCount = EmptyCartReserveCount;
                _parametersManager.Parameters.CartCheckMode = CartCheckMode;
                _parametersManager.Parameters.CuttingBridgeCarReserveCount = CuttingBridgeCarReserveCount;
                _parametersManager.Parameters.CodeReaderType = existing?.CodeReaderType ?? "Datalogic";
                _parametersManager.Parameters.CodeReaderIp = existing?.CodeReaderIp ?? "192.168.3.100";
                _parametersManager.Parameters.CodeReaderPort = existing?.CodeReaderPort ?? 51236;
                _parametersManager.Parameters.CodeReaderTriggerCommand = existing?.CodeReaderTriggerCommand ?? "T";
                _parametersManager.Parameters.CodeReaderConnectionTimeoutMs = existing?.CodeReaderConnectionTimeoutMs ?? 3000;
                _parametersManager.Parameters.CodeReaderReceiveTimeoutMs = existing?.CodeReaderReceiveTimeoutMs ?? 5000;
                _parametersManager.Parameters.CodeReaderScanRetryCount = CodeReaderScanRetryCount <= 0 ? 1 : CodeReaderScanRetryCount;
                _parametersManager.Parameters.MesEnabled = MesEnabled;
                _parametersManager.Parameters.MesBrokerHost = MesBrokerHost;
                _parametersManager.Parameters.MesBrokerPort = MesBrokerPort;
                _parametersManager.Parameters.MesUserName = MesUserName;
                _parametersManager.Parameters.MesPassword = MesPassword;
                _parametersManager.Parameters.MesClientId = MesClientId;
                _parametersManager.Parameters.MesCleanSession = MesCleanSession;
                _parametersManager.Parameters.MesKeepAliveSeconds = MesKeepAliveSeconds;
                _parametersManager.Parameters.MesRingLineDeviceId = MesRingLineDeviceId;
                _parametersManager.Parameters.MesRingLineDeviceCode = MesRingLineDeviceCode;
                _parametersManager.Parameters.I_小车间隔数量 = I_小车间隔数量;
                _parametersManager.Parameters.LiaokuangCodeTemplate = string.IsNullOrWhiteSpace(LiaokuangCodeTemplate) ? "BIN{0:D2}"
                        : LiaokuangCodeTemplate.Trim();
                _parametersManager.Parameters.I_持续空车数量 = I_持续空车数量;
                var parameters = _parametersManager.Parameters;

                //var parameters = new Ewan.Model.System.SystemParameters
                //{
                //    EnableLoadingModule = EnableLoadingModule,
                //    EnableUnloadingModule = EnableUnloadingModule,
                //    LoadingBinSelection = LoadingSelectedBin,
                //UnloadingBinSelection = UnloadingSelectedBin,
                //HighSpeedModeEnabled = HighSpeedModeEnabled,
                //ResetDelayMs = ResetDelayMs,
                //LowSpeedSetupDelayMs = LowSpeedSetupDelayMs,
                //RingLineTimeoutSeconds = RingLineTimeoutSeconds,
                //SafetyDoorAlarmBypass = SafetyDoorAlarmBypass,
                //EmptyCartReserveCount = EmptyCartReserveCount,
                //CartCheckMode = CartCheckMode,
                //CuttingBridgeCarReserveCount = CuttingBridgeCarReserveCount,
                //CodeReaderType = existing?.CodeReaderType ?? "Datalogic",
                //CodeReaderIp = existing?.CodeReaderIp ?? "192.168.3.100",
                //CodeReaderPort = existing?.CodeReaderPort ?? 51236,
                //CodeReaderTriggerCommand = existing?.CodeReaderTriggerCommand ?? "T",
                //CodeReaderConnectionTimeoutMs = existing?.CodeReaderConnectionTimeoutMs ?? 3000,
                //CodeReaderReceiveTimeoutMs = existing?.CodeReaderReceiveTimeoutMs ?? 5000,
                //CodeReaderScanRetryCount = CodeReaderScanRetryCount <= 0 ? 1 : CodeReaderScanRetryCount,
                //MesEnabled = MesEnabled,
                //MesBrokerHost = MesBrokerHost,
                //MesBrokerPort = MesBrokerPort,
                //MesUserName = MesUserName,
                //MesPassword = MesPassword,
                //MesClientId = MesClientId,
                //MesCleanSession = MesCleanSession,
                //MesKeepAliveSeconds = MesKeepAliveSeconds,
                //MesRingLineDeviceId = MesRingLineDeviceId,
                //MesRingLineDeviceCode = MesRingLineDeviceCode,
                //I_小车间隔数量 = I_小车间隔数量,
                //LiaokuangCodeTemplate = string.IsNullOrWhiteSpace(LiaokuangCodeTemplate)
                //    ? "BIN{0:D2}"
                //    : LiaokuangCodeTemplate.Trim()
                //};

                if (!parameters.Validate())
                {
                    MessageBox.Show(
                        "参数验证失败，请检查输入值是否有效。",
                        "参数错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                if (_parametersManager.SaveParameters(parameters))
                {
                    MessageBox.Show(
                        "系统参数保存成功！",
                        "成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    MessageBox.Show(
                        "系统参数保存失败，请重试。",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw($"保存系统参数失败: {ex.Message}");
                MessageBox.Show(
                    $"保存系统参数时发生错误：\n{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        public bool SaveParametersOnExit()
        {
            try
            {
                // 复用现有私有方法（会展示提示框），若需要在退出时不弹框，可改为在此实现静默保存逻辑
                return SaveParameters();
            }
            catch
            {
                return false;
            }
        }

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    Cleanup();
                    window.Close();
                    break;
                }
            }
        }

        public class BinSelectionOption : BindableBase
        {
            public BinSelectionOption(BinSelection value, string display)
            {
                Value = value;
                _display = display;
            }

            public BinSelection Value { get; }

            private string _display;
            public string Display
            {
                get => _display;
                set => SetProperty(ref _display, value);
            }
        }
    }
}
