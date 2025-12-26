namespace EwanCore.AlarmSystem
{
    /// <summary>
    /// 报警集合变化类型。
    /// </summary>
    public enum AlarmChangeKind
    {
        /// <summary>
        /// 新增。
        /// </summary>
        Added = 0,

        /// <summary>
        /// 更新（同 Key 重复触发，Occurrence/AlarmTime 刷新）。
        /// </summary>
        Updated = 1,

        /// <summary>
        /// 移除单条。
        /// </summary>
        Removed = 2,

        /// <summary>
        /// 清空。
        /// </summary>
        Cleared = 3
    }
}

