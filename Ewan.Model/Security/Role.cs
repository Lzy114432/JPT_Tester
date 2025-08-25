using System.Collections.Generic;

namespace Ewan.Model.Security
{
    /// <summary>
    /// 角色实体类
    /// </summary>
    public class Role
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 角色显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 角色描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 角色权限列表
        /// </summary>
        public List<Permission> Permissions { get; set; }

        public Role()
        {
            Permissions = new List<Permission>();
        }

        public Role(string name, string displayName, string description = null) : this()
        {
            Name = name;
            DisplayName = displayName;
            Description = description;
        }
    }
}