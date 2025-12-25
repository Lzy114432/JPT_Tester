using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ewan.CodeReader.Interfaces;

namespace Ewan.CodeReader.Scanners
{
    /// <summary>
    /// 得利捷扫描器实现 - 基于TCP通信
    /// </summary>
    public class DatalogicScanner : IScanner
    {
        private readonly object _syncLock = new object();
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private volatile bool _isConnected = false;
        private volatile bool _isScanning = false;
        private volatile bool _disposed = false;
        private Thread _receiveThread = null;
        private ScannerDeviceInfo _currentDevice = null;

        private const string DefaultTriggerCommand = "T";
        private const int DefaultConnectionTimeout = 3000;
        private const int DefaultReceiveTimeout = 5000;

        public event EventHandler<ScanResultEventArgs> OnScanResult;
        public event EventHandler<ScannerExceptionEventArgs> OnException;
        public event EventHandler<ConnectionStatusChangedEventArgs> OnConnectionStatusChanged;

        /// <summary>
        /// 触发命令，默认为"T"
        /// </summary>
        public string TriggerCommand { get; set; } = DefaultTriggerCommand;

        /// <summary>
        /// 连接超时时间（毫秒），默认3000ms
        /// </summary>
        public int ConnectionTimeout { get; set; } = DefaultConnectionTimeout;

        /// <summary>
        /// 接收超时时间（毫秒），默认5000ms
        /// </summary>
        public int ReceiveTimeout { get; set; } = DefaultReceiveTimeout;

        public bool IsConnected => _isConnected;
        public bool IsScanning => _isScanning;
        public ScannerDeviceInfo CurrentDevice => _currentDevice;

        /// <summary>
        /// 枚举设备 - 得利捷使用TCP连接，需手动指定IP和端口
        /// 可使用 CreateDeviceInfo 方法创建设备信息
        /// </summary>
        public List<ScannerDeviceInfo> EnumerateDevices()
        {
            // TCP设备无法自动枚举，返回空列表
            return new List<ScannerDeviceInfo>();
        }

        /// <summary>
        /// 创建设备信息（用于TCP连接）
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口</param>
        /// <param name="name">设备名称（可选）</param>
        public static ScannerDeviceInfo CreateDeviceInfo(string ip, int port, string name = null)
        {
            return new ScannerDeviceInfo
            {
                DeviceId = $"{ip}:{port}",
                DeviceName = name ?? $"Datalogic Scanner ({ip}:{port})",
                Manufacturer = "Datalogic",
                Model = "TCP Scanner",
                IpAddress = ip,
                RawDeviceInfo = new DatalogicConnectionInfo { IpAddress = ip, Port = port }
            };
        }

        public bool Connect(ScannerDeviceInfo device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            lock (_syncLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DatalogicScanner));
                if (_isConnected) return false;

                try
                {
                    string ip;
                    int port;

                    if (device.RawDeviceInfo is DatalogicConnectionInfo connInfo)
                    {
                        ip = connInfo.IpAddress;
                        port = connInfo.Port;
                    }
                    else
                    {
                        throw new ArgumentException("设备信息无效，请使用 CreateDeviceInfo 方法创建设备信息", nameof(device));
                    }

                    _tcpClient = new TcpClient();
                    var result = _tcpClient.BeginConnect(ip, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(ConnectionTimeout);

                    if (success && _tcpClient.Connected)
                    {
                        _tcpClient.EndConnect(result);
                        _networkStream = _tcpClient.GetStream();
                        _isConnected = true;
                        _currentDevice = device;
                        RaiseConnectionStatusChanged(true, $"已连接到 {ip}:{port}");
                        return true;
                    }
                    else
                    {
                        _tcpClient?.Close();
                        _tcpClient = null;
                        RaiseException(-1, "连接超时");
                        RaiseConnectionStatusChanged(false, "连接超时");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _tcpClient?.Close();
                    _tcpClient = null;
                    _isConnected = false;
                    RaiseException(-1, $"连接失败: {ex.Message}");
                    RaiseConnectionStatusChanged(false, $"连接失败: {ex.Message}");
                    return false;
                }
            }
        }

        public bool Disconnect()
        {
            lock (_syncLock)
            {
                if (!_isConnected) return false;

                try
                {
                    if (_isScanning)
                    {
                        StopScanInternal();
                    }

                    _networkStream?.Close();
                    _networkStream = null;

                    _tcpClient?.Close();
                    _tcpClient = null;

                    _isConnected = false;
                    var device = _currentDevice;
                    _currentDevice = null;
                    RaiseConnectionStatusChanged(false, "已断开连接");
                    return true;
                }
                catch (Exception ex)
                {
                    RaiseException(-1, ex.Message);
                    return false;
                }
            }
        }

        public bool StartScan()
        {
            lock (_syncLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DatalogicScanner));
                if (!_isConnected || _isScanning) return false;

