using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;

namespace Ewan.LogManager.Logger
{
    /// <summary>
    /// 日志管理器 - 负责初始化和配置日志系统
    /// </summary>
    public static class LogManager
    {
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        /// <param name="configFilePath">log4net配置文件路径（可选）</param>
        public static void Initialize(string configFilePath = null)
        {
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;

                try
                {
                    if (!string.IsNullOrEmpty(configFilePath))
                    {
                        // 使用指定的配置文件
                        var configFile = new FileInfo(configFilePath);
                        if (configFile.Exists)
                        {
                            XmlConfigurator.ConfigureAndWatch(configFile);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Log4net configuration file not found: {configFilePath}");
                        }
                    }
                    else
                    {
                        // 尝试从程序集目录加载 log4net.config
                        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                        var defaultConfigPath = Path.Combine(assemblyDirectory, "log4net.config");
                        
                        if (File.Exists(defaultConfigPath))
                        {
                            XmlConfigurator.ConfigureAndWatch(new FileInfo(defaultConfigPath));
                        }
                        else
                        {
                            // 如果没有找到配置文件，使用默认配置
                            BasicConfigurator.Configure();
                        }
                    }

                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    // 记录初始化错误到控制台
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
        /// 获取日志记录器
        /// </summary>
        /// <param name="type">类型</param>
        /// <returns>ILog实例</returns>
        public static ILog GetLogger(Type type)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            return log4net.LogManager.GetLogger(type);
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
        public static FileLogger CreateFileLogger(string loggerName, Type resourceType = null)
        {
            if (!_isInitialized)
            {
                Initialize();
            }
            return new FileLogger(loggerName, resourceType);
        }
    }
}