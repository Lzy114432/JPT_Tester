using Ewan.Core.Attribute;
using Ewan.Model.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Ewan.Core.Security
{
    /// <summary>
    /// 安全管理器 - 处理用户认证和权限控制
    /// </summary>
    [Manager(Priority = 0)]
    public class SecurityManager : BaseManager<SecurityManager>
    {
        private User _currentUser;
        private List<User> _users;
        private readonly string _usersFilePath = "users.json";

        /// <summary>
        /// 当前登录用户
        /// </summary>
        public User CurrentUser => _currentUser;

        /// <summary>
        /// 是否已登录
        /// </summary>
        public bool IsAuthenticated => _currentUser != null;

        /// <summary>
        /// 用户认证成功事件
        /// </summary>
        public event EventHandler<User> UserAuthenticated;

        /// <summary>
        /// 用户注销事件
        /// </summary>
        public event EventHandler UserLoggedOut;

        public override bool Init()
        {
            LoadUsers();
            InitializeDefaultUsers();
            _uiLogger.Info(() => Ewan.Resources.LogMessages.SecurityManagerInitialized);
            return base.Init();
        }

        /// <summary>
        /// 用户登录认证
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>认证是否成功</returns>
        public bool Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.LoginInvalidInput);
                return false;
            }

            var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (user == null || !user.IsActive)
            {
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.LoginUserNotFound, username);
                return false;
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.LoginPasswordIncorrect, username);
                return false;
            }

            _currentUser = user;
            _uiLogger.Info(() => Ewan.Resources.LogMessages.LoginSuccessful, username);
            UserAuthenticated?.Invoke(this, user);
            return true;
        }

        /// <summary>
        /// 用户注销
        /// </summary>
        public void Logout()
        {
            if (_currentUser != null)
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.UserLoggedOut, _currentUser.Username);
                _currentUser = null;
                UserLoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 检查用户是否具有指定权限
        /// </summary>
        /// <param name="resource">资源</param>
        /// <param name="action">操作</param>
        /// <returns>是否有权限</returns>
        public bool HasPermission(string resource, string action)
        {
            if (!IsAuthenticated)
                return false;

            return _currentUser.Roles
                .SelectMany(r => r.Permissions)
                .Any(p => p.Resource.Equals(resource, StringComparison.OrdinalIgnoreCase) &&
                         p.Action.Equals(action, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 检查用户是否具有指定角色
        /// </summary>
        /// <param name="roleName">角色名</param>
        /// <returns>是否具有角色</returns>
        public bool HasRole(string roleName)
        {
            if (!IsAuthenticated)
                return false;

            return _currentUser.Roles.Any(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取用户的所有权限
        /// </summary>
        /// <returns>权限列表</returns>
        public List<Permission> GetUserPermissions()
        {
            if (!IsAuthenticated)
                return new List<Permission>();

            return _currentUser.Roles
                .SelectMany(r => r.Permissions)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 创建新用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="displayName">显示名称</param>
        /// <param name="roles">角色列表</param>
        /// <returns>是否创建成功</returns>
        public bool CreateUser(string username, string password, string displayName, List<Role> roles)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return false;

            if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.UserAlreadyExists, username);
                return false;
            }

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                DisplayName = displayName ?? username,
                Roles = roles ?? new List<Role>(),
                IsActive = true
            };

            _users.Add(user);
            SaveUsers();
            _uiLogger.Info(() => Ewan.Resources.LogMessages.UserCreated, username);
            return true;
        }

        /// <summary>
        /// 初始化默认用户
        /// </summary>
        private void InitializeDefaultUsers()
        {
            if (_users.Count == 0)
            {
                // 创建默认管理员用户
                var adminRole = CreateAdministratorRole();
                var engineerRole = CreateEngineerRole();
                var operatorRole = CreateOperatorRole();

                CreateUser("admin", "123456", "系统管理员", new List<Role> { adminRole });
                CreateUser("engineer", "123456", "工程师", new List<Role> { engineerRole });
                CreateUser("operator", "123456", "操作员", new List<Role> { operatorRole });

                _uiLogger.Info(() => Ewan.Resources.LogMessages.DefaultUsersCreated);
            }
        }

        /// <summary>
        /// 创建管理员角色
        /// </summary>
        private Role CreateAdministratorRole()
        {
            var role = new Role(RoleNames.Administrator, "系统管理员", "拥有所有权限");
            role.Permissions.AddRange(new[]
            {
                new Permission(PermissionResources.Camera, PermissionActions.View, "查看相机"),
                new Permission(PermissionResources.Camera, PermissionActions.Control, "控制相机"),
                new Permission(PermissionResources.Camera, PermissionActions.Configure, "配置相机"),
                new Permission(PermissionResources.UPS, PermissionActions.View, "查看UPS"),
                new Permission(PermissionResources.UPS, PermissionActions.Control, "控制UPS"),
                new Permission(PermissionResources.UPS, PermissionActions.Configure, "配置UPS"),
                new Permission(PermissionResources.Log, PermissionActions.View, "查看日志"),
                new Permission(PermissionResources.Log, PermissionActions.Export, "导出日志"),
                new Permission(PermissionResources.Settings, PermissionActions.View, "查看设置"),
                new Permission(PermissionResources.Settings, PermissionActions.Update, "修改设置"),
                new Permission(PermissionResources.UserManagement, PermissionActions.View, "查看用户"),
                new Permission(PermissionResources.UserManagement, PermissionActions.Create, "创建用户"),
                new Permission(PermissionResources.UserManagement, PermissionActions.Update, "修改用户"),
                new Permission(PermissionResources.UserManagement, PermissionActions.Delete, "删除用户"),
                new Permission(PermissionResources.System, PermissionActions.Control, "系统控制")
            });
            return role;
        }

        /// <summary>
        /// 创建工程师角色
        /// </summary>
        private Role CreateEngineerRole()
        {
            var role = new Role(RoleNames.Engineer, "工程师", "设备控制和配置权限");
            role.Permissions.AddRange(new[]
            {
                new Permission(PermissionResources.Camera, PermissionActions.View, "查看相机"),
                new Permission(PermissionResources.Camera, PermissionActions.Control, "控制相机"),
                new Permission(PermissionResources.Camera, PermissionActions.Configure, "配置相机"),
                new Permission(PermissionResources.UPS, PermissionActions.View, "查看UPS"),
                new Permission(PermissionResources.UPS, PermissionActions.Control, "控制UPS"),
                new Permission(PermissionResources.Log, PermissionActions.View, "查看日志"),
                new Permission(PermissionResources.Log, PermissionActions.Export, "导出日志"),
                new Permission(PermissionResources.Settings, PermissionActions.View, "查看设置")
            });
            return role;
        }

        /// <summary>
        /// 创建操作员角色
        /// </summary>
        private Role CreateOperatorRole()
        {
            var role = new Role(RoleNames.Operator, "操作员", "基本操作权限");
            role.Permissions.AddRange(new[]
            {
                new Permission(PermissionResources.Camera, PermissionActions.View, "查看相机"),
                new Permission(PermissionResources.UPS, PermissionActions.View, "查看UPS"),
                new Permission(PermissionResources.Log, PermissionActions.View, "查看日志")
            });
            return role;
        }

        /// <summary>
        /// 加载用户数据
        /// </summary>
        private void LoadUsers()
        {
            _users = new List<User>();
            
            try
            {
                if (File.Exists(_usersFilePath))
                {
                    var json = File.ReadAllText(_usersFilePath, Encoding.UTF8);
                    var users = JsonConvert.DeserializeObject<List<User>>(json);
                    if (users != null)
                    {
                        _users = users;
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.UsersLoaded, _users.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.UsersLoadError, ex.Message);
            }
        }

        /// <summary>
        /// 保存用户数据
        /// </summary>
        private void SaveUsers()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_users, Formatting.Indented);
                File.WriteAllText(_usersFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.UsersSaveError, ex.Message);
            }
        }

        /// <summary>
        /// 密码哈希
        /// </summary>
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "MarkingMachine_Salt"));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}