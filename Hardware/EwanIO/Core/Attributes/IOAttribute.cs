using System;

namespace EwanIO.Core.Attributes
{
    /// <summary>
    /// V2 IO 属性标记 - 支持更多配置选项
    /// Direction is inferred from property type (InputSignal/OutputSignal).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IOAttribute : Attribute
    {
        /// <summary>
        /// 逻辑索引（与映射配置对应）
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 确认超时时间（毫秒），用于 Confirm 操作
        /// -1 表示使用全局默认值
        /// </summary>
        public int ConfirmTimeoutMs { get; set; } = -1;

        /// <summary>
        /// 显示名称（可选，用于 UI/日志）
        /// 如果不设置，则使用属性名
        /// </summary>
        public string? DisplayName { get; set; }

        public IOAttribute(int index)
        {
            Index = index;
        }
    }
}
