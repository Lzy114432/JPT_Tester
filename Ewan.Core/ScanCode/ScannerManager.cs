using EwanCore;
using EwanCore.Attribute;
using EwanCommon.Logging;
using log4net;
using Ewan.CodeReader;
using Ewan.CodeReader.Configuration;
using Ewan.CodeReader.Interfaces;
using Ewan.Model.System;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.ScanCode
{
    /// <summary>
    /// 扫码器管理器 - 使用 Ewan.CodeReader 统一扫码封装
    /// </summary>
    [Manager(Priority = 1)]
    public class ScannerManager : IManager
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(ScannerManager));
        private bool _disposed;

        #region 单例支持

        private static readonly Lazy<ScannerManager> s_instance = new Lazy<ScannerManager>(() => new ScannerManager());

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static ScannerManager Instance() => s_instance.Value;

        #endregion

        #region 私有字段

        private IScanner _scanner;
        private IScannerConfiguration _configuration;
        private ScannerDeviceInfo _scannerDevice;
        private readonly object _connectionLock = new object();

        // 默认配置
        private const string DefaultScannerIp = "192.168.3.11";
        private const int DefaultScannerPort = 51236;
        private const string DefaultTriggerCommand = "T";
        private const int DefaultConnectionTimeoutMs = 3000;
        private const int DefaultReceiveTimeoutMs = 5000;
        private const ScannerType DefaultScannerType = ScannerType.Datalogic;

        // 重连参数
        private bool _autoReconnect = true;

        #endregion

        #region 初始化和销毁

        /// <summary>
        /// 初始化扫码器管理器
        /// </summary>
        public bool Init()
        {
            s_logger.Info("ScannerManager 初始化开始");

            try
            {
                // 加载配置并连接
                LoadConfiguration();
                ConnectToScanner();

                s_logger.Info("ScannerManager 初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("ScannerManager 初始化失败: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_logger.Info("ScannerManager 开始销毁");

            try
            {
                DisconnectFromScanner();
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("ScannerManager 销毁错误: {0}", ex.Message);
            }

            s_logger.Info("ScannerManager 销毁完成");
        }

        /// <summary>
        /// 销毁（已过时，请使用 Dispose）
        /// </summary>
        [Obsolete("请使用 Dispose() 方法")]
        public void Destroy() => Dispose();

        #endregion

        #region 配置管理

        /// <summary>
        /// 加载扫码器配置
        /// </summary>
        protected virtual void LoadConfiguration()
        {
            try
            {
                var parameters = SystemParametersManager.Instance?.Parameters;
                if (parameters == null)
                {
                    _configuration = CreateDefaultConfiguration();
                    return;
                }

                var scannerType = ScannerFactory.ParseType(parameters.CodeReaderType, DefaultScannerType);
                _configuration = CreateConfigurationFromParameters(scannerType, parameters);
            }
            catch
            {
                _configuration = CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private IScannerConfiguration CreateDefaultConfiguration()
        {
            return new DatalogicConfiguration
            {
                IpAddress = DefaultScannerIp,
                Port = DefaultScannerPort,
                TriggerCommand = DefaultTriggerCommand,
                ConnectionTimeoutMs = DefaultConnectionTimeoutMs,
                ReceiveTimeoutMs = DefaultReceiveTimeoutMs,
            };
        }

        /// <summary>
        /// 从系统参数创建配置
        /// </summary>
        private IScannerConfiguration CreateConfigurationFromParameters(ScannerType type, SystemParameters parameters)
        {
            var config = ScannerFactory.CreateConfiguration(type);

            config.IpAddress = string.IsNullOrWhiteSpace(parameters.CodeReaderIp)
                ? DefaultScannerIp
                : parameters.CodeReaderIp.Trim();

            config.Port = parameters.CodeReaderPort > 0 && parameters.CodeReaderPort <= 65535
                ? parameters.CodeReaderPort
                : DefaultScannerPort;

            config.ConnectionTimeoutMs = parameters.CodeReaderConnectionTimeoutMs > 0
                ? parameters.CodeReaderConnectionTimeoutMs
                : DefaultConnectionTimeoutMs;

            config.ReceiveTimeoutMs = parameters.CodeReaderReceiveTimeoutMs > 0
                ? parameters.CodeReaderReceiveTimeoutMs
                : DefaultReceiveTimeoutMs;

            // 特定类型配置
            if (config is DatalogicConfiguration datalogicConfig)
            {
                datalogicConfig.TriggerCommand = string.IsNullOrWhiteSpace(parameters.CodeReaderTriggerCommand)
                    ? DefaultTriggerCommand
                    : parameters.CodeReaderTriggerCommand;
            }

            return config;
        }

        /// <summary>
        /// 当前配置
        /// </summary>
        public IScannerConfiguration Configuration => _configuration;

        /// <summary>
        /// 当前扫码器类型
        /// </summary>
        public ScannerType ScannerType => _configuration?.ScannerType ?? DefaultScannerType;

        #endregion

        #region 连接管理

        /// <summary>
        /// 连接到扫码器
        /// </summary>
        public bool ConnectToScanner()
        {
            lock (_connectionLock)
            {
                try
                {
                    if (_configuration == null)
                    {
                        LoadConfiguration();
                    }

                    s_logger.InfoFormat("连接扫码器({0}) {1}:{2}",
                        _configuration.ScannerType,
                        _configuration.IpAddress,
                        _configuration.Port);

                    DisconnectFromScanner();

                    _scanner = ScannerFactory.CreateScanner(_configuration.ScannerType);
                    _scanner.ApplyConfiguration(_configuration);

                    _scanner.OnException += OnScannerException;
                    _scanner.OnConnectionStatusChanged += OnScannerConnectionStatusChanged;

                    _scannerDevice = _configuration.CreateDeviceInfo();
                    bool connected = _scanner.Connect(_scannerDevice);

                    if (!connected)
                    {
                        s_logger.Error("扫码器连接失败");
                        DisconnectFromScanner();
                        return false;
                    }

                    s_logger.Info("扫码器连接成功");
                    return true;
                }
                catch (Exception ex)
                {
                    DisconnectFromScanner();
                    s_logger.ErrorFormat("扫码器连接失败: {0}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 断开扫码器连接
        /// </summary>
        public void DisconnectFromScanner()
        {
            lock (_connectionLock)
            {
                try
                {
                    if (_scanner != null)
                    {
                        try
                        {
                            _scanner.OnException -= OnScannerException;
                            _scanner.OnConnectionStatusChanged -= OnScannerConnectionStatusChanged;
                        }
                        catch
                        {
                        }

                        try
                        {
                            if (_scanner.IsScanning)
                            {
                                _scanner.StopScan();
                            }
                        }
                        catch
                        {
                        }

                        try
                        {
                            _scanner.Disconnect();
                        }
                        catch
                        {
                        }

                        try
                        {
                            _scanner.Dispose();
                        }
                        catch
                        {
                        }

                        _scanner = null;
                        _scannerDevice = null;
                    }

                    s_logger.Info("扫码器连接已断开");
                }
                catch (Exception ex)
                {
                    s_logger.ErrorFormat("断开扫码器连接错误: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// 确保已连接
        /// </summary>
        private void EnsureConnected()
        {
            if (_scanner == null || !_scanner.IsConnected)
            {
                if (_autoReconnect)
                {
                    ConnectToScanner();
                }

                if (_scanner == null || !_scanner.IsConnected)
                {
                    throw new InvalidOperationException("扫码器未连接");
                }
            }
        }

        #endregion

        #region 扫码控制

        /// <summary>
        /// 触发扫码并接收结果
        /// </summary>
        /// <returns>扫码结果，失败返回空字符串</returns>
        public string TriggerScan()
        {
            lock (_connectionLock)
            {
                try
                {
                    EnsureConnected();

                    s_logger.Info("发送扫码触发命令");

                    int timeoutMs = _configuration?.ReceiveTimeoutMs ?? DefaultReceiveTimeoutMs;
                    string rawResult = _scanner.TriggerScanSync(timeoutMs);
                    string scanResult = ScanResultNormalizer.Normalize(rawResult);

                    if (!string.IsNullOrEmpty(scanResult))
                    {
                        s_logger.InfoFormat("扫码成功，结果: {0}", scanResult);
                    }
                    else
                    {
                        string rawText = rawResult?.Trim();
                        if (!string.IsNullOrWhiteSpace(rawText))
                        {
                            s_logger.ErrorFormat("扫码失败，返回: {0}", rawText);
                        }
                        else
                        {
                            s_logger.Error("扫码失败，未收到有效结果");
                        }
                    }

                    return scanResult;
                }
                catch (InvalidOperationException ex)
                {
                    s_logger.ErrorFormat("触发扫码失败: {0}", ex.Message);
                    return "";
                }
                catch (Exception ex)
                {
                    s_logger.ErrorFormat("触发扫码失败: {0}", ex.Message);
                    return "";
                }
            }
        }

        /// <summary>
        /// 异步触发扫码
        /// </summary>
        /// <returns>扫码结果</returns>
        public async Task<string> TriggerScanAsync()
        {
            return await Task.Run(() => TriggerScan());
        }

        #endregion

        #region 公共属性和方法

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_connectionLock)
                {
                    return _scanner != null && _scanner.IsConnected;
                }
            }
        }

        /// <summary>
        /// 获取扫码器IP地址
        /// </summary>
        public string ScannerIP => _configuration?.IpAddress ?? DefaultScannerIp;

        /// <summary>
        /// 获取扫码器端口
        /// </summary>
        public int ScannerPort => _configuration?.Port ?? DefaultScannerPort;

        /// <summary>
        /// 启用/禁用自动重连
        /// </summary>
        public bool AutoReconnect
        {
            get => _autoReconnect;
            set => _autoReconnect = value;
        }

        /// <summary>
        /// 手动重连
        /// </summary>
        /// <returns>重连是否成功</returns>
        public bool Reconnect()
        {
            s_logger.Info("手动重连扫码器");
            return ConnectToScanner();
        }

        /// <summary>
        /// 获取连接状态信息
        /// </summary>
        /// <returns>连接状态描述</returns>
        public string GetConnectionStatus()
        {
            lock (_connectionLock)
            {
                if (_scanner != null && _scanner.IsConnected)
                {
                    return $"已连接到 {ScannerIP}:{ScannerPort} ({ScannerType})";
                }
                else
                {
                    return "未连接";
                }
            }
        }

        /// <summary>
        /// 获取当前扫码器实例（用于高级操作）
        /// </summary>
        public IScanner CurrentScanner => _scanner;

        #endregion

        #region 扫码器事件

        private void OnScannerException(object sender, ScannerExceptionEventArgs e)
        {
            try
            {
                s_logger.ErrorFormat("扫码器异常: ({0}) {1}", e?.ErrorCode, e?.ErrorMessage);
            }
            catch
            {
            }
        }

        private void OnScannerConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            try
            {
                s_logger.InfoFormat("扫码器连接状态: {0} {1}",
                    e?.IsConnected == true ? "已连接" : "已断开", e?.Message);
                SendConnectionStatusMessage(e?.IsConnected == true);
            }
            catch
            {
            }
        }

        #endregion

        #region 消息发送

        /// <summary>
        /// 发送连接状态消息
        /// </summary>
        /// <param name="isConnected">是否已连接</param>
        protected virtual void SendConnectionStatusMessage(bool isConnected)
        {
            try
            {
                // TODO: 创建并发送连接状态消息
                s_logger.InfoFormat("发送扫码器连接状态消息: {0}", isConnected ? "已连接" : "已断开");
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("发送连接状态消息错误: {0}", ex.Message);
            }
        }

        #endregion
    }
}
