using System;

namespace EwanCore.Attribute
{
    /// <summary>
    /// Manager 标记：用于被 <see cref="EwanCore.Bootstrap.ManagerTypeScanner"/> / <see cref="EwanCore.Bootstrap.ManagerLifetimeHost"/> 扫描并统一管理生命周期。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ManagerAttribute : System.Attribute
    {
        /// <summary>
        /// 是否启用：为 false 时扫描器会忽略该类型。
        /// </summary>
        public bool IsEnable { get; set; } = true;

        /// <summary>
        /// 优先级：数值越小越先初始化（释放时反序）。
        /// 约定建议：
        /// - 0：配置/基础设施
        /// - 1：外部连接/通讯（PLC/MES/IO）
        /// - 2：业务模块启动
        /// - 99：其它模块默认
        /// </summary>
        public int Priority { get; set; } = 99;
    }
}
