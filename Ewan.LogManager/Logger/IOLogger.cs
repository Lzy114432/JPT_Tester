// 向后兼容性转发 - 请使用 EwanCommon.Logging.IOLogger
namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// IO操作专用日志记录器 - 已迁移到 EwanCommon.Logging.IOLogger
    /// </summary>
    [System.Obsolete("请使用 EwanCommon.Logging.IOLogger")]
    public class IOLogger : EwanCommon.Logging.IOLogger
    {
        /// <summary>
        /// 获取IOLogger实例
        /// </summary>
        public new static EwanCommon.Logging.IOLogger Instance => EwanCommon.Logging.IOLogger.Instance;
    }
}
