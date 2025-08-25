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
        private string _systemManagementMenuHeader = "系统管理";
        private string _permissionConfigMenuHeader = "权限配置";
        private bool _canControlCamera = false;
        private bool _canControlUPS = false;
        private bool _canViewSettings = false;
        private bool _canSwitchLanguage = false;

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

        public string SystemManagementMenuHeader
        {
            get { return _systemManagementMenuHeader; }
            set { SetProperty(ref _systemManagementMenuHeader, value); }
        }

        public string PermissionConfigMenuHeader
        {
            get { return _permissionConfigMenuHeader; }
            set { SetProperty(ref _permissionConfigMenuHeader, value); }
        }

        public bool CanViewSettings
        {
            get { return _canViewSettings; }
            set { SetProperty(ref _canViewSettings, value); }
        }

        public bool CanSwitchLanguage
        {
            get { return _canSwitchLanguage; }
            set { SetProperty(ref _canSwitchLanguage, value); }
        }
        public DelegateCommand<string> SwitchLanguageCommand { get; }
        public DelegateCommand TestLogCommand { get; }
        public DelegateCommand LogoutCommand { get; }
        public DelegateCommand SwitchUserCommand { get; }
        public DelegateCommand OpenPermissionConfigCommand { get; }

        public MainWindowViewModel()
        {
            // 运行时逻辑
            _cultureManager = CultureManager.Instance();
            _cultureManager.CultureChanged += OnCultureChanged;
            
            // 初始化UIStrings的Culture
            Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
            
            _securityManager = SecurityManager.Instance();
            _securityManager.UserAuthenticated += OnUserAuthenticated;
            _securityManager.UserLoggedOut += OnUserLoggedOut;

            SwitchLanguageCommand = new DelegateCommand<string>(ExecuteSwitchLanguage);
            TestLogCommand = new DelegateCommand(ExecuteTestLog);
            LogoutCommand = new DelegateCommand(ExecuteLogout);
            SwitchUserCommand = new DelegateCommand(ExecuteSwitchUser);
            OpenPermissionConfigCommand = new DelegateCommand(ExecuteOpenPermissionConfig, CanOpenPermissionConfig);

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
            // 同步UIStrings的Culture设置
            Ewan.Resources.UIStrings.Culture = e.NewCulture;
            
            UpdateUITexts();
            UpdateUserInfo(); // 同时更新用户信息显示
        }

        private void ExecuteSwitchUser()
        {
            var loginWindow = new MarkingMachineFeeder.Windows.LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                // 用户重新登录成功，界面会自动更新
            }
        }

        private void ExecuteLogout()
        {
            _securityManager.Logout();
        }

        private void ExecuteOpenPermissionConfig()
        {
            var permissionWindow = new MarkingMachineFeeder.Windows.PermissionConfigWindow();
            if (permissionWindow.ShowDialog() == true)
            {
                // 权限配置已保存，重新加载权限
                UpdatePermissions();
                _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemInitialized);
            }
        }

        private bool CanOpenPermissionConfig()
        {
            // 只有管理员可以打开权限配置
            return _securityManager.IsAuthenticated && 
                   _securityManager.CurrentUser?.Roles.Any(r => r.Name == "Administrator") == true;
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
                
                // 根据当前语言获取本地化的显示名称
                var localizedDisplayName = GetLocalizedUserDisplayName(user.Username);
                var localizedRoleNames = string.Join(", ", user.Roles.Select(r => GetLocalizedRoleDisplayName(r.Name)));
                
                CurrentUserText = string.Format(Ewan.Resources.UIStrings.CurrentUser, $"{localizedDisplayName} [{localizedRoleNames}]");
            }
            else
            {
                CurrentUserText = Ewan.Resources.UIStrings.NoUserLoggedIn;
            }
            
            // 强制触发CurrentUserText的PropertyChanged事件
            RaisePropertyChanged(nameof(CurrentUserText));
        }

        private string GetLocalizedUserDisplayName(string username)
        {
            switch (username.ToLower())
            {
                case "admin":
                    return Ewan.Resources.UIStrings.AdminUser;
                case "engineer":
                    return Ewan.Resources.UIStrings.EngineerUser;
                case "operator":
                    return Ewan.Resources.UIStrings.OperatorUser;
                default:
                    return username; // 使用原用户名作为后备
            }
        }

        private string GetLocalizedRoleDisplayName(string roleName)
        {
            switch (roleName)
            {
                case "Administrator":
                    return Ewan.Resources.UIStrings.AdminRole;
                case "Engineer":
                    return Ewan.Resources.UIStrings.EngineerRole;
                case "Operator":
                    return Ewan.Resources.UIStrings.OperatorRole;
                default:
                    return roleName; // 使用原角色名作为后备
            }
        }

        private void UpdatePermissions()
        {
            CanControlCamera = _securityManager.HasPermission(PermissionResources.Camera, PermissionActions.Control);
            CanControlUPS = _securityManager.HasPermission(PermissionResources.UPS, PermissionActions.Control);
            CanViewSettings = _securityManager.HasPermission(PermissionResources.Settings, PermissionActions.View);
            
            // 只有管理员和工程师可以切换语言，操作员不能
            if (_securityManager.IsAuthenticated)
            {
                var user = _securityManager.CurrentUser;
                var hasOperatorRole = user.Roles.Any(r => r.Name == "Operator");
                CanSwitchLanguage = !hasOperatorRole; // 操作员不能切换语言
            }
            else
            {
                CanSwitchLanguage = false; // 未登录用户不能切换语言
            }
        }

        private void UpdateUITexts()
        {
            Title = Ewan.Resources.UIStrings.MainWindowTitle;
            LanguageMenuHeader = Ewan.Resources.UIStrings.LanguageMenuHeader;
            TestLogButtonText = Ewan.Resources.UIStrings.TestLogButton;
            LogoutButtonText = Ewan.Resources.UIStrings.LogoutButton;
            SwitchUserButtonText = Ewan.Resources.UIStrings.SwitchUserButton;
            SystemManagementMenuHeader = Ewan.Resources.UIStrings.SystemManagementMenu;
            PermissionConfigMenuHeader = Ewan.Resources.UIStrings.PermissionConfigMenu;
            
            // 强制触发所有相关属性的PropertyChanged事件
            RaisePropertyChanged(nameof(Title));
            RaisePropertyChanged(nameof(LanguageMenuHeader));
            RaisePropertyChanged(nameof(TestLogButtonText));
            RaisePropertyChanged(nameof(LogoutButtonText));
            RaisePropertyChanged(nameof(SwitchUserButtonText));
            RaisePropertyChanged(nameof(SystemManagementMenuHeader));
            RaisePropertyChanged(nameof(PermissionConfigMenuHeader));
        }
    }
}