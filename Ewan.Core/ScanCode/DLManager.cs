using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ewan.Core.Msg;

namespace Ewan.Core.ScanCode
{
    /// <summary>
    /// 扫码器管理器 - 通过TCP与扫码器通信
    /// </summary>
    [Manager(Priority = 1)]
    public class DLManager : BaseManager<DLManager>
    {
        #region 私有字段

        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isConnected = false;
        private readonly object _connectionLock = new object();
        
        // TCP连接参数
        private const string SCANNER_IP = "192.168.3.100";
        private const int SCANNER_PORT = 51236;
        private const string TRIGGER_COMMAND = "T";
        
        // 重连参数
        private Timer _reconnectTimer;
        private const int RECONNECT_INTERVAL = 5000; // 5秒重连间隔
        private bool _autoReconnect = true;
        
        // 消息队列
        private MsgManager _msgManager;

        #endregion

        #region 初始化和销毁

        public override bool Init()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "DLManager");
            
            try
            {
                // 启动连接
                ConnectToScanner();
                          
                return base.Init();
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.InitializationFailed, "DLManager初始化失败: " + ex.Message);
                return false;
            }
        }

        public override void Destroy()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "DLManager");
            
            try
            {          
                // 断开连接
                DisconnectFromScanner();
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "DLManager销毁错误: " + ex.Message);
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
                    // 如果已连接，先断开
                    if (_isConnected)
                    {
                        DisconnectFromScanner();
                    }

                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        $"连接扫码器 {SCANNER_IP}:{SCANNER_PORT}");

                    _tcpClient = new TcpClient();
                    
                    // 设置连接超时
                    var result = _tcpClient.BeginConnect(SCANNER_IP, SCANNER_PORT, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(3000); // 3秒超时
                    
                    if (success && _tcpClient.Connected)
                    {
                        _tcpClient.EndConnect(result);
                        _networkStream = _tcpClient.GetStream();
                        _isConnected = true;
                        
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "扫码器连接成功");
                        
                        
                        return true;
                    }
                    else
                    {
                        _tcpClient?.Close();
                        _tcpClient = null;
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "扫码器连接超时");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _tcpClient?.Close();
                    _tcpClient = null;
                    _isConnected = false;
                    
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "扫码器连接失败: " + ex.Message);
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
                    if (_networkStream != null)
                    {
                        _networkStream.Close();
                        _networkStream = null;
                    }

                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                        _tcpClient = null;
                    }

                    _isConnected = false;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "扫码器连接已断开");
                    
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "断开扫码器连接错误: " + ex.Message);
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
                    if (!_isConnected || _networkStream == null)
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "扫码器未连接，无法触发扫码");
                        return "";
                    }

                    // 发送触发命令
                    byte[] data = Encoding.ASCII.GetBytes(TRIGGER_COMMAND);
                    _networkStream.Write(data, 0, data.Length);
                    _networkStream.Flush();
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "发送扫码触发命令: " + TRIGGER_COMMAND);
                    
                    // 等待并接收扫码结果
                    string scanResult = ReceiveScanResult();
                    
                    if (!string.IsNullOrEmpty(scanResult))
                    {
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "扫码成功，结果: " + scanResult);
                    }
                    else
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "扫码失败，未收到有效结果");
                    }
                    
                    return scanResult;
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "触发扫码失败: " + ex.Message);
                    
                    // 连接可能已断开，标记为未连接
                    _isConnected = false;
                    return "";
                }
            }
        }

        /// <summary>
        /// 接收扫码结果
        /// </summary>
        /// <returns>扫码结果字符串</returns>
        private string ReceiveScanResult()
        {
            try
            {
                if (_networkStream == null || !_networkStream.CanRead)
                {
                    return "";
                }

                // 设置接收超时时间（5秒）
                _networkStream.ReadTimeout = 5000;
                
                byte[] buffer = new byte[1024];
                StringBuilder result = new StringBuilder();
                
                // 循环读取数据直到收到完整的扫码结果
                DateTime startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalMilliseconds < 5000) // 5秒超时
                {
                    if (_networkStream.DataAvailable)
                    {
                        int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            result.Append(receivedData);
                            
                            // 检查是否收到完整的数据（根据扫码器的协议判断）
                            // 通常扫码器会在数据末尾添加换行符或特定的结束符
                            string currentResult = result.ToString();
                            if (currentResult.Contains("\r") || currentResult.Contains("\n") || 
                                currentResult.Contains("\r\n"))
                            {
                                // 清理结果字符串
                                string cleanResult = currentResult.Trim('\r', '\n', ' ');
                                if (!string.IsNullOrEmpty(cleanResult))
                                {
                                    return cleanResult;
                                }
                            }
                        }
                    }
                    
                    // 短暂休眠避免占用过多CPU
                    Thread.Sleep(10);
                }
                
                // 超时或没有收到数据
                _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "接收扫码结果超时");
                return "";
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "接收扫码结果错误: " + ex.Message);
                return "";
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
                    return _isConnected;
                }
            }
        }

        /// <summary>
        /// 获取扫码器IP地址
        /// </summary>
        public string ScannerIP => SCANNER_IP;

        /// <summary>
        /// 获取扫码器端口
        /// </summary>
        public int ScannerPort => SCANNER_PORT;

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
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "手动重连扫码器");
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
                if (_isConnected)
                {
                    return $"已连接到 {SCANNER_IP}:{SCANNER_PORT}";
                }
                else
                {
                    return "未连接";
                }
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
                // var statusMsg = new ScannerConnectionMessage(isConnected, SCANNER_IP, SCANNER_PORT);
                // _msgManager.PushMsg(new MessageModel(MsgSubject.ScannerStatus, statusMsg));
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                    $"发送扫码器连接状态消息: {(isConnected ? "已连接" : "已断开")}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "发送连接状态消息错误: " + ex.Message);
            }
        }

       

        #endregion

    }
}
