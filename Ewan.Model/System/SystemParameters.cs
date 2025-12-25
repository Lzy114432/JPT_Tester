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
    /// 小车检测模式枚举
    /// </summary>
    public enum CartCheckMode
    {
        /// <summary>
        /// 按空车数量检测
        /// </summary>
        EmptyCart = 0,

        /// <summary>
        /// 按切栈桥车数量检测
        /// </summary>
        CuttingBridgeCar = 1
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
        /// 当前选择的料仓（已弃用，保留用于兼容旧配置）
        /// </summary>
        [Obsolete("请使用 LoadingBinSelection 和 UnloadingBinSelection")]
        public BinSelection SelectedBin { get; set; } = BinSelection.Bin1;

        /// <summary>
        /// 装料模块选择的料仓
        /// </summary>
        public BinSelection LoadingBinSelection { get; set; } = BinSelection.Bin1;

        /// <summary>
        /// 下料模块选择的料仓
        /// </summary>
        public BinSelection UnloadingBinSelection { get; set; } = BinSelection.Bin1;

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
        /// 需要保持的空车数量
        /// </summary>
        public int EmptyCartReserveCount { get; set; } = 0;

        /// <summary>
        /// 小车检测模式
        /// </summary>
        public CartCheckMode CartCheckMode { get; set; } = CartCheckMode.EmptyCart;

        /// <summary>
        /// 需要保持的切栈桥车数量
        /// </summary>
        public int CuttingBridgeCarReserveCount { get; set; } = 0;

        #region CodeReader

        /// <summary>
        /// 扫码器类型（Datalogic/Hikvision）
        /// </summary>
        public string CodeReaderType { get; set; } = "Datalogic";

        /// <summary>
        /// 扫码器IP
        /// </summary>
        public string CodeReaderIp { get; set; } = "192.168.3.100";

        /// <summary>
        /// 扫码器端口（TCP类型扫码器）
        /// </summary>
        public int CodeReaderPort { get; set; } = 51236;

        /// <summary>
        /// 触发命令（TCP类型扫码器）
        /// </summary>
        public string CodeReaderTriggerCommand { get; set; } = "T";

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        public int CodeReaderConnectionTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// 接收超时（毫秒）
        /// </summary>
        public int CodeReaderReceiveTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// æ‰«ç é‡è¯•æ¬¡æ•°
        /// </summary>
        public int CodeReaderScanRetryCount { get; set; } = 3;

        #endregion

        #region MES

        /// <summary>
        /// 是否启用 MES
        /// </summary>
        public bool MesEnabled { get; set; } = false;

        /// <summary>
        /// MQTT Broker 地址
        /// </summary>
        public string MesBrokerHost { get; set; } = "localhost";

        /// <summary>
        /// MQTT Broker 端口
        /// </summary>
        public int MesBrokerPort { get; set; } = 1883;

        /// <summary>
        /// MQTT 用户名
        /// </summary>
        public string MesUserName { get; set; } = string.Empty;

        /// <summary>
        /// MQTT 密码
        /// </summary>
        public string MesPassword { get; set; } = string.Empty;

        /// <summary>
        /// MQTT ClientId（为空则自动生成）
        /// </summary>
        public string MesClientId { get; set; } = string.Empty;

        /// <summary>
        /// 是否 CleanSession
        /// </summary>
        public bool MesCleanSession { get; set; } = true;

        /// <summary>
        /// KeepAlive（秒）
        /// </summary>
        public int MesKeepAliveSeconds { get; set; } = 30;

        /// <summary>
        /// 环线设备ID
        /// </summary>
        public string MesRingLineDeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 环线设备编码
        /// </summary>
        public string MesRingLineDeviceCode { get; set; } = string.Empty;

        #endregion

        /// <summary>
        /// 创建默认参数配置
        /// </summary>
        public static SystemParameters CreateDefault()
        {
            return new SystemParameters
            {
                EnableLoadingModule = true,
                EnableUnloadingModule = true,
                LoadingBinSelection = BinSelection.Bin1,
                UnloadingBinSelection = BinSelection.Bin1,
                HighSpeedModeEnabled = false,
                ResetDelayMs = 4000,
                LowSpeedSetupDelayMs = 500,
                RingLineTimeoutSeconds = 30,
                SafetyDoorAlarmBypass = false,
                EmptyCartReserveCount = 0,
                CartCheckMode = CartCheckMode.EmptyCart,
                CuttingBridgeCarReserveCount = 0,
                CodeReaderType = "Datalogic",
                CodeReaderIp = "192.168.3.100",
                CodeReaderPort = 51236,
                CodeReaderTriggerCommand = "T",
                CodeReaderConnectionTimeoutMs = 3000,
                CodeReaderReceiveTimeoutMs = 5000,
                CodeReaderScanRetryCount = 3,
                MesEnabled = false,
                MesBrokerHost = "localhost",
                MesBrokerPort = 1883,
                MesUserName = string.Empty,
                MesPassword = string.Empty,
                MesClientId = string.Empty,
                MesCleanSession = true,
                MesKeepAliveSeconds = 30,
                MesRingLineDeviceId = string.Empty,
                MesRingLineDeviceCode = string.Empty
            };
        }

        /// <summary>
        /// 验证参数有效性
        /// </summary>
        public bool Validate()
        {
            var baseValid = ResetDelayMs > 0
                            && LowSpeedSetupDelayMs > 0
                            && RingLineTimeoutSeconds > 0
                            && EmptyCartReserveCount >= 0
                            && CuttingBridgeCarReserveCount >= 0
                            && CodeReaderScanRetryCount > 0
                            && Enum.IsDefined(typeof(BinSelection), LoadingBinSelection)
                            && Enum.IsDefined(typeof(BinSelection), UnloadingBinSelection)
                            && Enum.IsDefined(typeof(CartCheckMode), CartCheckMode);

            if (!baseValid)
            {
                return false;
            }

            if (!MesEnabled)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(MesBrokerHost))
            {
                return false;
            }

            if (MesBrokerPort <= 0 || MesBrokerPort > 65535)
            {
                return false;
            }

            if (MesKeepAliveSeconds <= 0 || MesKeepAliveSeconds > ushort.MaxValue)
            {
                return false;
            }

            return true;
        }
    }
}
