namespace IOLibrary.Core.Models
{
    /// <summary>
    /// 硬件类型枚举
    /// </summary>
    public enum HardwareType
    {
        /// <summary>
        /// 三菱PLC (MC协议)
        /// </summary>
        MitsubishiPLC,
        
        /// <summary>
        /// IOC0640板卡
        /// </summary>
        IOC0640,
        
        /// <summary>
        /// SMC606IO板卡
        /// </summary>
        SMC606IO,
        
        /// <summary>
        /// 模拟硬件（用于测试）
        /// </summary>
        Simulator,
        
        /// <summary>
        /// 自定义硬件
        /// </summary>
        Custom
    }
}