                try
                {
                    _isScanning = true;
                    _receiveThread = new Thread(ReceiveThreadProcess);
                    _receiveThread.IsBackground = true;
                    _receiveThread.Start();
                    return true;
                }
                catch (Exception ex)
                {
                    _isScanning = false;
                    RaiseException(-1, ex.Message);
                    return false;
                }
            }
        }

        public bool StopScan()
        {
            lock (_syncLock)
            {
                return StopScanInternal();
            }
        }

        private bool StopScanInternal()
        {
            if (!_isScanning) return false;

            try
            {
                _isScanning = false;
                if (_receiveThread != null && _receiveThread.IsAlive)
                {
                    _receiveThread.Join(3000);
                }
                _receiveThread = null;
                return true;
            }
            catch (Exception ex)
            {
                RaiseException(-1, ex.Message);
                return false;
            }
        }

        public bool TriggerScan()
        {
            lock (_syncLock)
            {
                if (!_isConnected || _networkStream == null) return false;

                try
                {
                    byte[] data = Encoding.ASCII.GetBytes(TriggerCommand);
                    _networkStream.Write(data, 0, data.Length);
                    _networkStream.Flush();
                    return true;
                }
                catch (Exception ex)
                {
                    RaiseException(-1, $"触发扫描失败: {ex.Message}");
                    _isConnected = false;
                    RaiseConnectionStatusChanged(false, "连接异常断开");
                    return false;
                }
            }
        }

        /// <summary>
        /// 同步触发扫描并等待结果
        /// </summary>
        /// <returns>扫描结果，失败返回空字符串</returns>
        public string TriggerScanSync()
        {
            lock (_syncLock)
            {
                if (!_isConnected || _networkStream == null)
                {
                    return "";
                }

                try
                {
                    byte[] data = Encoding.ASCII.GetBytes(TriggerCommand);
                    _networkStream.Write(data, 0, data.Length);
                    _networkStream.Flush();

                    return ReceiveScanResult();
                }
                catch (Exception ex)
                {
                    RaiseException(-1, $"触发扫描失败: {ex.Message}");
                    _isConnected = false;
                    return "";
                }
            }
        }

        private string ReceiveScanResult()
        {
            try
            {
                if (_networkStream == null || !_networkStream.CanRead)
                {
                    return "";
                }

                _networkStream.ReadTimeout = ReceiveTimeout;

                byte[] buffer = new byte[1024];
                StringBuilder result = new StringBuilder();

                DateTime startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalMilliseconds < ReceiveTimeout)
                {
                    if (_networkStream.DataAvailable)
                    {
                        int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            result.Append(receivedData);

                            string currentResult = result.ToString();
                            if (currentResult.Contains("\r") || currentResult.Contains("\n"))
                            {
                                string cleanResult = currentResult.Trim('\r', '\n', ' ');
                                if (!string.IsNullOrEmpty(cleanResult))
                                {
                                    return cleanResult;
                                }
                            }
                        }
                    }

                    Thread.Sleep(10);
                }

                return "";
            }
            catch (Exception ex)
            {
                RaiseException(-1, $"接收扫码结果错误: {ex.Message}");
                return "";
            }
        }

        private void ReceiveThreadProcess()
        {
            byte[] buffer = new byte[1024];
            StringBuilder dataBuffer = new StringBuilder();

            try
            {
                while (_isScanning && _networkStream != null)
                {
                    try
                    {
                        if (_networkStream.DataAvailable)
                        {
                            int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                string receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                                dataBuffer.Append(receivedData);

                                string currentData = dataBuffer.ToString();
                                if (currentData.Contains("\r") || currentData.Contains("\n"))
                                {
                                    string cleanResult = currentData.Trim('\r', '\n', ' ');
                                    dataBuffer.Clear();

                                    if (!string.IsNullOrEmpty(cleanResult))
                                    {
                                        var eventArgs = new ScanResultEventArgs
                                        {
                                            Results = new List<ScanResult>
                                            {
                                                new ScanResult
                                                {
                                                    Code = cleanResult,
                                                    CodeType = "Unknown",
                                                    ScanTime = DateTime.Now,
                                                    ProcessTime = 0,
                                                    Quality = 0
                                                }
                                            },
                                            ImageData = null,
                                            ImageWidth = 0,
                                            ImageHeight = 0
                                        };
                                        RaiseScanResult(eventArgs);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单次读取异常
                    }

                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                RaiseException(-1, $"接收线程异常: {ex.Message}");
            }
        }

        // 得利捷TCP扫码器不支持以下参数设置
        public bool SetTriggerMode(bool isTriggerMode) => true;
        public float GetExposureTime() => 0;
        public bool SetExposureTime(float value) => false;
        public float GetGain() => 0;
        public bool SetGain(float value) => false;

        private void RaiseScanResult(ScanResultEventArgs args)
        {
            OnScanResult?.Invoke(this, args);
        }

        private void RaiseException(int errorCode, string message)
        {
            OnException?.Invoke(this, new ScannerExceptionEventArgs
            {
                ErrorCode = errorCode,
                ErrorMessage = message
            });
        }

        private void RaiseConnectionStatusChanged(bool isConnected, string message)
        {
            OnConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
            {
                IsConnected = isConnected,
                Message = message,
                Device = _currentDevice
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_syncLock)
                {
                    if (_isScanning)
                    {
                        StopScanInternal();
                    }

                    try
                    {
                        _networkStream?.Close();
                        _tcpClient?.Close();
                    }
                    catch { }

                    _networkStream = null;
                    _tcpClient = null;
                    _isConnected = false;
                    _currentDevice = null;
                }
            }

            _disposed = true;
        }

        ~DatalogicScanner()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 得利捷TCP连接信息
    /// </summary>
    public class DatalogicConnectionInfo
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
    }
}
