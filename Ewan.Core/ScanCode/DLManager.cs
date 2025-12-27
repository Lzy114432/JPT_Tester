using EwanCore.Attribute;
using Ewan.CodeReader;
using Ewan.CodeReader.Interfaces;
using Ewan.CodeReader.Scanners;
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
    public class DLManager : BaseManager<DLManager>
    {
        #region 私有字段

        private IScanner _scanner;
        private ScannerDeviceInfo _scannerDevice;
        private readonly object _connectionLock = new object();

        private const string DefaultScannerIp = "192.168.3.100";
        private const int DefaultScannerPort = 51236;
        private const string DefaultTriggerCommand = "T";
        private const int DefaultConnectionTimeoutMs = 3000;
        private const int DefaultReceiveTimeoutMs = 5000;
        private const ScannerType DefaultScannerType = ScannerType.Datalogic;

        private ScannerType _scannerType = DefaultScannerType;
        private string _scannerIp = DefaultScannerIp;
        private int _scannerPort = DefaultScannerPort;
        private string _triggerCommand = DefaultTriggerCommand;
        private int _connectionTimeoutMs = DefaultConnectionTimeoutMs;
        private int _receiveTimeoutMs = DefaultReceiveTimeoutMs;
        
        // 重连参数
        private bool _autoReconnect = true;

        #endregion

        #region 初始化和销毁

        public override bool Init()
        {
            _uiLogger.InfoRaw("模块初始化成功: {0}", "DLManager");
            
            try
            {
                // 启动连接
                ConnectToScanner();
                          
                return base.Init();
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("初始化失败: {0}", "DLManager初始化失败: " + ex.Message);
                return false;
            }
        }

        public override void Destroy()
        {
            _uiLogger.InfoRaw("模块已销毁: {0}", "DLManager");
            
            try
            {          
                // 断开连接
                DisconnectFromScanner();
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "DLManager销毁错误: " + ex.Message);
            }
            
            base.Destroy();
        }

        #endregion

        #region TCP连接管理

        /// <summary>
        /// 连接到扫码器
        /// </summary>
        public bool ConnectToScanner()
        {
            lock (_connectionLock)
            {
                try
                {
                    LoadScannerSettings();

                    _uiLogger.InfoRaw("处理已开始: {0}", 
                        $"连接扫码器({_scannerType}) {_scannerIp}:{_scannerPort}");

                    DisconnectFromScanner();

                    _scanner = ScannerFactory.CreateScanner(_scannerType);
                    ApplyScannerSettings(_scanner);

                    _scanner.OnException += OnScannerException;
                    _scanner.OnConnectionStatusChanged += OnScannerConnectionStatusChanged;

                    _scannerDevice = CreateScannerDeviceInfo(_scannerType, _scannerIp, _scannerPort);
                    bool connected = _scanner.Connect(_scannerDevice);

                    if (!connected)
                    {
                        _uiLogger.ErrorRaw("操作失败: {0}", "扫码器连接失败");
                        DisconnectFromScanner();
                        return false;
                    }

                    _uiLogger.InfoRaw("处理已完成: {0}", "扫码器连接成功");
                    return true;
                }
                catch (Exception ex)
                {
                    DisconnectFromScanner();
                    
                    _uiLogger.ErrorRaw("操作失败: {0}", "扫码器连接失败: " + ex.Message);
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
                    
                    _uiLogger.InfoRaw("处理已完成: {0}", "扫码器连接已断开");
                    
                }
                catch (Exception ex)
                {
                    _uiLogger.ErrorRaw("处理错误: {0} - {1}", "断开扫码器连接错误: " + ex.Message);
                }
            }
        }

        #endregion

        #region 扫码控制

        /// <summary>
        /// 触发扫码并接收二维码信息
        /// </summary>
        /// <returns>扫码结果，失败返回空字符串</returns>
        public string TriggerScan()
        {
            lock (_connectionLock)
            {
                try
                {
                    if (_scanner == null || !_scanner.IsConnected)
                    {
                        if (_autoReconnect)
                        {
                            ConnectToScanner();
                        }

                        if (_scanner == null || !_scanner.IsConnected)
                        {
                            _uiLogger.ErrorRaw("操作失败: {0}", "扫码器未连接，无法触发扫码");
                            return "";
                        }
                    }

                    _uiLogger.InfoRaw("处理已开始: {0}", "发送扫码触发命令: " + _triggerCommand);
                    
                    string rawResult = TriggerScanInternal();
                    string scanResult = NormalizeScanResult(rawResult);
                    
                    if (!string.IsNullOrEmpty(scanResult))
                    {
                        _uiLogger.InfoRaw("处理已完成: {0}", "扫码成功，结果: " + scanResult);
                    }
                    else
                    {
                        string rawText = rawResult?.Trim();
                        if (!string.IsNullOrWhiteSpace(rawText))
                        {
                            _uiLogger.ErrorRaw("操作失败: {0}", "扫码失败，返回: " + rawText);
                        }
                        else
                        {
                            _uiLogger.ErrorRaw("操作失败: {0}", "扫码失败，未收到有效结果");
                        }
                    }
                    
                    return scanResult;
                }
                catch (Exception ex)
                {
                    _uiLogger.ErrorRaw("操作失败: {0}", "触发扫码失败: " + ex.Message);
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
        public string ScannerIP => _scannerIp;

        /// <summary>
        /// 获取扫码器端口
        /// </summary>
        public int ScannerPort => _scannerPort;

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
            _uiLogger.InfoRaw("处理已开始: {0}", "手动重连扫码器");
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
                    return $"已连接到 {_scannerIp}:{_scannerPort}";
                }
                else
                {
                    return "未连接";
                }
            }
        }

        #endregion

        #region 扫码器配置

        private void LoadScannerSettings()
        {
            try
            {
                var parameters = SystemParametersManager.Instance?.Parameters;
                if (parameters == null)
                {
                    ApplyDefaultScannerSettings();
                    return;
                }

                _scannerType = ParseScannerType(parameters.CodeReaderType);
                _scannerIp = string.IsNullOrWhiteSpace(parameters.CodeReaderIp) ? DefaultScannerIp : parameters.CodeReaderIp.Trim();
                _scannerPort = parameters.CodeReaderPort > 0 && parameters.CodeReaderPort <= 65535
                    ? parameters.CodeReaderPort
                    : DefaultScannerPort;
                _triggerCommand = string.IsNullOrWhiteSpace(parameters.CodeReaderTriggerCommand)
                    ? DefaultTriggerCommand
                    : parameters.CodeReaderTriggerCommand;
                _connectionTimeoutMs = parameters.CodeReaderConnectionTimeoutMs > 0
                    ? parameters.CodeReaderConnectionTimeoutMs
                    : DefaultConnectionTimeoutMs;
                _receiveTimeoutMs = parameters.CodeReaderReceiveTimeoutMs > 0
                    ? parameters.CodeReaderReceiveTimeoutMs
                    : DefaultReceiveTimeoutMs;
            }
            catch
            {
                ApplyDefaultScannerSettings();
            }
        }

        private void ApplyDefaultScannerSettings()
        {
            _scannerType = DefaultScannerType;
            _scannerIp = DefaultScannerIp;
            _scannerPort = DefaultScannerPort;
            _triggerCommand = DefaultTriggerCommand;
            _connectionTimeoutMs = DefaultConnectionTimeoutMs;
            _receiveTimeoutMs = DefaultReceiveTimeoutMs;
        }

        private static ScannerType ParseScannerType(string typeText)
        {
            if (!string.IsNullOrWhiteSpace(typeText) &&
                Enum.TryParse(typeText.Trim(), ignoreCase: true, out ScannerType type))
            {
                return type;
            }

            return DefaultScannerType;
        }

        private void ApplyScannerSettings(IScanner scanner)
        {
            if (scanner == null)
            {
                return;
            }

            if (scanner is DatalogicScanner datalogicScanner)
            {
                datalogicScanner.TriggerCommand = _triggerCommand;
                datalogicScanner.ConnectionTimeout = _connectionTimeoutMs;
                datalogicScanner.ReceiveTimeout = _receiveTimeoutMs;
            }
            else if (scanner is HikvisionScanner hikvisionScanner)
            {
                hikvisionScanner.ReceiveTimeout = _receiveTimeoutMs;
            }
        }

        private static ScannerDeviceInfo CreateScannerDeviceInfo(ScannerType type, string ip, int port)
        {
            switch (type)
            {
                case ScannerType.Datalogic:
                    return DatalogicScanner.CreateDeviceInfo(ip, port);
                case ScannerType.Hikvision:
                    return HikvisionScanner.CreateDeviceInfo(ip);
                default:
                    throw new NotSupportedException($"不支持的扫码器类型: {type}");
            }
        }

        private string TriggerScanInternal()
        {
            if (_scanner == null)
            {
                return "";
            }

            if (_scanner is DatalogicScanner datalogicScanner)
            {
                return datalogicScanner.TriggerScanSync();
            }

            return TriggerScanByEvent(_receiveTimeoutMs);
        }

        private string TriggerScanByEvent(int timeoutMs)
        {
            try
            {
                string scanResult = "";
                int waitTimeoutMs = timeoutMs > 0 ? timeoutMs : DefaultReceiveTimeoutMs;

                using (var waitHandle = new ManualResetEventSlim(false))
                {
                    EventHandler<ScanResultEventArgs> handler = (sender, args) =>
                    {
                        try
                        {
                            if (args?.Results == null || args.Results.Count == 0)
                            {
                                return;
                            }

                            string invalidResult = "";

                            for (int i = 0; i < args.Results.Count; i++)
                            {
                                string code = args.Results[i]?.Code;
                                if (string.IsNullOrWhiteSpace(code))
                                {
                                    continue;
                                }

                                string trimmed = code.Trim();
                                string normalized = NormalizeScanResult(trimmed);

                                if (!string.IsNullOrEmpty(normalized))
                                {
                                    scanResult = normalized;
                                    waitHandle.Set();
                                    return;
                                }

                                if (string.Equals(trimmed, "NG", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(trimmed, "NoRead", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(trimmed, "NOREAD", StringComparison.OrdinalIgnoreCase))
                                {
                                    invalidResult = trimmed;
                                }
                            }

                            if (!string.IsNullOrEmpty(invalidResult))
                            {
                                scanResult = invalidResult;
                                waitHandle.Set();
                            }
                        }
                        catch
                        {
                        }
                    };

                    _scanner.OnScanResult += handler;

                    try
                    {
                        if (!_scanner.IsScanning)
                        {
                            if (!_scanner.StartScan())
                            {
                                return "";
                            }
                        }

                        _scanner.SetTriggerMode(true);

                        if (!_scanner.TriggerScan())
                        {
                            return "";
                        }

                        if (!waitHandle.Wait(waitTimeoutMs))
                        {
                            return "";
                        }

                        return scanResult;
                    }
                    finally
                    {
                        try
                        {
                            _scanner.OnScanResult -= handler;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        private static string NormalizeScanResult(string scanResult)
        {
            if (scanResult == null)
            {
                return "";
            }

            string normalized = scanResult.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "";
            }

            if (string.Equals(normalized, "NG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "NoRead", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "NOREAD", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return normalized;
        }

        #endregion

        #region 扫码器事件

        private void OnScannerException(object sender, ScannerExceptionEventArgs e)
        {
            try
            {
                _uiLogger.ErrorRaw("操作失败: {0}", $"扫码器异常: ({e?.ErrorCode}) {e?.ErrorMessage}");
            }
            catch
            {
            }
        }

        private void OnScannerConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            try
            {
                _uiLogger.InfoRaw("处理已完成: {0}",
                    $"扫码器连接状态: {(e?.IsConnected == true ? "已连接" : "已断开")} {e?.Message}");
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
        private void SendConnectionStatusMessage(bool isConnected)
        {
            try
            {
                // TODO: 创建并发送连接状态消息
                // var statusMsg = new ScannerConnectionMessage(isConnected, _scannerIp, _scannerPort);
                // _msgManager.PushMsg(new MessageModel(MsgSubject.ScannerStatus, statusMsg));
                
                _uiLogger.InfoRaw("处理已完成: {0}", 
                    $"发送扫码器连接状态消息: {(isConnected ? "已连接" : "已断开")}");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", 
                    "发送连接状态消息错误: " + ex.Message);
            }
        }

       

        #endregion

    }
}
