// 向后兼容性转发 - 请使用 EwanCommon.Logging
using EwanCommon.Logging;
using log4net;
using System;

namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// 日志管理器 - 已迁移到 EwanCommon.Logging
    /// 使用 Log4NetBootstrapper 进行约定式配置
    /// </summary>
    [System.Obsolete("请使用 EwanCommon.Logging.Log4NetBootstrapper")]
    public static class LogManager
    {
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 初始化日志系统
        /// 使用 EwanCommon.Logging.Log4NetBootstrapper 进行约定式配置
        /// </summary>
        /// <param name="configFilePath">log4net配置文件路径（可选，默认使用约定式配置）</param>
        public static void Initialize(string configFilePath = null)
        {
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;

                try
                {
                    bool configured;

                    if (!string.IsNullOrEmpty(configFilePath))
                    {
                        // 方案 A：显式指定配置文件路径
                        configured = Log4NetBootstrapper.TryConfigureFromFile(configFilePath, watch: true);
                    }
                    else
                    {
                        // 方案 B：约定式配置 - 从程序目录加载 log4net.config
                        configured = Log4NetBootstrapper.TryConfigureByConvention(configFileName: "log4net.config", watch: true);
                    }

                    if (!configured)
                    {
                        // 如果约定式配置失败，使用基础配置
                        log4net.Config.BasicConfigurator.Configure();
                    }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to initialize log4net: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// 获取日志记录器
        /// </summary>
        /// <param name="loggerName">日志记录器名称</param>
        /// <returns>ILog实例</returns>
        public static ILog GetLogger(string loggerName)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            return log4net.LogManager.GetLogger(loggerName);
        }

        /// <summary>
        /// 获取日志记录器（使用 EwanCommon.Logging.Log）
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>ILog实例</returns>
        public static ILog GetLogger(Type type)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            // 使用 EwanCommon.Logging.Log 统一入口
            return Log.GetLogger(type);
        }

        /// <summary>
        /// 获取日志记录器（泛型版本）
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <returns>ILog实例</returns>
        public static ILog GetLogger<T>()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            return Log.GetLogger<T>();
        }

        /// <summary>
        /// 关闭日志系统
        /// </summary>
        public static void Shutdown()
        {
            log4net.LogManager.Shutdown();
            _isInitialized = false;
        }

        /// <summary>
        /// 创建文件日志记录器
        /// </summary>
        /// <param name="loggerName">日志记录器名称</param>
        /// <param name="resourceType">资源类型（用于国际化）</param>
        /// <returns>FileLogger实例</returns>
        public static EwanCommon.Logging.FileLogger CreateFileLogger(string loggerName, Type resourceType = null)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            return new EwanCommon.Logging.FileLogger(loggerName, resourceType);
        }
    }
}
