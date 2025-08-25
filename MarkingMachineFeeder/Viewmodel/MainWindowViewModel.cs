using Ewan.Core.Culture;
using Ewan.Core.Logger;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Globalization;

namespace MarkingMachineFeeder.Viewmodel
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly CultureManager _cultureManager;

        private string _title = "MarkingMachineFeeder";
        private string _languageMenuHeader = "Language";
        private string _testLogButtonText = "Test Log";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public string LanguageMenuHeader
        {
            get { return _languageMenuHeader; }
            set { SetProperty(ref _languageMenuHeader, value); }
        }

        public string TestLogButtonText
        {
            get { return _testLogButtonText; }
            set { SetProperty(ref _testLogButtonText, value); }
        }

        public DelegateCommand<string> SwitchLanguageCommand { get; }
        public DelegateCommand TestLogCommand { get; }

        public MainWindowViewModel()
        {
            _cultureManager = CultureManager.Instance();
            _cultureManager.CultureChanged += OnCultureChanged;

            SwitchLanguageCommand = new DelegateCommand<string>(ExecuteSwitchLanguage);
            TestLogCommand = new DelegateCommand(ExecuteTestLog);

            UpdateUITexts();
            _uiLogger.Info(() => Ewan.Resources.LogMessages.MainWindowStarted);
        }

        private void ExecuteSwitchLanguage(string cultureName)
        {
            try
            {
                _cultureManager.SetCulture(cultureName);
            }
            catch (CultureNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Culture error: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General error: {ex.Message}");
            }
        }

        private void ExecuteTestLog()
        {
            // 统一使用字符串消息键方式
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemStatusNormal);
            _uiLogger.Warn(() => Ewan.Resources.LogMessages.Log4netConfigNotFound);
            _uiLogger.Error(() => Ewan.Resources.LogMessages.DatabaseConnectionError, "Connection timeout");
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingComplete, "2.35");
            _uiLogger.Debug(() => Ewan.Resources.LogMessages.BaseManagerInitialized, "TestManager");
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            UpdateUITexts();
        }

        private void UpdateUITexts()
        {
            switch (_cultureManager.CurrentCulture.Name)
            {
                case "zh-CN":
                    LanguageMenuHeader = "语言";
                    TestLogButtonText = "测试日志";
                    break;
                default:
                    LanguageMenuHeader = "Language";
                    TestLogButtonText = "Test Log";
                    break;
            }
        }
    }
}