using Ewan.Core.Logger;
using Ewan.Core.Culture;
using Ewan.Core.Security;
using Ewan.Model.Permission;
using Ewan.Model.Security;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using Newtonsoft.Json;

namespace MarkingMachineFeeder.Viewmodel
{
    /// <summary>
    /// 权限项视图模型
    /// </summary>
    public class PermissionItemViewModel : BindableBase
    {
        private bool _isGranted;
        private bool _isEditable = true;

        public string PermissionId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string LocalizedDisplayName { get; set; }
        public string LocalizedDescription { get; set; }

        public bool IsGranted
        {
            get => _isGranted;
            set => SetProperty(ref _isGranted, value);
        }

        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value);
        }
    }

    /// <summary>
    /// 权限分类视图模型
    /// </summary>
    public class PermissionCategoryViewModel : BindableBase
    {
        public string CategoryName { get; set; }
        public string LocalizedCategoryName { get; set; }
        public ObservableCollection<PermissionItemViewModel> Permissions { get; set; }

        public PermissionCategoryViewModel()
        {
            Permissions = new ObservableCollection<PermissionItemViewModel>();
        }
    }

    /// <summary>
    /// 权限配置窗口视图模型
    /// </summary>
    public class PermissionConfigViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly CultureManager _cultureManager;
        private readonly SecurityManager _securityManager;
        private PermissionConfiguration _configuration;
        
        private ObservableCollection<RolePermissionConfig> _roles;
        private ObservableCollection<PermissionCategoryViewModel> _permissionCategories;
        
        private RolePermissionConfig _selectedRole;
        private string _currentConfigName;
        private string _currentConfigDescription;
        
        // UI Text Properties
        private string _windowTitle;
        private string _windowDescription;
        private string _roleListTitle;
        private string _saveButtonText;
        private string _cancelButtonText;
        private string _currentConfigDisplayText;
        
        public bool DialogResult { get; private set; }
        public event EventHandler CloseRequested;

        #region Properties

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public string WindowDescription
        {
            get => _windowDescription;
            set => SetProperty(ref _windowDescription, value);
        }

        public string RoleListTitle
        {
            get => _roleListTitle;
            set => SetProperty(ref _roleListTitle, value);
        }


        public string SaveButtonText
        {
            get => _saveButtonText;
            set => SetProperty(ref _saveButtonText, value);
        }

        public string CancelButtonText
        {
            get => _cancelButtonText;
            set => SetProperty(ref _cancelButtonText, value);
        }

        public string CurrentConfigDisplayText
        {
            get => _currentConfigDisplayText;
            set => SetProperty(ref _currentConfigDisplayText, value);
        }

        public ObservableCollection<RolePermissionConfig> Roles
        {
            get => _roles;
            set => SetProperty(ref _roles, value);
        }


        public ObservableCollection<PermissionCategoryViewModel> PermissionCategories
        {
            get => _permissionCategories;
            set => SetProperty(ref _permissionCategories, value);
        }

        public RolePermissionConfig SelectedRole
        {
            get => _selectedRole;
            set
            {
                SetProperty(ref _selectedRole, value);
                if (value != null)
                {
                    LoadRolePermissions(value);
                }
            }
        }

        public string CurrentConfigName
        {
            get => _currentConfigName;
            set => SetProperty(ref _currentConfigName, value);
        }

        public string CurrentConfigDescription
        {
            get => _currentConfigDescription;
            set => SetProperty(ref _currentConfigDescription, value);
        }

        #endregion

        #region Commands

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }
        public DelegateCommand CloseCommand { get; }

        #endregion

        public PermissionConfigViewModel()
        {
            // 初始化SecurityManager
            _securityManager = SecurityManager.Instance();
            
            // 监听用户认证状态变化
            _securityManager.UserAuthenticated += OnUserAuthenticated;
            _securityManager.UserLoggedOut += OnUserLoggedOut;
            
            // 初始化CultureManager
            _cultureManager = CultureManager.Instance();
            _cultureManager.CultureChanged += OnCultureChanged;
            
            // 确保UIStrings的Culture同步
            Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
            
            SaveCommand = new DelegateCommand(ExecuteSave, CanExecutePermissionControl);
            CancelCommand = new DelegateCommand(ExecuteCancel);
            CloseCommand = new DelegateCommand(ExecuteCancel); // 关闭按钮执行取消操作

            // 初始化数据
            InitializeData();
            
            // 初始化UI文本
            UpdateUITexts();
            
            // 刷新命令权限状态
            RefreshCommandStates();
            
        }

        /// <summary>
        /// 用户认证成功事件处理
        /// </summary>
        private void OnUserAuthenticated(object sender, User user)
        {
            RefreshCommandStates();
        }

        /// <summary>
        /// 用户注销事件处理
        /// </summary>
        private void OnUserLoggedOut(object sender, EventArgs e)
        {
            RefreshCommandStates();
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            // 同步UIStrings和PermissionConfigStrings的Culture设置
            Ewan.Resources.UIStrings.Culture = e.NewCulture;
            Ewan.Resources.PermissionConfigStrings.Culture = e.NewCulture;
            UpdateUITexts();
            
            // 更新角色本地化名称
            foreach (var role in Roles)
            {
                role.LocalizedRoleDisplayName = GetLocalizedRoleName(role.RoleName);
            }
            
            // 刷新权限分类显示
            if (PermissionCategories != null)
            {
                LoadPermissionCategories();
                
                // 重新加载当前选中项的权限
                if (SelectedRole != null)
                {
                    LoadRolePermissions(SelectedRole);
                }
            }
            
            // 刷新命令权限状态
            RefreshCommandStates();
        }

        private void UpdateUITexts()
        {
            // 确保资源文件使用正确的语言
            Ewan.Resources.PermissionConfigStrings.Culture = _cultureManager.CurrentCulture;
            
            WindowTitle = Ewan.Resources.PermissionConfigStrings.WindowTitle;
            WindowDescription = Ewan.Resources.PermissionConfigStrings.WindowDescription;
            RoleListTitle = Ewan.Resources.PermissionConfigStrings.RoleListTitle;
            SaveButtonText = Ewan.Resources.PermissionConfigStrings.SaveButton;
            CancelButtonText = Ewan.Resources.PermissionConfigStrings.CancelButton;
            
            // Update CurrentConfigDisplayText if it's set
            if (!string.IsNullOrEmpty(CurrentConfigName))
            {
                CurrentConfigDisplayText = string.Format(Ewan.Resources.PermissionConfigStrings.CurrentConfig, CurrentConfigName);
            }
        }

        /// <summary>
        /// 刷新所有命令的权限状态
        /// </summary>
        private void RefreshCommandStates()
        {
            SaveCommand.RaiseCanExecuteChanged();
            
        }

        private void InitializeData()
        {
            // 从SecurityManager加载实际用户和角色数据
            LoadFromSecurityManager();
            
            // 初始化集合
            PermissionCategories = new ObservableCollection<PermissionCategoryViewModel>();
            
            // 加载权限分类
            LoadPermissionCategories();
            
            // 默认选中第一个角色
            if (Roles.Count > 0)
            {
                SelectedRole = Roles[0];
            }
        }

        private void LoadFromSecurityManager()
        {
            // 创建配置对象
            _configuration = new PermissionConfiguration();
            
            // 从SecurityManager获取所有角色
            var allRoles = _securityManager.GetAllRoles();
            var roleConfigs = new List<RolePermissionConfig>();
            
            foreach (var role in allRoles)
            {
                var roleConfig = new RolePermissionConfig
                {
                    RoleName = role.Name,
                    RoleDisplayName = role.DisplayName,
                    LocalizedRoleDisplayName = GetLocalizedRoleName(role.Name),
                    IsSystemRole = IsSystemRole(role.Name),
                    PermissionIds = role.Permissions.Select(p => $"{p.Resource.ToLower()}.{p.Action.ToLower()}").ToList()
                };
                roleConfigs.Add(roleConfig);
            }
            
            _configuration.RolePermissions = roleConfigs;
            Roles = new ObservableCollection<RolePermissionConfig>(roleConfigs);
            
            // 定义系统权限结构（这部分保持不变，用于显示权限分类）
            _configuration.Permissions = CreateSystemPermissions();
        }

        private bool IsSystemRole(string roleName)
        {
            return roleName == RoleNames.Administrator || 
                   roleName == RoleNames.Engineer || 
                   roleName == RoleNames.Operator;
        }

        private List<PermissionConfig> CreateSystemPermissions()
        {
            return new List<PermissionConfig>
            {
                // 语言切换权限
                new PermissionConfig { PermissionId = "language.control", DisplayName = "LanguageSwitch", Description = "LanguageSwitchDesc", Category = "SystemSettings" },
                
                // 权限配置权限
                new PermissionConfig { PermissionId = "permissionconfig.view", DisplayName = "PermissionConfigView", Description = "PermissionConfigViewDesc", Category = "SystemSettings" },
                new PermissionConfig { PermissionId = "permissionconfig.control", DisplayName = "PermissionConfigControl", Description = "PermissionConfigControlDesc", Category = "SystemSettings" },
                
                // 系统控制权限（包括退出应用程序）
                new PermissionConfig { PermissionId = "systemcontrol.control", DisplayName = "SystemControl", Description = "SystemControlDesc", Category = "SystemControl" },
            };
        }

        private PermissionConfiguration CreateDefaultConfiguration()
        {
            var config = new PermissionConfiguration();
            
            // 定义系统权限
            config.Permissions = new List<PermissionConfig>
            {
                // 语言切换权限
                new PermissionConfig { PermissionId = "language.control", DisplayName = "LanguageSwitch", Description = "LanguageSwitchDesc", Category = "SystemSettings" },
                
                // 权限配置权限
                new PermissionConfig { PermissionId = "permissionconfig.view", DisplayName = "PermissionConfigView", Description = "PermissionConfigViewDesc", Category = "SystemSettings" },
                new PermissionConfig { PermissionId = "permissionconfig.control", DisplayName = "PermissionConfigControl", Description = "PermissionConfigControlDesc", Category = "SystemSettings" },
            };
            
            // 定义角色权限
            config.RolePermissions = new List<RolePermissionConfig>
            {
                new RolePermissionConfig
                {
                    RoleName = "Administrator",
                    RoleDisplayName = "管理员",
                    LocalizedRoleDisplayName = GetLocalizedRoleName("Administrator"),
                    IsSystemRole = true,
                    PermissionIds = config.Permissions.Select(p => p.PermissionId).ToList() // 管理员拥有所有权限
                },
                new RolePermissionConfig
                {
                    RoleName = "Engineer",
                    RoleDisplayName = "工程师",
                    LocalizedRoleDisplayName = GetLocalizedRoleName("Engineer"),
                    IsSystemRole = true,
                    PermissionIds = new List<string>
                    {
                        "language.control",
                        "permissionconfig.view"
                    }
                },
                new RolePermissionConfig
                {
                    RoleName = "Operator",
                    RoleDisplayName = "操作员",
                    LocalizedRoleDisplayName = GetLocalizedRoleName("Operator"),
                    IsSystemRole = true,
                    PermissionIds = new List<string>
                    {
                        "language.control"
                    }
                }
            };
            
            return config;
        }

        private void LoadPermissionCategories()
        {
            PermissionCategories.Clear();
            
            var categories = _configuration.Permissions
                .GroupBy(p => p.Category)
                .Select(g => new PermissionCategoryViewModel
                {
                    CategoryName = g.Key,
                    LocalizedCategoryName = GetLocalizedCategoryName(g.Key),
                    Permissions = new ObservableCollection<PermissionItemViewModel>(
                        g.Select(p => new PermissionItemViewModel
                        {
                            PermissionId = p.PermissionId,
                            DisplayName = p.DisplayName,
                            Description = p.Description,
                            LocalizedDisplayName = GetLocalizedPermissionName(p.DisplayName),
                            LocalizedDescription = GetLocalizedPermissionDescription(p.Description),
                            Category = p.Category,
                            IsGranted = false,
                            IsEditable = true
                        }))
                });
            
            foreach (var category in categories)
            {
                PermissionCategories.Add(category);
            }
        }

        private void LoadRolePermissions(RolePermissionConfig role)
        {
            CurrentConfigName = role.LocalizedRoleDisplayName ?? GetLocalizedRoleName(role.RoleName);
            
            CurrentConfigDescription = Ewan.Resources.PermissionConfigStrings.RolePermissionConfiguration;
            CurrentConfigDisplayText = string.Format(Ewan.Resources.PermissionConfigStrings.CurrentConfig, CurrentConfigName);
            
            // 更新权限勾选状态
            foreach (var category in PermissionCategories)
            {
                foreach (var permission in category.Permissions)
                {
                    permission.IsGranted = role.PermissionIds.Contains(permission.PermissionId);
                    permission.IsEditable = !role.IsSystemRole || role.RoleName != "Administrator";
                }
            }
        }


        #region Command Implementations


        /// <summary>
        /// 检查用户是否有权限控制权限配置
        /// </summary>
        private bool CanExecutePermissionControl()
        {
            return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.Control);
        }

        private void ExecuteSave()
        {
            SaveCurrentPermissions();
            
            try
            {
                // 直接更新SecurityManager中所有用户的角色权限
                var allUsers = _securityManager.GetAllUsers();
                
                // 禁用详细日志，只显示最终结果
                foreach (var user in allUsers)
                {
                    var updatedRoles = new List<Role>();
                    
                    // 为该用户重新构建角色列表
                    foreach (var userRole in user.Roles)
                    {
                        // 从配置中找到对应的角色配置
                        var roleConfig = _configuration.RolePermissions.FirstOrDefault(r => r.RoleName == userRole.Name);
                        if (roleConfig != null)
                        {
                            var role = new Role(roleConfig.RoleName, roleConfig.RoleDisplayName, "");
                            
                            // 添加角色的权限
                            foreach (var permId in roleConfig.PermissionIds)
                            {
                                var parts = permId.Split('.');
                                if (parts.Length == 2)
                                {
                                    var resource = parts[0];
                                    var action = parts[1];
                                    
                                    // 转换资源名称为正确的大小写
                                    resource = ConvertToProperCase(resource);
                                    action = ConvertToProperCase(action);
                                    
                                    role.Permissions.Add(new Permission(resource, action, ""));
                                }
                            }
                            
                            updatedRoles.Add(role);
                        }
                    }
                    
                    // 静默更新用户角色（不显示详细日志）
                    _securityManager.UpdateUserRoles(user.Username, updatedRoles);
                }
                
                // 也保存到配置文件作为备份
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Permissions.json");
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                var json = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
                File.WriteAllText(configPath, json);
                
                DialogResult = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.PermissionsSaved);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Ewan.Resources.PermissionConfigStrings.SaveFailed, ex.Message), 
                    Ewan.Resources.PermissionConfigStrings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                _uiLogger.Error(() => Ewan.Resources.LogMessages.PermissionsSaveError, ex.Message);
            }
        }

        private string ConvertToProperCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
            
            // 将资源名称转换为正确的大小写格式
            switch (input.ToLower())
            {
                case "camera": return "Camera";
                case "ups": return "UPS";
                case "log": return "Log";
                case "settings": return "Settings";
                case "usermanagement": return "UserManagement";
                case "system": return "System";
                case "permissionconfig": return "PermissionConfig";
                
                // 动作转换
                case "view": return "View";
                case "control": return "Control";
                case "configure": return "Configure";
                case "export": return "Export";
                case "update": return "Update";
                case "create": return "Create";
                case "delete": return "Delete";
                
                default: return input;
            }
        }

        private void ExecuteCancel()
        {
            DialogResult = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SaveCurrentPermissions()
        {
            // 保存当前编辑的权限
            if (SelectedRole != null)
            {
                SelectedRole.PermissionIds.Clear();
                foreach (var category in PermissionCategories)
                {
                    foreach (var permission in category.Permissions)
                    {
                        if (permission.IsGranted)
                        {
                            SelectedRole.PermissionIds.Add(permission.PermissionId);
                        }
                    }
                }
            }
            
            _configuration.LastModified = DateTime.Now;
        }

        private string GetLocalizedCategoryName(string categoryKey)
        {
            // 根据资源键获取本地化的分类名称
            var property = typeof(Ewan.Resources.PermissionConfigStrings).GetProperty(categoryKey);
            if (property != null)
            {
                return property.GetValue(null)?.ToString() ?? categoryKey;
            }
            return categoryKey;
        }

        private string GetLocalizedPermissionName(string nameKey)
        {
            // 根据资源键获取本地化的权限名称
            var property = typeof(Ewan.Resources.PermissionConfigStrings).GetProperty(nameKey);
            if (property != null)
            {
                return property.GetValue(null)?.ToString() ?? nameKey;
            }
            return nameKey;
        }

        private string GetLocalizedPermissionDescription(string descKey)
        {
            // 根据资源键获取本地化的权限描述
            var property = typeof(Ewan.Resources.PermissionConfigStrings).GetProperty(descKey);
            if (property != null)
            {
                return property.GetValue(null)?.ToString() ?? descKey;
            }
            return descKey;
        }

        private string GetLocalizedRoleName(string roleName)
        {
            // 根据角色名称获取本地化的名称
            switch (roleName)
            {
                case "Administrator":
                    return Ewan.Resources.PermissionConfigStrings.AdminRole;
                case "Engineer":
                    return Ewan.Resources.PermissionConfigStrings.EngineerRole;
                case "Operator":
                    return Ewan.Resources.PermissionConfigStrings.OperatorRole;
                default:
                    return roleName;
            }
        }

        #endregion
    }
}