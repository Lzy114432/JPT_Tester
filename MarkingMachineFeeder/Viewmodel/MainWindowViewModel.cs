using Ewan.Core.Culture;
using Ewan.Core.Logger;
using Ewan.Core.Security;
using Ewan.Model.Security;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Globalization;
using System.Linq;

namespace MarkingMachineFeeder.Viewmodel
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly CultureManager _cultureManager;
        private readonly SecurityManager _securityManager;

        private string _title = "MarkingMachineFeeder";
        private string _languageMenuHeader = "Language";
        private string _testLogButtonText = "Test Log";
        private string _currentUserText = "未登录";
        private string _logoutButtonText = "注销";
        private string _switchUserButtonText = "切换用户";
        private bool _canControlCamera = false;
        private bool _canControlUPS = false;
        private bool _canViewSettings = false;

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

        public string CurrentUserText
        {
            get { return _currentUserText; }
            set { SetProperty(ref _currentUserText, value); }
        }

        public string LogoutButtonText
        {
            get { return _logoutButtonText; }
            set { SetProperty(ref _logoutButtonText, value); }
        }

        public bool CanControlCamera
        {
            get { return _canControlCamera; }
            set { SetProperty(ref _canControlCamera, value); }
        }

        public bool CanControlUPS
        {
            get { return _canControlUPS; }
            set { SetProperty(ref _canControlUPS, value); }
        }

        public string SwitchUserButtonText
        {
            get { return _switchUserButtonText; }
            set { SetProperty(ref _switchUserButtonText, value); }
        }

        public bool CanViewSettings
        {
            get { return _canViewSettings; }
            set { SetProperty(ref _canViewSettings, value); }
        }
        public DelegateCommand<string> SwitchLanguageCommand { get; }
        public DelegateCommand TestLogCommand { get; }
        public DelegateCommand LogoutCommand { get; }
        public DelegateCommand SwitchUserCommand { get; }

        public MainWindowViewModel()
        {
            _cultureManager = CultureManager.Instance();
            _cultureManager.CultureChanged += OnCultureChanged;
            
            _securityManager = SecurityManager.Instance();
            _securityManager.UserAuthenticated += OnUserAuthenticated;
            _securityManager.UserLoggedOut += OnUserLoggedOut;

            SwitchLanguageCommand = new DelegateCommand<string>(ExecuteSwitchLanguage);
            TestLogCommand = new DelegateCommand(ExecuteTestLog);
            LogoutCommand = new DelegateCommand(ExecuteLogout);
            SwitchUserCommand = new DelegateCommand(ExecuteSwitchUser);

            UpdateUITexts();
            UpdateUserInfo();
            UpdatePermissions();
            
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

        private void ExecuteSwitchUser()
        {
            var loginWindow = new MarkingMachineFeeder.Windows.LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                // 用户重新登录成功，界面会自动更新
                _uiLogger.Info(() => Ewan.Resources.LogMessages.LoginSuccessful, _securityManager.CurrentUser?.Username);
            }
        }

        private void ExecuteLogout()
        {
            _securityManager.Logout();
        }

        private void OnUserAuthenticated(object sender, User user)
        {
            UpdateUserInfo();
            UpdatePermissions();
        }

        private void OnUserLoggedOut(object sender, EventArgs e)
        {
            UpdateUserInfo();
            UpdatePermissions();
        }

        private void UpdateUserInfo()
        {
            if (_securityManager.IsAuthenticated)
            {
                var user = _securityManager.CurrentUser;
                var roleNames = string.Join(", ", user.Roles.Select(r => r.DisplayName));
                CurrentUserText = $"{user.DisplayName} [{roleNames}]";
            }
            else
            {
                CurrentUserText = "未登录";
            }
        }

        private void UpdatePermissions()
        {
            CanControlCamera = _securityManager.HasPermission(PermissionResources.Camera, PermissionActions.Control);
            CanControlUPS = _securityManager.HasPermission(PermissionResources.UPS, PermissionActions.Control);
            CanViewSettings = _securityManager.HasPermission(PermissionResources.Settings, PermissionActions.View);
        }

        private void UpdateUITexts()
        {
            switch (_cultureManager.CurrentCulture.Name)
            {
                case "zh-CN":
                    LanguageMenuHeader = "语言";
                    TestLogButtonText = "测试日志";
                    LogoutButtonText = "注销";
                    SwitchUserButtonText = "切换用户";
                    break;
                default:
                    LanguageMenuHeader = "Language";
                    TestLogButtonText = "Test Log";
                    LogoutButtonText = "Logout";
                    SwitchUserButtonText = "Switch User";
                    break;
            }
        }
    }
}