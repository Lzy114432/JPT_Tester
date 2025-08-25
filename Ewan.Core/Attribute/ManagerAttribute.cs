namespace Ewan.Core.Attribute
{
    /// <summary>
    /// 管理类注解
    /// 加上此标签的才自动初始化
    /// </summary>
    public class ManagerAttribute : System.Attribute
    {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnable = true;
        /// <summary>
        /// 优先级 0代表优先级最高
        /// 配置类的优先级最高 为0
        /// 外部连接类的优先级次之 为1
        /// 模块启动的优先级 为2
        /// ......
        /// 其他模块 默认99
        /// </summary>
        public int Priority { get; set; } = 99;
    }
}
