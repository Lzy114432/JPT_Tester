// 向后兼容性转发 - 请使用 EwanCommon.Logging.FileLogger
using System;

namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// 基础文件日志记录器 - 已迁移到 EwanCommon.Logging.FileLogger
    /// </summary>
    [System.Obsolete("请使用 EwanCommon.Logging.FileLogger")]
    public class FileLogger : EwanCommon.Logging.FileLogger
    {
        public FileLogger(string loggerName, Type resourceType = null)
            : base(loggerName, resourceType)
        {
        }
    }
}
