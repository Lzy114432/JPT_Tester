using System;

namespace Ewan.Model.System
{
    /// <summary>
    /// 系统参数配置模型
    /// </summary>
    [Serializable]
    public class SystemParameters
    {
        /// <summary>
        /// 高速模式启用状态
        /// </summary>
        public bool HighSpeedModeEnabled { get; set; } = false;

        /// <summary>
        /// 系统复位延迟时间（毫秒）
        /// </summary>
        public int ResetDelayMs { get; set; } = 4000;

        /// <summary>
        /// 低速模式设置延迟（毫秒）
        /// </summary>
        public int LowSpeedSetupDelayMs { get; set; } = 500;

        /// <summary>
        /// 环线上料请求超时时间（秒）
        /// </summary>
        public int RingLineTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 创建默认参数配置
        /// </summary>
        public static SystemParameters CreateDefault()
        {
            return new SystemParameters
            {
                HighSpeedModeEnabled = false,
                ResetDelayMs = 4000,
                LowSpeedSetupDelayMs = 500,
                RingLineTimeoutSeconds = 30
            };
        }

        /// <summary>
        /// 验证参数有效性
        /// </summary>
        public bool Validate()
        {
            return ResetDelayMs > 0 && LowSpeedSetupDelayMs > 0 && RingLineTimeoutSeconds > 0;
        }
    }
}
