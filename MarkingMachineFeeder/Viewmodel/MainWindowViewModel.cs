using Ewan.BusinessBonding;
using Ewan.Core.Culture;
using Ewan.Core.Logger;
using Ewan.Core.Security;
using Ewan.Model.Security;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace MarkingMachineFeeder.Viewmodel
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly CultureManager _cultureManager;
        private readonly SecurityManager _securityManager;
        private readonly SystemControlService _systemControlService;

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
        private string _ioControlMenuHeader = "";
        private string _ioMappingConfigMenuHeader = "";
        private string _axisConfigMenuHeader = "";
        private string _axisControlMenuHeader = "";
        private string _hardwareControlMenuHeader = "";
        private bool _canControlCamera = false;
        private bool _canControlUPS = false;
        private bool _canViewSettings = false;
        private bool _canSwitchLanguage = false;
        private bool _canExit = false;
        private bool _canAccessHardwareControl = false;

        // 窗体实例管理 - 单例模式
        private MarkingMachineFeeder.Windows.IOControlWindow _ioControlWindow;
        private MarkingMachineFeeder.Windows.IOMappingConfigWindow _ioMappingConfigWindow;
        private MarkingMachineFeeder.Windows.AxisConfigWindow _axisConfigWindow;
        private MarkingMachineFeeder.Windows.AxisControlWindow _axisControlWindow;

        // IO状态属性 - 根据io.csv定义的交互信号
        // 输入信号：IN3-IN11, IN15-IN17, IN19-IN20
        private bool _in3_DetectMaterial = false;
        private bool _in4_GripComplete = false;
        private bool _in5_LowerCameraPos = false;
        private bool _in6_PositionComplete = false;
        private bool _in7_MoveToScanArea = false;
        private bool _in8_PlaceComplete = false;
        private bool _in9_Initialize = false;
        private bool _in10_PickComplete = false;
        private bool _in11_InsertCartComplete = false;
        private bool _in15_RobotAlarm = false;
        private bool _in16_UpperCameraAlarm = false;
        private bool _in17_LowerCameraAlarm = false;
        private bool _in19_CylinderAlarm = false;
        private bool _in20_RobotBusy = false;


        // UPH相关属性 - 上料和下料
        private string _loadingUPH = "1,250";
        private string _unloadingUPH = "1,180";

        // A料属性
        private string _materialA_Barcode = "A240912001";
        private string _materialA_Count = "150";
        private string _materialA_Priority = "1";

        // B料属性
        private string _materialB_Barcode = "B240912002";
        private string _materialB_Count = "89";
        private string _materialB_Priority = "2";

        // NG料属性
        private string _materialNG_Barcode = "NG240912003";
        private string _materialNG_Count = "12";
        private string _materialNG_Priority = "3";

        // 系统状态属性
        private string _systemRunningStatus = "Green";
        private string _emergencyStopStatus = "Gray";
        private string _alarmStatus = "Gray";
        private string _pauseStatus = "Gray";
        private string _productionModeColor = "Blue";
        private string _productionModeText = "自动模式";

        // 系统状态布尔属性（用于EwanIO的IsOn绑定）
        private bool _systemRunningIsOn = true;
        private bool _emergencyStopIsOn = false;
        private bool _alarmIsOn = false;
        private bool _pauseIsOn = false;

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
        
        public bool CanAccessHardwareControl
        {
            get { return _canAccessHardwareControl; }
            set { SetProperty(ref _canAccessHardwareControl, value); }
        }
        
        public string IOControlMenuHeader
        {
            get { return _ioControlMenuHeader; }
            set { SetProperty(ref _ioControlMenuHeader, value); }
        }

        public string IOMappingConfigMenuHeader
        {
            get { return _ioMappingConfigMenuHeader; }
            set { SetProperty(ref _ioMappingConfigMenuHeader, value); }
        }

        public string HardwareControlMenuHeader
        {
            get { return _hardwareControlMenuHeader; }
            set { SetProperty(ref _hardwareControlMenuHeader, value); }
        }
        
        public string AxisConfigMenuHeader
        {
            get { return _axisConfigMenuHeader; }
            set { SetProperty(ref _axisConfigMenuHeader, value); }
        }

        public string AxisControlMenuHeader
        {
            get { return _axisControlMenuHeader; }
            set { SetProperty(ref _axisControlMenuHeader, value); }
        }
        
        public DelegateCommand<string> SwitchLanguageCommand { get; }
        public DelegateCommand TestLogCommand { get; }
        public DelegateCommand LoginCommand { get; }
        public DelegateCommand SwitchUserCommand { get; }
        public DelegateCommand OpenPermissionConfigCommand { get; }
        public DelegateCommand OpenSettingsCommand { get; }
        public DelegateCommand OpenIOControlCommand { get; }
        public DelegateCommand OpenIOMappingConfigCommand { get; }
        public DelegateCommand OpenAxisConfigCommand { get; }
        public DelegateCommand OpenAxisControlCommand { get; }
        public DelegateCommand ExitCommand { get; }
        
        // 物料优先级调整命令
        public DelegateCommand MaterialA_IncreasePriorityCommand { get; }
        public DelegateCommand MaterialB_IncreasePriorityCommand { get; }
        public DelegateCommand MaterialNG_IncreasePriorityCommand { get; }
        
        // 物料清除命令
        public DelegateCommand MaterialA_ClearCommand { get; }
        public DelegateCommand MaterialB_ClearCommand { get; }
        public DelegateCommand MaterialNG_ClearCommand { get; }
        
        // 系统控制命令
        public DelegateCommand SystemResetCommand { get; }
        public DelegateCommand EmergencyStopCommand { get; }
        public DelegateCommand ClearAlarmCommand { get; }
        public DelegateCommand SystemStartCommand { get; }
        public DelegateCommand SystemPauseCommand { get; }
        public DelegateCommand SystemStopCommand { get; }
        
        // 机械手手动控制命令 - 对应OUT11-OUT13, OUT17
        public DelegateCommand OUT11_Bin1SelectCommand { get; }
        public DelegateCommand OUT12_Bin2SelectCommand { get; }
        public DelegateCommand OUT13_Bin3SelectCommand { get; }
        public DelegateCommand OUT17_InsertCartCommand { get; }
        
        // 新增机械手操作命令 - 对应Y10,Y11,Y12,Y13,Y14,Y15,Y17
        public DelegateCommand Y14_GrabToScanCommand { get; }       // 抓上物料皮带到扫码区
        public DelegateCommand Y10_PlaceToBin1Command { get; }      // 放置到料仓1 (Y10+Y11)
        public DelegateCommand Y10_PlaceToBin2Command { get; }      // 放置到料仓2 (Y10+Y12)
        public DelegateCommand Y10_PlaceToBin3Command { get; }      // 放置到料仓3 (Y10+Y13)
        public DelegateCommand Y15_PickFromBin1Command { get; }     // 从料仓1取料到扫码区 (Y15+Y11)
        public DelegateCommand Y15_PickFromBin2Command { get; }     // 从料仓2取料到扫码区 (Y15+Y12)
        public DelegateCommand Y15_PickFromBin3Command { get; }     // 从料仓3取料到扫码区 (Y15+Y13)
        public DelegateCommand Y17_PlaceToCartCommand { get; }      // 放入小车

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

            _systemControlService = SystemControlService.Instance();

            SwitchLanguageCommand = new DelegateCommand<string>(ExecuteSwitchLanguage);
            TestLogCommand = new DelegateCommand(ExecuteTestLog);
            LoginCommand = new DelegateCommand(ExecuteLogin);
            SwitchUserCommand = new DelegateCommand(ExecuteSwitchUser);
            OpenPermissionConfigCommand = new DelegateCommand(ExecuteOpenPermissionConfig, CanOpenPermissionConfig);
            OpenSettingsCommand = new DelegateCommand(ExecuteOpenSettings, CanOpenSettings);
            OpenIOControlCommand = new DelegateCommand(ExecuteOpenIOControl, CanOpenIOControl);
            OpenIOMappingConfigCommand = new DelegateCommand(ExecuteOpenIOMappingConfig, CanOpenIOMappingConfig);
            OpenAxisConfigCommand = new DelegateCommand(ExecuteOpenAxisConfig, CanOpenAxisConfig);
            OpenAxisControlCommand = new DelegateCommand(ExecuteOpenAxisControl, CanOpenAxisControl);
            ExitCommand = new DelegateCommand(ExecuteExit, CanExecuteExit);
            
            // 初始化物料优先级调整命令
            MaterialA_IncreasePriorityCommand = new DelegateCommand(ExecuteMaterialA_IncreasePriority);
            MaterialB_IncreasePriorityCommand = new DelegateCommand(ExecuteMaterialB_IncreasePriority);
            MaterialNG_IncreasePriorityCommand = new DelegateCommand(ExecuteMaterialNG_IncreasePriority);
            
            // 初始化物料清除命令
            MaterialA_ClearCommand = new DelegateCommand(ExecuteMaterialA_Clear);
            MaterialB_ClearCommand = new DelegateCommand(ExecuteMaterialB_Clear);
            MaterialNG_ClearCommand = new DelegateCommand(ExecuteMaterialNG_Clear);
            
            // 初始化系统控制命令
            SystemResetCommand = new DelegateCommand(ExecuteSystemReset);
            EmergencyStopCommand = new DelegateCommand(ExecuteEmergencyStop);
            ClearAlarmCommand = new DelegateCommand(ExecuteClearAlarm);
            SystemStartCommand = new DelegateCommand(ExecuteSystemStart);
            SystemPauseCommand = new DelegateCommand(ExecuteSystemPause);
            SystemStopCommand = new DelegateCommand(ExecuteSystemStop);
            
            // 初始化机械手手动控制命令
            OUT11_Bin1SelectCommand = new DelegateCommand(ExecuteOUT11_Bin1Select);
            OUT12_Bin2SelectCommand = new DelegateCommand(ExecuteOUT12_Bin2Select);
            OUT13_Bin3SelectCommand = new DelegateCommand(ExecuteOUT13_Bin3Select);
            OUT17_InsertCartCommand = new DelegateCommand(ExecuteOUT17_InsertCart);
            
            // 初始化新增机械手操作命令
            Y14_GrabToScanCommand = new DelegateCommand(ExecuteY14_GrabToScan);
            Y10_PlaceToBin1Command = new DelegateCommand(ExecuteY10_PlaceToBin1);
            Y10_PlaceToBin2Command = new DelegateCommand(ExecuteY10_PlaceToBin2);
            Y10_PlaceToBin3Command = new DelegateCommand(ExecuteY10_PlaceToBin3);
            Y15_PickFromBin1Command = new DelegateCommand(ExecuteY15_PickFromBin1);
            Y15_PickFromBin2Command = new DelegateCommand(ExecuteY15_PickFromBin2);
            Y15_PickFromBin3Command = new DelegateCommand(ExecuteY15_PickFromBin3);
            Y17_PlaceToCartCommand = new DelegateCommand(ExecuteY17_PlaceToCart);

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

        private void ExecuteOpenIOControl()
        {
            // 单例模式：检查是否已存在IO控制窗口
            if (_ioControlWindow == null || !_ioControlWindow.IsLoaded)
            {
                _ioControlWindow = new MarkingMachineFeeder.Windows.IOControlWindow();
                _ioControlWindow.Closed += (s, e) => _ioControlWindow = null; // 窗口关闭时清除引用
                _ioControlWindow.Show();
            }
            else
            {
                // 如果窗口已存在，激活并置顶
                _ioControlWindow.Activate();
                _ioControlWindow.Focus();
            }
        }

        private void ExecuteOpenIOMappingConfig()
        {
            // 单例模式：检查是否已存在IO映射配置窗口
            if (_ioMappingConfigWindow == null || !_ioMappingConfigWindow.IsLoaded)
            {
                _ioMappingConfigWindow = new MarkingMachineFeeder.Windows.IOMappingConfigWindow();
                _ioMappingConfigWindow.Closed += (s, e) => _ioMappingConfigWindow = null; // 窗口关闭时清除引用
                _ioMappingConfigWindow.Show();
            }
            else
            {
                // 如果窗口已存在，激活并置顶
                _ioMappingConfigWindow.Activate();
                _ioMappingConfigWindow.Focus();
            }
        }

        private bool CanOpenSettings()
        {
            // 检查用户是否有权限访问设置 - 使用权限系统检查
            return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
        }

        private bool CanOpenIOControl()
        {
            // 检查用户是否有权限访问硬件控制
            return _securityManager.HasPermission(PermissionResources.HardwareControl, PermissionActions.Control);
        }

        private bool CanOpenIOMappingConfig()
        {
            // 检查用户是否有权限访问IO映射配置
            return _securityManager.HasPermission(PermissionResources.HardwareControl, PermissionActions.Control);
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
            IOControlMenuHeader = Ewan.Resources.UIStrings.IOControlMenu;
            IOMappingConfigMenuHeader = Ewan.Resources.UIStrings.IOMappingConfigMenu;
            AxisConfigMenuHeader = Ewan.Resources.UIStrings.AxisConfigMenu;
            AxisControlMenuHeader = "轴手动控制"; // Using fallback text since AxisControlMenu property is not generating properly
            HardwareControlMenuHeader = Ewan.Resources.UIStrings.HardwareControlMenu;
            
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
            RaisePropertyChanged(nameof(IOControlMenuHeader));
            RaisePropertyChanged(nameof(IOMappingConfigMenuHeader));
            RaisePropertyChanged(nameof(AxisConfigMenuHeader));
            RaisePropertyChanged(nameof(HardwareControlMenuHeader));
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
            
            // 使用HardwareControl权限控制硬件功能访问
            CanAccessHardwareControl = _securityManager.HasPermission(PermissionResources.HardwareControl, PermissionActions.Control);
            
            // 触发属性变更通知，确保UI更新
            RaisePropertyChanged(nameof(CanViewSettings));
            RaisePropertyChanged(nameof(CanSwitchLanguage));
            RaisePropertyChanged(nameof(CanControlCamera));
            RaisePropertyChanged(nameof(CanControlUPS));
            RaisePropertyChanged(nameof(CanExit));
            RaisePropertyChanged(nameof(CanAccessHardwareControl));
            
            // 刷新依赖权限的命令状态
            OpenPermissionConfigCommand.RaiseCanExecuteChanged();
            OpenSettingsCommand.RaiseCanExecuteChanged();
            ExitCommand.RaiseCanExecuteChanged();
            OpenIOControlCommand.RaiseCanExecuteChanged();
            OpenIOMappingConfigCommand.RaiseCanExecuteChanged();
            OpenAxisConfigCommand.RaiseCanExecuteChanged();
            OpenAxisControlCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteOpenAxisConfig()
        {
            try
            {
                // 单例模式：检查是否已存在轴配置窗口
                if (_axisConfigWindow == null || !_axisConfigWindow.IsLoaded)
                {
                    _axisConfigWindow = new MarkingMachineFeeder.Windows.AxisConfigWindow();
                    _axisConfigWindow.Closed += (s, e) => _axisConfigWindow = null; // 窗口关闭时清除引用
                    _axisConfigWindow.Show();
                }
                else
                {
                    // 如果窗口已存在，激活并置顶
                    _axisConfigWindow.Activate();
                    _axisConfigWindow.Focus();
                }
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingComplete);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "轴配置", ex.Message);
            }
        }

        private void ExecuteOpenAxisControl()
        {
            try
            {
                // 单例模式：检查是否已存在轴手动控制窗口
                if (_axisControlWindow == null || !_axisControlWindow.IsLoaded)
                {
                    _axisControlWindow = new MarkingMachineFeeder.Windows.AxisControlWindow();
                    _axisControlWindow.Closed += (s, e) => _axisControlWindow = null; // 窗口关闭时清除引用
                    _axisControlWindow.Show();
                }
                else
                {
                    // 如果窗口已存在，激活并置顶
                    _axisControlWindow.Activate();
                    _axisControlWindow.Focus();
                }
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingComplete, "轴手动控制");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "轴手动控制", ex.Message);
            }
        }

        private bool CanOpenAxisConfig()
        {
            // 检查用户是否有权限访问轴配置 - 使用权限配置的查看权限
            return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
        }

        private bool CanOpenAxisControl()
        {
            // 检查用户是否有权限访问轴手动控制 - 使用权限配置的查看权限
            return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
        }

        #region 交互IO状态属性 - 根据io.csv定义

        // IN3 - 检测到料片信号
        public bool IN3_DetectMaterial
        {
            get => _in3_DetectMaterial;
            set => SetProperty(ref _in3_DetectMaterial, value);
        }

        // IN4 - 抓取完成信号
        public bool IN4_GripComplete
        {
            get => _in4_GripComplete;
            set => SetProperty(ref _in4_GripComplete, value);
        }

        // IN5 - 下相机精确定位完成信号
        public bool IN5_LowerCameraPos
        {
            get => _in5_LowerCameraPos;
            set => SetProperty(ref _in5_LowerCameraPos, value);
        }

        // IN6 - 定位完成信号
        public bool IN6_PositionComplete
        {
            get => _in6_PositionComplete;
            set => SetProperty(ref _in6_PositionComplete, value);
        }

        // IN7 - 移至扫码区到位信号
        public bool IN7_MoveToScanArea
        {
            get => _in7_MoveToScanArea;
            set => SetProperty(ref _in7_MoveToScanArea, value);
        }

        // IN8 - 放置完成反馈信号
        public bool IN8_PlaceComplete
        {
            get => _in8_PlaceComplete;
            set => SetProperty(ref _in8_PlaceComplete, value);
        }

        // IN9 - 初始化信号
        public bool IN9_Initialize
        {
            get => _in9_Initialize;
            set => SetProperty(ref _in9_Initialize, value);
        }

        // IN10 - 取料完成反馈信号
        public bool IN10_PickComplete
        {
            get => _in10_PickComplete;
            set => SetProperty(ref _in10_PickComplete, value);
        }

        // IN11 - 插入小车完成反馈信号
        public bool IN11_InsertCartComplete
        {
            get => _in11_InsertCartComplete;
            set => SetProperty(ref _in11_InsertCartComplete, value);
        }

        // IN15 - 机械手报警信号
        public bool IN15_RobotAlarm
        {
            get => _in15_RobotAlarm;
            set => SetProperty(ref _in15_RobotAlarm, value);
        }

        // IN16 - 上相机报警信号
        public bool IN16_UpperCameraAlarm
        {
            get => _in16_UpperCameraAlarm;
            set => SetProperty(ref _in16_UpperCameraAlarm, value);
        }

        // IN17 - 下相机报警信号
        public bool IN17_LowerCameraAlarm
        {
            get => _in17_LowerCameraAlarm;
            set => SetProperty(ref _in17_LowerCameraAlarm, value);
        }

        // IN19 - 气缸报警信号
        public bool IN19_CylinderAlarm
        {
            get => _in19_CylinderAlarm;
            set => SetProperty(ref _in19_CylinderAlarm, value);
        }

        // IN20 - 机械手忙碌状态信号
        public bool IN20_RobotBusy
        {
            get => _in20_RobotBusy;
            set => SetProperty(ref _in20_RobotBusy, value);
        }


        #endregion
        
        #region UPH相关属性
        
        public string LoadingUPH
        {
            get => _loadingUPH;
            set => SetProperty(ref _loadingUPH, value);
        }
        
        public string UnloadingUPH
        {
            get => _unloadingUPH;
            set => SetProperty(ref _unloadingUPH, value);
        }
        
        #endregion
        
        #region 物料相关属性
        
        // A料属性
        public string MaterialA_Barcode
        {
            get => _materialA_Barcode;
            set => SetProperty(ref _materialA_Barcode, value);
        }
        
        public string MaterialA_Count
        {
            get => _materialA_Count;
            set => SetProperty(ref _materialA_Count, value);
        }
        
        public string MaterialA_Priority
        {
            get => _materialA_Priority;
            set => SetProperty(ref _materialA_Priority, value);
        }
        
        // B料属性
        public string MaterialB_Barcode
        {
            get => _materialB_Barcode;
            set => SetProperty(ref _materialB_Barcode, value);
        }
        
        public string MaterialB_Count
        {
            get => _materialB_Count;
            set => SetProperty(ref _materialB_Count, value);
        }
        
        public string MaterialB_Priority
        {
            get => _materialB_Priority;
            set => SetProperty(ref _materialB_Priority, value);
        }
        
        // NG料属性
        public string MaterialNG_Barcode
        {
            get => _materialNG_Barcode;
            set => SetProperty(ref _materialNG_Barcode, value);
        }
        
        public string MaterialNG_Count
        {
            get => _materialNG_Count;
            set => SetProperty(ref _materialNG_Count, value);
        }
        
        public string MaterialNG_Priority
        {
            get => _materialNG_Priority;
            set => SetProperty(ref _materialNG_Priority, value);
        }
        
        #endregion
        
        #region 系统状态属性
        
        public string SystemRunningStatus
        {
            get => _systemRunningStatus;
            set => SetProperty(ref _systemRunningStatus, value);
        }
        
        public string EmergencyStopStatus
        {
            get => _emergencyStopStatus;
            set => SetProperty(ref _emergencyStopStatus, value);
        }
        
        public string AlarmStatus
        {
            get => _alarmStatus;
            set => SetProperty(ref _alarmStatus, value);
        }
        
        public string PauseStatus
        {
            get => _pauseStatus;
            set => SetProperty(ref _pauseStatus, value);
        }
        
        public string ProductionModeColor
        {
            get => _productionModeColor;
            set => SetProperty(ref _productionModeColor, value);
        }
        
        public string ProductionModeText
        {
            get => _productionModeText;
            set => SetProperty(ref _productionModeText, value);
        }
        
        // 系统状态布尔属性
        public bool SystemRunningIsOn
        {
            get => _systemRunningIsOn;
            set => SetProperty(ref _systemRunningIsOn, value);
        }
        
        public bool EmergencyStopIsOn
        {
            get => _emergencyStopIsOn;
            set => SetProperty(ref _emergencyStopIsOn, value);
        }
        
        public bool AlarmIsOn
        {
            get => _alarmIsOn;
            set => SetProperty(ref _alarmIsOn, value);
        }
        
        public bool PauseIsOn
        {
            get => _pauseIsOn;
            set => SetProperty(ref _pauseIsOn, value);
        }
        
        #endregion
        
        #region 物料清除命令方法
        
        private void ExecuteMaterialA_Clear()
        {
            MaterialA_Barcode = "";
            MaterialA_Count = "0";
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }
        
        private void ExecuteMaterialB_Clear()
        {
            MaterialB_Barcode = "";
            MaterialB_Count = "0";
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }
        
        private void ExecuteMaterialNG_Clear()
        {
            MaterialNG_Barcode = "";
            MaterialNG_Count = "0";
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }
        
        #endregion
        
        #region 物料优先级调整命令方法
        
        private void ExecuteMaterialA_IncreasePriority()
        {
            if (int.TryParse(MaterialA_Priority, out int currentPriority) && currentPriority > 1)
            {
                MaterialA_Priority = (currentPriority - 1).ToString();
                _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
            }
        }
        
        private void ExecuteMaterialB_IncreasePriority()
        {
            if (int.TryParse(MaterialB_Priority, out int currentPriority) && currentPriority > 1)
            {
                MaterialB_Priority = (currentPriority - 1).ToString();
                _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
            }
        }
        
        private void ExecuteMaterialNG_IncreasePriority()
        {
            if (int.TryParse(MaterialNG_Priority, out int currentPriority) && currentPriority > 1)
            {
                MaterialNG_Priority = (currentPriority - 1).ToString();
                _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
            }
        }
        
        #endregion
        
        #region 系统控制命令方法
        
        private void ExecuteSystemReset()
        {
            // 复位系统状态
            SystemRunningStatus = "Green";
            SystemRunningIsOn = true;
            EmergencyStopStatus = "Gray";
            EmergencyStopIsOn = false;
            AlarmStatus = "Gray";
            AlarmIsOn = false;
            PauseStatus = "Gray";
            PauseIsOn = false;
            ProductionModeText = "自动模式";
            ProductionModeColor = "Blue";
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }
        
        private void ExecuteEmergencyStop()
        {
            try
            {
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingCompleted, "用户触发紧急停止");
                
                // 调用系统控制服务紧急停止
                _systemControlService.EmergencyStopSystem();
                
                // 更新界面状态
                SystemRunningStatus = "Red";
                SystemRunningIsOn = false;
                EmergencyStopStatus = "Red";
                EmergencyStopIsOn = true;
                PauseStatus = "Gray";
                PauseIsOn = false;
                ProductionModeText = "急停状态";
                ProductionModeColor = "Red";
                
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingCompleted, "紧急停止操作完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "紧急停止操作", ex.Message);
            }
        }
        
        private void ExecuteClearAlarm()
        {
            // 清除报警
            AlarmStatus = "Gray";
            AlarmIsOn = false;
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked);
        }
        
        private void ExecuteSystemStart()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "用户请求启动系统");
                
                // 调用系统控制服务启动系统
                _systemControlService.StartSystem();
                
                // 更新界面状态
                SystemRunningStatus = "Green";
                SystemRunningIsOn = true;
                EmergencyStopIsOn = false;
                EmergencyStopStatus = "Gray";
                PauseStatus = "Gray";
                PauseIsOn = false;
                ProductionModeText = "运行中";
                ProductionModeColor = "Green";
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统启动操作完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "系统启动操作", ex.Message);
            }
        }
        
        private void ExecuteSystemPause()
        {
            // 暂停系统
            SystemRunningStatus = "Yellow";
            SystemRunningIsOn = true;
            PauseStatus = "Yellow";
            PauseIsOn = true;
            ProductionModeText = "暂停中";
            ProductionModeColor = "Orange";
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked);
        }
        
        private void ExecuteSystemStop()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "用户请求停止系统");
                
                // 调用系统控制服务停止系统
                _systemControlService.StopSystem();
                
                // 更新界面状态
                SystemRunningStatus = "Gray";
                SystemRunningIsOn = false;
                EmergencyStopIsOn = false;
                EmergencyStopStatus = "Gray";
                PauseStatus = "Gray";
                PauseIsOn = false;
                ProductionModeText = "已停止";
                ProductionModeColor = "Gray";
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统停止操作完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "系统停止操作", ex.Message);
            }
        }
        
        #endregion
        
        #region 机械手手动控制命令方法

        private void ExecuteOUT11_Bin1Select()
        {
            // OUT11 - 料仓1选择信号
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }

        private void ExecuteOUT12_Bin2Select()
        {
            // OUT12 - 料仓2选择信号
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }

        private void ExecuteOUT13_Bin3Select()
        {
            // OUT13 - 料仓3选择信号
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }

        private void ExecuteOUT17_InsertCart()
        {
            // OUT17 - 发送插入小车指令
            _uiLogger.Info(() => Ewan.Resources.LogMessages.TestLogClicked); // 使用现有的日志消息作为占位符
        }

        #endregion

        #region 新增机械手操作命令方法

        private void ExecuteY14_GrabToScan()
        {
            // Y14 - 抓取上料皮带物料到扫码区
            _uiLogger.Info(() => "机械手执行: 抓取上料皮带物料到扫码区 (Y14)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y14", true);
        }

        private void ExecuteY10_PlaceToBin1()
        {
            // Y10+Y11 - 将物料放置到料仓1
            _uiLogger.Info(() => "机械手执行: 将物料放置到料仓1 (Y10+Y11)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y11", true);  // 先选择料仓1
            // ioManager.SetOutput("Y12", false);
            // ioManager.SetOutput("Y13", false);
            // ioManager.SetOutput("Y10", true);  // 执行放置动作
        }

        private void ExecuteY10_PlaceToBin2()
        {
            // Y10+Y12 - 将物料放置到料仓2
            _uiLogger.Info(() => "机械手执行: 将物料放置到料仓2 (Y10+Y12)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y11", false);
            // ioManager.SetOutput("Y12", true);  // 先选择料仓2
            // ioManager.SetOutput("Y13", false);
            // ioManager.SetOutput("Y10", true);  // 执行放置动作
        }

        private void ExecuteY10_PlaceToBin3()
        {
            // Y10+Y13 - 将物料放置到料仓3
            _uiLogger.Info(() => "机械手执行: 将物料放置到料仓3 (Y10+Y13)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y11", false);
            // ioManager.SetOutput("Y12", false);
            // ioManager.SetOutput("Y13", true);  // 先选择料仓3
            // ioManager.SetOutput("Y10", true);  // 执行放置动作
        }

        private void ExecuteY15_PickFromBin1()
        {
            // Y15+Y11 - 从料仓1取料到扫码区
            _uiLogger.Info(() => "机械手执行: 从料仓1取料到扫码区 (Y15+Y11)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y11", true);  // 先选择料仓1
            // ioManager.SetOutput("Y12", false);
            // ioManager.SetOutput("Y13", false);
            // ioManager.SetOutput("Y15", true);  // 执行取料动作
        }

        private void ExecuteY15_PickFromBin2()
        {
            // Y15+Y12 - 从料仓2取料到扫码区
            _uiLogger.Info(() => "机械手执行: 从料仓2取料到扫码区 (Y15+Y12)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y11", false);
            // ioManager.SetOutput("Y12", true);  // 先选择料仓2
            // ioManager.SetOutput("Y13", false);
            // ioManager.SetOutput("Y15", true);  // 执行取料动作
        }

        private void ExecuteY15_PickFromBin3()
        {
            // Y15+Y13 - 从料仓3取料到扫码区
            _uiLogger.Info(() => "机械手执行: 从料仓3取料到扫码区 (Y15+Y13)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y11", false);
            // ioManager.SetOutput("Y12", false);
            // ioManager.SetOutput("Y13", true);  // 先选择料仓3
            // ioManager.SetOutput("Y15", true);  // 执行取料动作
        }

        private void ExecuteY17_PlaceToCart()
        {
            // Y17 - 将物料放入小车
            _uiLogger.Info(() => "机械手执行: 将物料放入小车 (Y17)");
            
            // TODO: 实际的IO控制逻辑
            // var ioManager = LayeredIOManager.Instance();
            // ioManager.SetOutput("Y17", true);
        }

        #endregion
    }
}