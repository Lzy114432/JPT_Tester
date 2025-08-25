using System.Collections.Generic;

namespace Ewan.Model.Security
{
    /// <summary>
    /// 用户实体类
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码哈希值
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// 用户真实姓名
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 用户角色列表
        /// </summary>
        public List<Role> Roles { get; set; }

        /// <summary>
        /// 用户是否激活
        /// </summary>
        public bool IsActive { get; set; }

        public User()
        {
            Roles = new List<Role>();
            IsActive = true;
        }
    }
}