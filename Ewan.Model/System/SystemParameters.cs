using System;

namespace Ewan.Model.System
{
    /// <summary>
    /// 料仓选择枚举
    /// </summary>
    public enum BinSelection
    {
        /// <summary>
        /// 料仓1
        /// </summary>
        Bin1 = 1,

        /// <summary>
        /// 料仓2
        /// </summary>
        Bin2 = 2,

        /// <summary>
        /// 料仓3
        /// </summary>
        Bin3 = 3
    }

    /// <summary>
    /// 系统参数配置模型
    /// </summary>
    [Serializable]
    public class SystemParameters
    {
        /// <summary>
        /// 是否启用装料模块
        /// </summary>
        public bool EnableLoadingModule { get; set; } = true;

        /// <summary>
        /// 是否启用下料模块
        /// </summary>
        public bool EnableUnloadingModule { get; set; } = true;

        /// <summary>
        /// 当前选择的料仓
        /// </summary>
        public BinSelection SelectedBin { get; set; } = BinSelection.Bin1;

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
        /// 是否屏蔽安全门报警
        /// </summary>
        public bool SafetyDoorAlarmBypass { get; set; } = false;

        /// <summary>
        /// 环线下料是否启用放空车
        /// </summary>
        public bool EnableEmptyCartRelease { get; set; } = false;

        /// <summary>
        /// 环线下料间隔多少辆放空车
        /// </summary>
        public int EmptyCartReleaseInterval { get; set; } = 5;

        /// <summary>
        /// 创建默认参数配置
        /// </summary>
        public static SystemParameters CreateDefault()
        {
            return new SystemParameters
            {
                EnableLoadingModule = true,
                EnableUnloadingModule = true,
                SelectedBin = BinSelection.Bin1,
                HighSpeedModeEnabled = false,
                ResetDelayMs = 4000,
                LowSpeedSetupDelayMs = 500,
                RingLineTimeoutSeconds = 30,
                SafetyDoorAlarmBypass = false,
                EnableEmptyCartRelease = false,
                EmptyCartReleaseInterval = 5
            };
        }

        /// <summary>
        /// 验证参数有效性
        /// </summary>
        public bool Validate()
        {
            return ResetDelayMs > 0
                   && LowSpeedSetupDelayMs > 0
                   && RingLineTimeoutSeconds > 0
                   && EmptyCartReleaseInterval > 0
                   && Enum.IsDefined(typeof(BinSelection), SelectedBin);
        }
    }
}
