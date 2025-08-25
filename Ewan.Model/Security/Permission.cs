namespace Ewan.Model.Security
{
    /// <summary>
    /// 权限实体类
    /// </summary>
    public class Permission
    {
        /// <summary>
        /// 资源名称 (例如: Camera, UPS, Log, Settings)
        /// </summary>
        public string Resource { get; set; }

        /// <summary>
        /// 操作类型 (例如: View, Control, Configure, Delete)
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// 权限描述
        /// </summary>
        public string Description { get; set; }

        public Permission()
        {
        }

        public Permission(string resource, string action, string description = null)
        {
            Resource = resource;
            Action = action;
            Description = description;
        }

        /// <summary>
        /// 获取权限键值
        /// </summary>
        public string Key => $"{Resource}.{Action}";

        public override string ToString()
        {
            return Key;
        }

        public override bool Equals(object obj)
        {
            if (obj is Permission other)
            {
                return Resource == other.Resource && Action == other.Action;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}