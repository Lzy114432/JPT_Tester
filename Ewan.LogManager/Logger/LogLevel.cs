// 向后兼容性转发 - 请使用 EwanCommon.Logging.LogLevel
namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// 日志级别枚举 - 已迁移到 EwanCommon.Logging.LogLevel
    /// </summary>
    [System.Obsolete("请使用 EwanCommon.Logging.LogLevel")]
    public enum LogLevel
    {
        Debug = EwanCommon.Logging.LogLevel.Debug,
        Info = EwanCommon.Logging.LogLevel.Info,
        Warn = EwanCommon.Logging.LogLevel.Warn,
        Error = EwanCommon.Logging.LogLevel.Error,
        Fatal = EwanCommon.Logging.LogLevel.Fatal
    }
}
