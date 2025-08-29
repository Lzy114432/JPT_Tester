namespace IOLibrary.Core.Models
{
    /// <summary>
    /// IO映射定义
    /// </summary>
    public class IOMapping
    {
        public int LogicalIndex { get; set; }      // 逻辑位号（程序使用）
        public int PhysicalIndex { get; set; }     // 物理位号（硬件实际）
        public string? Name { get; set; }           // 名称描述
        public bool IsNormallyOpen { get; set; }    // true=常开，false=常闭

        public override string ToString()
        {
            return $"{Name}: L{LogicalIndex}->P{PhysicalIndex} ({(IsNormallyOpen ? "NO" : "NC")})";
        }
    }
}