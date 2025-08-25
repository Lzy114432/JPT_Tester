using Ewan.Core.Logger;
using Ewan.Core.Culture;
using Ewan.Model.Permission;
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
        private PermissionConfiguration _configuration;
        
        private ObservableCollection<RolePermissionConfig> _roles;
        private ObservableCollection<UserPermissionConfig> _users;
        private ObservableCollection<PermissionCategoryViewModel> _permissionCategories;
        
        private RolePermissionConfig _selectedRole;
        private UserPermissionConfig _selectedUser;
        private string _currentConfigName;
        private string _currentConfigDescription;
        
        // UI Text Properties
        private string _windowTitle;
        private string _windowDescription;
        private string _roleListTitle;
        private string _userSpecialPermissionsTitle;
        private string _addRoleButtonText;
        private string _deleteRoleButtonText;
        private string _importConfigButtonText;
        private string _exportConfigButtonText;
        private string _applyButtonText;
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

        public string UserSpecialPermissionsTitle
        {
            get => _userSpecialPermissionsTitle;
            set => SetProperty(ref _userSpecialPermissionsTitle, value);
        }

        public string AddRoleButtonText
        {
            get => _addRoleButtonText;
            set => SetProperty(ref _addRoleButtonText, value);
        }

        public string DeleteRoleButtonText
        {
            get => _deleteRoleButtonText;
            set => SetProperty(ref _deleteRoleButtonText, value);
        }

        public string ImportConfigButtonText
        {
            get => _importConfigButtonText;
            set => SetProperty(ref _importConfigButtonText, value);
        }

        public string ExportConfigButtonText
        {
            get => _exportConfigButtonText;
            set => SetProperty(ref _exportConfigButtonText, value);
        }

        public string ApplyButtonText
        {
            get => _applyButtonText;
            set => SetProperty(ref _applyButtonText, value);
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

        public ObservableCollection<UserPermissionConfig> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
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
                    SelectedUser = null;
                    LoadRolePermissions(value);
                }
            }
        }

        public UserPermissionConfig SelectedUser
        {
            get => _selectedUser;
            set
            {
                SetProperty(ref _selectedUser, value);
                if (value != null)
                {
                    SelectedRole = null;
                    LoadUserPermissions(value);
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

        public DelegateCommand AddRoleCommand { get; }
        public DelegateCommand DeleteRoleCommand { get; }
        public DelegateCommand ImportConfigCommand { get; }
        public DelegateCommand ExportConfigCommand { get; }
        public DelegateCommand ApplyCommand { get; }
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        #endregion

        public PermissionConfigViewModel()
        {
            // 初始化CultureManager
            _cultureManager = CultureManager.Instance();
            _cultureManager.CultureChanged += OnCultureChanged;
            
            // 确保UIStrings的Culture同步
            Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
            
            // 初始化命令
            AddRoleCommand = new DelegateCommand(ExecuteAddRole);
            DeleteRoleCommand = new DelegateCommand(ExecuteDeleteRole, CanDeleteRole);
            ImportConfigCommand = new DelegateCommand(ExecuteImportConfig);
            ExportConfigCommand = new DelegateCommand(ExecuteExportConfig);
            ApplyCommand = new DelegateCommand(ExecuteApply);
            SaveCommand = new DelegateCommand(ExecuteSave);
            CancelCommand = new DelegateCommand(ExecuteCancel);

            // 初始化数据
            InitializeData();
            
            // 初始化UI文本
            UpdateUITexts();
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            // 同步UIStrings和PermissionConfigStrings的Culture设置
            Ewan.Resources.UIStrings.Culture = e.NewCulture;
            Ewan.Resources.PermissionConfigStrings.Culture = e.NewCulture;
            UpdateUITexts();
            
            // 刷新权限分类显示
            if (PermissionCategories != null)
            {
                LoadPermissionCategories();
                
                // 重新加载当前选中项的权限
                if (SelectedRole != null)
                {
                    LoadRolePermissions(SelectedRole);
                }
                else if (SelectedUser != null)
                {
                    LoadUserPermissions(SelectedUser);
                }
            }
        }

        private void UpdateUITexts()
        {
            // 确保资源文件使用正确的语言
            Ewan.Resources.PermissionConfigStrings.Culture = _cultureManager.CurrentCulture;
            
            WindowTitle = Ewan.Resources.PermissionConfigStrings.WindowTitle;
            WindowDescription = Ewan.Resources.PermissionConfigStrings.WindowDescription;
            RoleListTitle = Ewan.Resources.PermissionConfigStrings.RoleListTitle;
            UserSpecialPermissionsTitle = Ewan.Resources.PermissionConfigStrings.UserSpecialPermissions;
            AddRoleButtonText = Ewan.Resources.PermissionConfigStrings.AddRoleButton;
            DeleteRoleButtonText = Ewan.Resources.PermissionConfigStrings.DeleteRoleButton;
            ImportConfigButtonText = Ewan.Resources.PermissionConfigStrings.ImportConfigButton;
            ExportConfigButtonText = Ewan.Resources.PermissionConfigStrings.ExportConfigButton;
            ApplyButtonText = Ewan.Resources.PermissionConfigStrings.ApplyButton;
            SaveButtonText = Ewan.Resources.PermissionConfigStrings.SaveButton;
            CancelButtonText = Ewan.Resources.PermissionConfigStrings.CancelButton;
            
            // Update CurrentConfigDisplayText if it's set
            if (!string.IsNullOrEmpty(CurrentConfigName))
            {
                CurrentConfigDisplayText = string.Format(Ewan.Resources.PermissionConfigStrings.CurrentConfig, CurrentConfigName);
            }
        }

        private void InitializeData()
        {
            // 创建默认配置
            _configuration = CreateDefaultConfiguration();
            
            // 初始化集合
            Roles = new ObservableCollection<RolePermissionConfig>(_configuration.RolePermissions);
            Users = new ObservableCollection<UserPermissionConfig>(_configuration.UserPermissions);
            PermissionCategories = new ObservableCollection<PermissionCategoryViewModel>();
            
            // 加载权限分类
            LoadPermissionCategories();
            
            // 默认选中第一个角色
            if (Roles.Count > 0)
            {
                SelectedRole = Roles[0];
            }
        }

        private PermissionConfiguration CreateDefaultConfiguration()
        {
            var config = new PermissionConfiguration();
            
            // 定义系统权限
            config.Permissions = new List<PermissionConfig>
            {
                // 系统管理
                new PermissionConfig { PermissionId = "system.settings", DisplayName = "SystemSettings", Description = "SystemSettingsDesc", Category = "SystemManagement" },
                new PermissionConfig { PermissionId = "system.language", DisplayName = "LanguageSwitch", Description = "LanguageSwitchDesc", Category = "SystemManagement" },
                new PermissionConfig { PermissionId = "system.logs", DisplayName = "ViewLogs", Description = "ViewLogsDesc", Category = "SystemManagement" },
                new PermissionConfig { PermissionId = "system.backup", DisplayName = "BackupRestore", Description = "BackupRestoreDesc", Category = "SystemManagement" },
                
                // 用户管理
                new PermissionConfig { PermissionId = "user.manage", DisplayName = "UserManagePerm", Description = "UserManagePermDesc", Category = "UserManagement" },
                new PermissionConfig { PermissionId = "user.permissions", DisplayName = "PermissionConfig", Description = "PermissionConfigDesc", Category = "UserManagement" },
                new PermissionConfig { PermissionId = "user.switchUser", DisplayName = "SwitchUser", Description = "SwitchUserDesc", Category = "UserManagement" },
                
                // 设备控制
                new PermissionConfig { PermissionId = "device.camera", DisplayName = "CameraControl", Description = "CameraControlDesc", Category = "DeviceControl" },
                new PermissionConfig { PermissionId = "device.ups", DisplayName = "UPSControl", Description = "UPSControlDesc", Category = "DeviceControl" },
                new PermissionConfig { PermissionId = "device.marking", DisplayName = "MarkingMachineControl", Description = "MarkingMachineControlDesc", Category = "DeviceControl" },
                
                // 数据管理
                new PermissionConfig { PermissionId = "data.export", DisplayName = "DataExport", Description = "DataExportDesc", Category = "DataManagement" },
                new PermissionConfig { PermissionId = "data.import", DisplayName = "DataImport", Description = "DataImportDesc", Category = "DataManagement" },
                new PermissionConfig { PermissionId = "data.delete", DisplayName = "DataDelete", Description = "DataDeleteDesc", Category = "DataManagement" },
                
                // 生产操作
                new PermissionConfig { PermissionId = "production.start", DisplayName = "StartProduction", Description = "StartProductionDesc", Category = "ProductionOperation" },
                new PermissionConfig { PermissionId = "production.stop", DisplayName = "StopProduction", Description = "StopProductionDesc", Category = "ProductionOperation" },
                new PermissionConfig { PermissionId = "production.configure", DisplayName = "ConfigureProduction", Description = "ConfigureProductionDesc", Category = "ProductionOperation" },
            };
            
            // 定义角色权限
            config.RolePermissions = new List<RolePermissionConfig>
            {
                new RolePermissionConfig
                {
                    RoleName = "Administrator",
                    RoleDisplayName = "管理员",
                    IsSystemRole = true,
                    PermissionIds = config.Permissions.Select(p => p.PermissionId).ToList() // 管理员拥有所有权限
                },
                new RolePermissionConfig
                {
                    RoleName = "Engineer",
                    RoleDisplayName = "工程师",
                    IsSystemRole = true,
                    PermissionIds = new List<string>
                    {
                        "system.language", "system.logs",
                        "user.switchUser",
                        "device.camera", "device.ups", "device.marking",
                        "data.export", "data.import",
                        "production.start", "production.stop", "production.configure"
                    }
                },
                new RolePermissionConfig
                {
                    RoleName = "Operator",
                    RoleDisplayName = "操作员",
                    IsSystemRole = true,
                    PermissionIds = new List<string>
                    {
                        "device.camera", "device.ups",
                        "production.start", "production.stop"
                    }
                }
            };
            
            // 定义用户特殊权限（示例）
            config.UserPermissions = new List<UserPermissionConfig>
            {
                new UserPermissionConfig
                {
                    Username = "admin",
                    GrantedPermissions = new List<string>(), // 使用角色默认权限
                    DeniedPermissions = new List<string>()
                },
                new UserPermissionConfig
                {
                    Username = "engineer",
                    GrantedPermissions = new List<string>(), 
                    DeniedPermissions = new List<string>()
                },
                new UserPermissionConfig
                {
                    Username = "operator",
                    GrantedPermissions = new List<string>(),
                    DeniedPermissions = new List<string>()
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
            CurrentConfigName = GetLocalizedRoleName(role.RoleDisplayName);
            
            CurrentConfigDescription = string.Format(Ewan.Resources.PermissionConfigStrings.RolePrefix, role.RoleName);
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

        private void LoadUserPermissions(UserPermissionConfig user)
        {
            CurrentConfigName = user.Username;
            
            CurrentConfigDescription = Ewan.Resources.PermissionConfigStrings.UserSpecialPermissionsDesc;
            CurrentConfigDisplayText = string.Format(Ewan.Resources.PermissionConfigStrings.CurrentConfig, CurrentConfigName);
            
            // 首先获取用户的角色权限
            // 这里需要从SecurityManager获取用户角色，暂时简化处理
            var userRole = Roles.FirstOrDefault(r => r.RoleName == "Operator"); // 示例
            
            foreach (var category in PermissionCategories)
            {
                foreach (var permission in category.Permissions)
                {
                    // 基础权限来自角色
                    bool hasPermission = userRole?.PermissionIds.Contains(permission.PermissionId) ?? false;
                    
                    // 应用用户特殊权限
                    if (user.GrantedPermissions.Contains(permission.PermissionId))
                        hasPermission = true;
                    if (user.DeniedPermissions.Contains(permission.PermissionId))
                        hasPermission = false;
                    
                    permission.IsGranted = hasPermission;
                    permission.IsEditable = true;
                }
            }
        }

        #region Command Implementations

        private void ExecuteAddRole()
        {
            MessageBox.Show(Ewan.Resources.PermissionConfigStrings.AddRoleFeatureDevelopment, 
                Ewan.Resources.PermissionConfigStrings.Info, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteDeleteRole()
        {
            if (SelectedRole != null && !SelectedRole.IsSystemRole)
            {
                string confirmMessage = string.Format(
                    Ewan.Resources.PermissionConfigStrings.ConfirmDeleteRole, 
                    GetLocalizedRoleName(SelectedRole.RoleDisplayName));
                    
                string confirmTitle = Ewan.Resources.PermissionConfigStrings.ConfirmDeleteTitle;
                    
                var result = MessageBox.Show(
                    confirmMessage,
                    confirmTitle,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    Roles.Remove(SelectedRole);
                    _configuration.RolePermissions.Remove(SelectedRole);
                    SelectedRole = Roles.FirstOrDefault();
                }
            }
        }

        private bool CanDeleteRole()
        {
            return SelectedRole != null && !SelectedRole.IsSystemRole;
        }

        private void ExecuteImportConfig()
        {
            string filter = Ewan.Resources.PermissionConfigStrings.JsonFiles;
            string title = Ewan.Resources.PermissionConfigStrings.ImportTitle;
                
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(dialog.FileName);
                    _configuration = JsonConvert.DeserializeObject<PermissionConfiguration>(json);
                    InitializeData();
                    MessageBox.Show(Ewan.Resources.PermissionConfigStrings.ImportSuccess, 
                        Ewan.Resources.PermissionConfigStrings.Success, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Ewan.Resources.PermissionConfigStrings.ImportFailed, ex.Message), 
                        Ewan.Resources.PermissionConfigStrings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteExportConfig()
        {
            string filter = Ewan.Resources.PermissionConfigStrings.JsonFiles;
            string title = Ewan.Resources.PermissionConfigStrings.ExportTitle;
                
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = $"PermissionConfig_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };
            
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    SaveCurrentPermissions();
                    var json = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
                    File.WriteAllText(dialog.FileName, json);
                    MessageBox.Show(Ewan.Resources.PermissionConfigStrings.ExportSuccess, 
                        Ewan.Resources.PermissionConfigStrings.Success, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(Ewan.Resources.PermissionConfigStrings.ExportFailed, ex.Message), 
                        Ewan.Resources.PermissionConfigStrings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteApply()
        {
            SaveCurrentPermissions();
            // TODO: 应用权限到系统
            
            MessageBox.Show(Ewan.Resources.PermissionConfigStrings.PermissionsApplied, 
                Ewan.Resources.PermissionConfigStrings.Success, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExecuteSave()
        {
            SaveCurrentPermissions();
            
            // TODO: 保存到配置文件
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Permissions.json");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            
            try
            {
                var json = JsonConvert.SerializeObject(_configuration, Formatting.Indented);
                File.WriteAllText(configPath, json);
                
                DialogResult = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Ewan.Resources.PermissionConfigStrings.SaveFailed, ex.Message), 
                    Ewan.Resources.PermissionConfigStrings.Error, MessageBoxButton.OK, MessageBoxImage.Error);
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
            else if (SelectedUser != null)
            {
                // 保存用户特殊权限
                // TODO: 实现用户权限保存逻辑
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

        private string GetLocalizedRoleName(string roleDisplayName)
        {
            // 根据角色显示名称获取本地化的名称
            switch (roleDisplayName)
            {
                case "管理员":
                case "Administrator":
                    return Ewan.Resources.PermissionConfigStrings.AdminRole;
                case "工程师":
                case "Engineer":
                    return Ewan.Resources.PermissionConfigStrings.EngineerRole;
                case "操作员":
                case "Operator":
                    return Ewan.Resources.PermissionConfigStrings.OperatorRole;
                default:
                    return roleDisplayName;
            }
        }

        #endregion
    }
}