namespace Ewan.Model.Security
{
    /// <summary>
    /// 权限资源常量
    /// </summary>
    public static class PermissionResources
    {
        public const string Language = "Language";                   // 语言切换权限
        public const string PermissionConfig = "PermissionConfig";  // 权限配置界面权限
    }

    /// <summary>
    /// 权限操作常量
    /// </summary>
    public static class PermissionActions
    {
        public const string View = "View";        // 查看权限
        public const string Control = "Control";  // 控制权限
    }

    /// <summary>
    /// 预定义角色常量
    /// </summary>
    public static class RoleNames
    {
        public const string Administrator = "Administrator";
        public const string Engineer = "Engineer";
        public const string Operator = "Operator";
        public const string Guest = "Guest";
    }
}