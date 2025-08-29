using IOLibrary.Core.Models;
using PlcCommunication.Interfaces;

namespace IOLibrary.Core.Factory
{
    /// <summary>
    /// 硬件IO配置类
    /// </summary>
    public class HardwareIOConfig
    {
        /// <summary>
        /// 硬件类型
        /// </summary>
        public HardwareType Type { get; set; }
        
        /// <summary>
        /// 连接字符串
        /// </summary>
        public string ConnectionString { get; set; }
        
        /// <summary>
        /// IO点数（用于PLC和模拟器）
        /// </summary>
        public int IOCount { get; set; } = 64;
        
        /// <summary>
        /// PLC实例（仅用于MitsubishiPLC类型）
        /// </summary>
        public IPlcBase PlcInstance { get; set; }
        
        /// <summary>
        /// 自定义参数
        /// </summary>
        public object CustomParameter { get; set; }
    }
}