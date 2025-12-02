using System;
using System.Collections.ObjectModel;
using System.Windows;
using Prism.Commands;
using Prism.Mvvm;
using Ewan.Core.Culture;
using Ewan.Core.Logger;
using Ewan.Model.System;

namespace MarkingMachineFeeder.Viewmodel
{
    public class ParameterSettingsViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(ParameterSettingsViewModel));
        private readonly SystemParametersManager _parametersManager = SystemParametersManager.Instance;
        private readonly CultureManager _cultureManager = CultureManager.Instance();

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

        private BinSelection _selectedBin = BinSelection.Bin1;
        public BinSelection SelectedBin
        {
            get => _selectedBin;
            set => SetProperty(ref _selectedBin, value);
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

        private bool _safetyDoorAlarmBypass;
        public bool SafetyDoorAlarmBypass
        {
            get => _safetyDoorAlarmBypass;
            set => SetProperty(ref _safetyDoorAlarmBypass, value);
        }
        #endregion

        public ObservableCollection<BinSelectionOption> BinOptions => _binOptions;

        #region UI Text Properties
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

        private string _binSelectionLabel = string.Empty;
        public string BinSelectionLabel
        {
            get => _binSelectionLabel;
            set => SetProperty(ref _binSelectionLabel, value);
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
            Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;

            _cultureManager.CultureChanged += OnCultureChanged;

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
            SelectedBin = parameters.SelectedBin;
            HighSpeedModeEnabled = parameters.HighSpeedModeEnabled;
            ResetDelayMs = parameters.ResetDelayMs;
            LowSpeedSetupDelayMs = parameters.LowSpeedSetupDelayMs;
            RingLineTimeoutSeconds = parameters.RingLineTimeoutSeconds;
            SafetyDoorAlarmBypass = parameters.SafetyDoorAlarmBypass;

            UpdateBinOptionDisplays();
        }

        private void UpdateUITexts()
        {
            EnableLoadingLabel = Ewan.Resources.UIStrings.EnableLoadingOptionLabel;
            EnableUnloadingLabel = Ewan.Resources.UIStrings.EnableUnloadingOptionLabel;
            BinSelectionLabel = Ewan.Resources.UIStrings.BinSelectionLabel;

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
                    return Ewan.Resources.UIStrings.BinOption2;
                case BinSelection.Bin3:
                    return Ewan.Resources.UIStrings.BinOption3;
                default:
                    return Ewan.Resources.UIStrings.BinOption1;
            }
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            Ewan.Resources.UIStrings.Culture = e.NewCulture;
            UpdateUITexts();
        }

        private void Cleanup()
        {
            _cultureManager.CultureChanged -= OnCultureChanged;
        }

        private void ExecuteSave()
        {
            if (SaveParameters())
            {
                _uiLogger.InfoRaw("系统参数已保存");
                CloseWindow();
            }
        }

        private void ExecuteApply()
        {
            if (SaveParameters())
            {
                _uiLogger.InfoRaw("系统参数已应用");
            }
        }

        private void ExecuteCancel()
        {
            CloseWindow();
        }

        private void ExecuteClose(Window window)
        {
            window?.Close();
        }

        private bool SaveParameters()
        {
            try
            {
                var parameters = new Ewan.Model.System.SystemParameters
                {
                    EnableLoadingModule = EnableLoadingModule,
                    EnableUnloadingModule = EnableUnloadingModule,
                    SelectedBin = SelectedBin,
                    HighSpeedModeEnabled = HighSpeedModeEnabled,
                    ResetDelayMs = ResetDelayMs,
                    LowSpeedSetupDelayMs = LowSpeedSetupDelayMs,
                    RingLineTimeoutSeconds = RingLineTimeoutSeconds,
                    SafetyDoorAlarmBypass = SafetyDoorAlarmBypass
                };

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
