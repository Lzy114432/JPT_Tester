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
        private string _currentUser = "Guest";
        private string _currentUserLabel = "";
        private string _loginButtonText = "";
        private string _switchUserButtonText = "";
        private string _systemManagementMenuHeader = "";
        private string _permissionConfigMenuHeader = "";
        private string _settingsMenuHeader = "";
        private string _systemMenuHeader = "";
        private string _exitMenuHeader = "";
        private bool _canControlCamera = false;
        private bool _canControlUPS = false;
        private bool _canViewSettings = false;
        private bool _canSwitchLanguage = false;
        private bool _canExit = false;

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

        public string CurrentUser
        {
            get { return _currentUser; }
            set { SetProperty(ref _currentUser, value); }
        }

        public string LoginButtonText
        {
            get { return _loginButtonText; }
            set { SetProperty(ref _loginButtonText, value); }
        }

        public string CurrentUserText
        {
            get { return _currentUserText; }
            set { SetProperty(ref _currentUserText, value); }
        }

        public string CurrentUserLabel
        {
            get { return _currentUserLabel; }
            set { SetProperty(ref _currentUserLabel, value); }
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

        public string SettingsMenuHeader
        {
            get { return _settingsMenuHeader; }
            set { SetProperty(ref _settingsMenuHeader, value); }
        }

        public string SystemMenuHeader
        {
            get { return _systemMenuHeader; }
            set { SetProperty(ref _systemMenuHeader, value); }
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
        
        public bool CanExit
        {
            get { return _canExit; }
            set { SetProperty(ref _canExit, value); }
        }
        
        public string ExitMenuHeader
        {
            get { return _exitMenuHeader; }
            set { SetProperty(ref _exitMenuHeader, value); }
        }
        
        public DelegateCommand<string> SwitchLanguageCommand { get; }
        public DelegateCommand TestLogCommand { get; }
        public DelegateCommand LoginCommand { get; }
        public DelegateCommand SwitchUserCommand { get; }
        public DelegateCommand OpenPermissionConfigCommand { get; }
        public DelegateCommand OpenSettingsCommand { get; }
        public DelegateCommand ExitCommand { get; }

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
            LoginCommand = new DelegateCommand(ExecuteLogin);
            SwitchUserCommand = new DelegateCommand(ExecuteSwitchUser);
            OpenPermissionConfigCommand = new DelegateCommand(ExecuteOpenPermissionConfig, CanOpenPermissionConfig);
            OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings, CanOpenSettings);
            ExitCommand = new DelegateCommand(ExecuteExit, CanExecuteExit);

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

        private void ExecuteLogin()
        {
            // 与SwitchUser功能相同，打开登录窗口
            var loginWindow = new MarkingMachineFeeder.Windows.LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                // 用户登录成功，界面会自动更新
            }
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

        private void ExecuteOpenSettings()
        {
            // 打开设置窗口的逻辑
            _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemInitialized);
            System.Windows.MessageBox.Show("设置功能将在未来版本中实现", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private bool CanOpenSettings()
        {
            // 检查用户是否有权限访问设置 - 使用权限系统检查
            return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
        }

        private bool CanOpenPermissionConfig()
        {
            // 基于权限系统检查用户是否有权访问权限配置
            return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
        }

        private bool CanExecuteExit()
        {
            // 检查用户是否有权限退出应用程序
            return _securityManager.HasPermission(PermissionResources.SystemControl, PermissionActions.Control);
        }

        private void ExecuteExit()
        {
            // 创建并显示自定义确认对话框
            var confirmDialog = new MarkingMachineFeeder.Windows.ConfirmationDialog(
                Ewan.Resources.UIStrings.ExitConfirmTitle,
                Ewan.Resources.UIStrings.ExitConfirmMessage,
                false);

            // 如果用户确认退出
            if (confirmDialog.ShowDialog() == true)
            {
                // 记录当前用户退出应用程序
                var currentUser = _securityManager.CurrentUser?.Username ?? "游客";
                _uiLogger.Info(() => Ewan.Resources.LogMessages.UserExiting, currentUser);
                
                System.Windows.Application.Current.Shutdown();
            }
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
                
                CurrentUser = localizedDisplayName;
                CurrentUserText = string.Format(Ewan.Resources.UIStrings.CurrentUser, $"{localizedDisplayName} [{localizedRoleNames}]");
            }
            else
            {
                // 使用硬编码值，因为UIStrings中没有对应的资源
                CurrentUser = "游客";
                CurrentUserText = Ewan.Resources.UIStrings.NoUserLoggedIn;
            }
            
            // 强制触发PropertyChanged事件
            RaisePropertyChanged(nameof(CurrentUser));
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

        private void UpdateUITexts()
        {
            Title = Ewan.Resources.UIStrings.MainWindowTitle;
            LanguageMenuHeader = Ewan.Resources.UIStrings.LanguageMenuHeader;
            TestLogButtonText = Ewan.Resources.UIStrings.TestLogButton;
            // 使用资源字符串替代硬编码值
            LoginButtonText = Ewan.Resources.UIStrings.LoginButtonText;
            SwitchUserButtonText = Ewan.Resources.UIStrings.SwitchUserButton;
            SystemManagementMenuHeader = Ewan.Resources.UIStrings.SystemManagementMenu;
            PermissionConfigMenuHeader = Ewan.Resources.UIStrings.PermissionConfigMenu;
            // 使用ResourceManager直接获取资源字符串，如果资源不存在则使用默认值
            SettingsMenuHeader = Ewan.Resources.UIStrings.ResourceManager.GetString("SettingsMenu", Ewan.Resources.UIStrings.Culture) ?? "设置";
            SystemMenuHeader = Ewan.Resources.UIStrings.ResourceManager.GetString("SystemMenu", Ewan.Resources.UIStrings.Culture) ?? "系统";
            CurrentUserLabel = Ewan.Resources.UIStrings.ResourceManager.GetString("CurrentUserLabel", Ewan.Resources.UIStrings.Culture) ?? "当前用户：";
            ExitMenuHeader = Ewan.Resources.UIStrings.ExitMenu;
            
            // 强制触发所有相关属性的PropertyChanged事件
            RaisePropertyChanged(nameof(Title));
            RaisePropertyChanged(nameof(LanguageMenuHeader));
            RaisePropertyChanged(nameof(TestLogButtonText));
            RaisePropertyChanged(nameof(LoginButtonText));
            RaisePropertyChanged(nameof(SwitchUserButtonText));
            RaisePropertyChanged(nameof(SystemManagementMenuHeader));
            RaisePropertyChanged(nameof(PermissionConfigMenuHeader));
            RaisePropertyChanged(nameof(SettingsMenuHeader));
            RaisePropertyChanged(nameof(SystemMenuHeader));
            RaisePropertyChanged(nameof(CurrentUserLabel));
            RaisePropertyChanged(nameof(ExitMenuHeader));
        }
        private void UpdatePermissions()
        {
            // 移除Camera和UPS权限检查，因为已经删除了这些权限
            CanControlCamera = false;  // 禁用相机控制
            CanControlUPS = false;     // 禁用UPS控制
            CanViewSettings = _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
            
            // 使用新的Language权限控制语言切换
            CanSwitchLanguage = _securityManager.HasPermission(PermissionResources.Language, PermissionActions.Control);
            
            // 使用SystemControl权限控制退出功能
            CanExit = _securityManager.HasPermission(PermissionResources.SystemControl, PermissionActions.Control);
            
            // 触发属性变更通知，确保UI更新
            RaisePropertyChanged(nameof(CanViewSettings));
            RaisePropertyChanged(nameof(CanSwitchLanguage));
            RaisePropertyChanged(nameof(CanControlCamera));
            RaisePropertyChanged(nameof(CanControlUPS));
            RaisePropertyChanged(nameof(CanExit));
            
            // 刷新依赖权限的命令状态
            OpenPermissionConfigCommand.RaiseCanExecuteChanged();
            OpenSettingsCommand.RaiseCanExecuteChanged();
            ExitCommand.RaiseCanExecuteChanged();
        }
    }
}