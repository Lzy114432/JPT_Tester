using System;
using System.Windows;
using Prism.Commands;
using Prism.Mvvm;
using Ewan.Core.Logger;
using Ewan.Model.System;

namespace MarkingMachineFeeder.Viewmodel
{
    public class ParameterSettingsViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(ParameterSettingsViewModel));
        private readonly SystemParametersManager _parametersManager = SystemParametersManager.Instance;

        #region Parameter Properties
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
        #endregion

        #region Commands
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand<Window> CloseCommand { get; }
        #endregion

        public ParameterSettingsViewModel()
        {
            SaveCommand = new DelegateCommand(ExecuteSave);
            ApplyCommand = new DelegateCommand(ExecuteApply);
            CancelCommand = new DelegateCommand(ExecuteCancel);
            CloseCommand = new DelegateCommand<Window>(ExecuteClose);

            LoadParameters();
        }

        private void LoadParameters()
        {
            var parameters = _parametersManager.Parameters;
            HighSpeedModeEnabled = parameters.HighSpeedModeEnabled;
            ResetDelayMs = parameters.ResetDelayMs;
            LowSpeedSetupDelayMs = parameters.LowSpeedSetupDelayMs;
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
                    HighSpeedModeEnabled = HighSpeedModeEnabled,
                    ResetDelayMs = ResetDelayMs,
                    LowSpeedSetupDelayMs = LowSpeedSetupDelayMs
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
                    window.Close();
                    break;
                }
            }
        }
    }
}
