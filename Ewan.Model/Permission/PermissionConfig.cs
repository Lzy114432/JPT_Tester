using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ewan.Model.Permission
{
    /// <summary>
    /// 权限配置项
    /// </summary>
    public class PermissionConfig
    {
        /// <summary>
        /// 权限ID
        /// </summary>
        public string PermissionId { get; set; }

        /// <summary>
        /// 权限显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 权限描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 权限分类
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// 角色权限配置
    /// </summary>
    public class RolePermissionConfig : INotifyPropertyChanged
    {
        private string _localizedRoleDisplayName;

        /// <summary>
        /// 角色名称
        /// </summary>
        public string RoleName { get; set; }

        /// <summary>
        /// 角色显示名称
        /// </summary>
        public string RoleDisplayName { get; set; }

        /// <summary>
        /// 本地化的角色显示名称（运行时设置）
        /// </summary>
        public string LocalizedRoleDisplayName 
        { 
            get => _localizedRoleDisplayName;
            set
            {
                if (_localizedRoleDisplayName != value)
                {
                    _localizedRoleDisplayName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 角色拥有的权限ID列表
        /// </summary>
        public List<string> PermissionIds { get; set; } = new List<string>();

        /// <summary>
        /// 是否为系统角色（不可删除）
        /// </summary>
        public bool IsSystemRole { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 用户特殊权限配置
    /// </summary>
    public class UserPermissionConfig
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 分配的角色列表
        /// </summary>
        public List<string> AssignedRoles { get; set; } = new List<string>();

        /// <summary>
        /// 额外授予的权限ID列表
        /// </summary>
        public List<string> AdditionalPermissionIds { get; set; } = new List<string>();

        /// <summary>
        /// 明确撤销的权限ID列表（优先级高于角色权限）
        /// </summary>
        public List<string> RevokedPermissionIds { get; set; } = new List<string>();

        /// <summary>
        /// 额外授予的权限（兼容旧版本）
        /// </summary>
        public List<string> GrantedPermissions 
        { 
            get => AdditionalPermissionIds;
            set => AdditionalPermissionIds = value ?? new List<string>();
        }

        /// <summary>
        /// 明确拒绝的权限（兼容旧版本）
        /// </summary>
        public List<string> DeniedPermissions 
        { 
            get => RevokedPermissionIds;
            set => RevokedPermissionIds = value ?? new List<string>();
        }
    }

    /// <summary>
    /// 完整的权限配置
    /// </summary>
    public class PermissionConfiguration
    {
        /// <summary>
        /// 所有权限定义
        /// </summary>
        public List<PermissionConfig> Permissions { get; set; } = new List<PermissionConfig>();

        /// <summary>
        /// 角色权限配置
        /// </summary>
        public List<RolePermissionConfig> RolePermissions { get; set; } = new List<RolePermissionConfig>();

        /// <summary>
        /// 用户特殊权限配置
        /// </summary>
        public List<UserPermissionConfig> UserPermissions { get; set; } = new List<UserPermissionConfig>();

        /// <summary>
        /// 配置版本
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;
    }
}