// 向后兼容性转发 - 请使用 EwanCommon.Logging.AppLogger
namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// 应用程序日志记录器 - 已迁移到 EwanCommon.Logging.AppLogger
    /// </summary>
    [System.Obsolete("请使用 EwanCommon.Logging.AppLogger")]
    public class AppLogger : EwanCommon.Logging.AppLogger
    {
        /// <summary>
        /// 获取AppLogger实例
        /// </summary>
        public new static EwanCommon.Logging.AppLogger Instance => EwanCommon.Logging.AppLogger.Instance;
    }
}
