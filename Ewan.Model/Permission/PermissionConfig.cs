using System;
using System.Collections.Generic;

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
    public class RolePermissionConfig
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string RoleName { get; set; }

        /// <summary>
        /// 角色显示名称
        /// </summary>
        public string RoleDisplayName { get; set; }

        /// <summary>
        /// 角色拥有的权限ID列表
        /// </summary>
        public List<string> PermissionIds { get; set; } = new List<string>();

        /// <summary>
        /// 是否为系统角色（不可删除）
        /// </summary>
        public bool IsSystemRole { get; set; }
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
        /// 额外授予的权限
        /// </summary>
        public List<string> GrantedPermissions { get; set; } = new List<string>();

        /// <summary>
        /// 明确拒绝的权限（优先级高于角色权限）
        /// </summary>
        public List<string> DeniedPermissions { get; set; } = new List<string>();
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