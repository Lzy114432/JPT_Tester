namespace Ewan.Model.Security
{
    /// <summary>
    /// 权限资源常量
    /// </summary>
    public static class PermissionResources
    {
        public const string Camera = "Camera";
        public const string UPS = "UPS";
        public const string Log = "Log";
        public const string Settings = "Settings";
        public const string UserManagement = "UserManagement";
        public const string System = "System";
    }

    /// <summary>
    /// 权限操作常量
    /// </summary>
    public static class PermissionActions
    {
        public const string View = "View";
        public const string Control = "Control";
        public const string Configure = "Configure";
        public const string Delete = "Delete";
        public const string Export = "Export";
        public const string Create = "Create";
        public const string Update = "Update";
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