using System;

namespace EwanCommon.Logging
{
    /// <summary>
    /// 应用程序日志记录器
    /// 用于记录系统级别的日志到app.log
    /// </summary>
    public class AppLogger : FileLogger
    {
        private static readonly Lazy<AppLogger> _instance = new Lazy<AppLogger>(() => new AppLogger());

        /// <summary>
        /// 获取AppLogger实例
        /// </summary>
        public static AppLogger Instance => _instance.Value;

        /// <summary>
        /// 私有构造函数，确保单例
        /// </summary>
        protected AppLogger() : base("root")
        {
            // 使用root logger配置，写入app.log
        }
    }
}
