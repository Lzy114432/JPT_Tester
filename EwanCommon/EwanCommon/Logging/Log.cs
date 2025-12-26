using log4net;
using System;
using System.Reflection;

namespace EwanCommon.Logging
{
    /// <summary>
    /// log4net 获取 logger 的统一入口。
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 指定 log4net 的 repositoryAssembly（默认 EntryAssembly；为空则回退到 type.Assembly）
        /// </summary>
        public static Assembly RepositoryAssembly { get; set; }

        /// <summary>
        /// 获取指定类型对应的 logger。
        /// </summary>
        /// <param name="type">类型。</param>
        public static ILog GetLogger(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var repoAsm = RepositoryAssembly ?? Assembly.GetEntryAssembly() ?? type.Assembly;
            return LogManager.GetLogger(repoAsm, type);
        }

        /// <summary>
        /// 获取指定类型对应的 logger。
        /// </summary>
        public static ILog GetLogger<T>()
        {
            return GetLogger(typeof(T));
        }
    }
}
