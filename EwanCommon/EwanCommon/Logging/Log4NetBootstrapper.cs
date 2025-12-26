using log4net.Config;
using log4net.Repository;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace EwanCommon.Logging
{
    /// <summary>
    /// 约定式日志初始化：默认从程序目录加载 log4net.config（存在则配置，不存在则忽略）
    /// </summary>
    public static class Log4NetBootstrapper
    {
        private static readonly object s_lock = new object();
        private static int s_configured;

        /// <summary>
        /// 方案 A（纯代码配置）：显式指定配置文件路径（存在则配置，不存在则忽略）
        /// </summary>
        /// <param name="configPath">配置文件完整路径。</param>
        /// <param name="watch">是否监视配置文件变化。</param>
        /// <param name="repositoryAssembly">log4net repository 归属程序集（可为空）。</param>
        /// <returns>是否已配置（已配置也会返回 true）。</returns>
        public static bool TryConfigureFromFile(string configPath, bool watch = true, Assembly repositoryAssembly = null)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                return false;
            }

            if (Volatile.Read(ref s_configured) == 1)
            {
                return true;
            }

            lock (s_lock)
            {
                if (s_configured == 1)
                {
                    return true;
                }

                try
                {
                    repositoryAssembly ??= Log.RepositoryAssembly ?? Assembly.GetEntryAssembly() ?? typeof(Log4NetBootstrapper).Assembly;
                    ILoggerRepository repo = log4net.LogManager.GetRepository(repositoryAssembly);
                    if (repo.Configured)
                    {
                        s_configured = 1;
                        return true;
                    }

                    if (!File.Exists(configPath))
                    {
                        return false;
                    }

                    var fileInfo = new FileInfo(configPath);
                    if (watch)
                    {
                        XmlConfigurator.ConfigureAndWatch(repo, fileInfo);
                    }
                    else
                    {
                        XmlConfigurator.Configure(repo, fileInfo);
                    }

                    s_configured = 1;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 方案 B（约定式）：从程序目录查找 <paramref name="configFileName"/> 并配置。
        /// </summary>
        /// <param name="configFileName">配置文件名（默认 log4net.config）。</param>
        /// <param name="watch">是否监视配置文件变化。</param>
        /// <param name="repositoryAssembly">log4net repository 归属程序集（可为空）。</param>
        /// <returns>是否已配置成功。</returns>
        public static bool TryConfigureByConvention(string configFileName = "log4net.config", bool watch = true, Assembly repositoryAssembly = null)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            var configPath = Path.Combine(baseDir, configFileName ?? "log4net.config");
            return TryConfigureFromFile(configPath, watch, repositoryAssembly);
        }
    }
}
