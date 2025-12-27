using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ewan.CodeReader.Configuration;
using Ewan.CodeReader.Interfaces;
#if HIKVISION_SDK
using MvCodeReaderSDKNet;
#endif

namespace Ewan.CodeReader.Scanners
{
#if HIKVISION_SDK
    /// <summary>
    /// 海康条码类型枚举
    /// </summary>
    public enum HikBarType : uint
    {
        DM = 1,
        QR = 2,
        EAN8 = 8,
        UPCE = 9,
        UPCA = 10,
        EAN13 = 11,
        Code39 = 14,
        Code93 = 15,
        Code128 = 16,
        ITF25 = 17,
        PDF417 = 51
    }

    /// <summary>
    /// 海康威视扫描器实现
    /// </summary>
    public class HikvisionScanner : IScanner
    {
        private readonly object _syncLock = new object();
        private MvCodeReader _device = null;
        private MvCodeReader.MV_CODEREADER_DEVICE_INFO_LIST _deviceList = new MvCodeReader.MV_CODEREADER_DEVICE_INFO_LIST();
        private volatile bool _isConnected = false;
        private volatile bool _isScanning = false;
        private volatile bool _disposed = false;
        private Thread _receiveThread = null;
        private ScannerDeviceInfo _currentDevice = null;

        private const int DefaultReceiveTimeout = 1000;
        private const int ThreadJoinTimeout = 3000;

        public event EventHandler<ScanResultEventArgs> OnScanResult;
        public event EventHandler<ScannerExceptionEventArgs> OnException;
        public event EventHandler<ConnectionStatusChangedEventArgs> OnConnectionStatusChanged;

        /// <summary>
        /// 接收帧超时时间（毫秒），默认1000ms
        /// </summary>
        public int ReceiveTimeout { get; set; } = DefaultReceiveTimeout;

        public bool IsConnected => _isConnected;
        public bool IsScanning => _isScanning;
        public ScannerDeviceInfo CurrentDevice => _currentDevice;

        public List<ScannerDeviceInfo> EnumerateDevices()
        {
            var devices = new List<ScannerDeviceInfo>();
            _deviceList.nDeviceNum = 0;

            int nRet = MvCodeReader.MV_CODEREADER_EnumDevices_NET(ref _deviceList, MvCodeReader.MV_CODEREADER_GIGE_DEVICE);
            if (nRet != MvCodeReader.MV_CODEREADER_OK || _deviceList.nDeviceNum == 0)
            {
                return devices;
            }

            for (int i = 0; i < _deviceList.nDeviceNum; i++)
            {
                var stDevInfo = (MvCodeReader.MV_CODEREADER_DEVICE_INFO)Marshal.PtrToStructure(
                    _deviceList.pDeviceInfo[i], typeof(MvCodeReader.MV_CODEREADER_DEVICE_INFO));

                if (stDevInfo.nTLayerType == MvCodeReader.MV_CODEREADER_GIGE_DEVICE)
                {
                    IntPtr buffer = Marshal.UnsafeAddrOfPinnedArrayElement(stDevInfo.SpecialInfo.stGigEInfo, 0);
                    var stGigEInfo = (MvCodeReader.MV_CODEREADER_GIGE_DEVICE_INFO)Marshal.PtrToStructure(
                        buffer, typeof(MvCodeReader.MV_CODEREADER_GIGE_DEVICE_INFO));

                    var deviceInfo = new ScannerDeviceInfo
                    {
                        DeviceId = stGigEInfo.chSerialNumber,
                        DeviceName = string.IsNullOrEmpty(stGigEInfo.chUserDefinedName) 
                            ? $"{stGigEInfo.chManufacturerName} {stGigEInfo.chModelName}"
                            : stGigEInfo.chUserDefinedName,
                        Manufacturer = stGigEInfo.chManufacturerName,
                        Model = stGigEInfo.chModelName,
                        IpAddress = $"{(stGigEInfo.nCurrentIp >> 24) & 0xFF}.{(stGigEInfo.nCurrentIp >> 16) & 0xFF}.{(stGigEInfo.nCurrentIp >> 8) & 0xFF}.{stGigEInfo.nCurrentIp & 0xFF}",
                        RawDeviceInfo = stDevInfo
                    };
                    devices.Add(deviceInfo);
                }
            }
            return devices;
        }

        public bool Connect(ScannerDeviceInfo device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            // 如果是通过IP创建的设备信息，使用IP连接
            if (device.RawDeviceInfo is HikvisionConnectionInfo connInfo)
            {
                return ConnectByIp(connInfo.IpAddress);
            }

            if (device.RawDeviceInfo == null)
                throw new ArgumentException("设备信息无效", nameof(device));

            lock (_syncLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(HikvisionScanner));
                if (_isConnected) return false;

                try
                {
                    var stDevInfo = (MvCodeReader.MV_CODEREADER_DEVICE_INFO)device.RawDeviceInfo;

                    _device = new MvCodeReader();
                    int nRet = _device.MV_CODEREADER_CreateHandle_NET(ref stDevInfo);
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        RaiseException(nRet, "创建句柄失败");
                        return false;
                    }

                    nRet = _device.MV_CODEREADER_OpenDevice_NET();
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        _device.MV_CODEREADER_DestroyHandle_NET();
                        RaiseException(nRet, "打开设备失败");
                        return false;
                    }

                    _device.MV_CODEREADER_SetEnumValue_NET("TriggerMode", 
                        (uint)MvCodeReader.MV_CODEREADER_TRIGGER_MODE.MV_CODEREADER_TRIGGER_MODE_OFF);

                    _isConnected = true;
                    _currentDevice = device;
                    RaiseConnectionStatusChanged(true, $"已连接到 {device.IpAddress}");
                    return true;
                }
                catch (Exception ex)
                {
                    RaiseException(-1, ex.Message);
                    RaiseConnectionStatusChanged(false, $"连接失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 创建设备信息（用于通过IP直接连接）
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        /// <param name="name">设备名称（可选）</param>
        public static ScannerDeviceInfo CreateDeviceInfo(string ip, string name = null)
        {
            return new ScannerDeviceInfo
            {
                DeviceId = ip,
                DeviceName = name ?? $"Hikvision Scanner ({ip})",
                Manufacturer = "Hikvision",
                Model = "GigE Scanner",
                IpAddress = ip,
                RawDeviceInfo = new HikvisionConnectionInfo { IpAddress = ip }
            };
        }

        /// <summary>
        /// 通过IP地址直接连接设备
        /// </summary>
        /// <param name="ip">设备IP地址</param>
        public bool ConnectByIp(string ip)
        {
            if (string.IsNullOrEmpty(ip))
                throw new ArgumentNullException(nameof(ip));

            // 枚举设备并查找匹配IP的设备
            var devices = EnumerateDevices();
            var targetDevice = devices.Find(d => d.IpAddress == ip);

            if (targetDevice == null)
            {
                RaiseException(-1, $"未找到IP为 {ip} 的设备");
                RaiseConnectionStatusChanged(false, $"未找到IP为 {ip} 的设备");
                return false;
            }

            // 使用找到的设备信息进行连接
            lock (_syncLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(HikvisionScanner));
                if (_isConnected) return false;

                try
                {
                    var stDevInfo = (MvCodeReader.MV_CODEREADER_DEVICE_INFO)targetDevice.RawDeviceInfo;

                    _device = new MvCodeReader();
                    int nRet = _device.MV_CODEREADER_CreateHandle_NET(ref stDevInfo);
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        RaiseException(nRet, $"通过IP创建句柄失败: {ip}");
                        RaiseConnectionStatusChanged(false, $"通过IP创建句柄失败: {ip}");
                        return false;
                    }

                    nRet = _device.MV_CODEREADER_OpenDevice_NET();
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        _device.MV_CODEREADER_DestroyHandle_NET();
                        RaiseException(nRet, "打开设备失败");
                        RaiseConnectionStatusChanged(false, "打开设备失败");
                        return false;
                    }

                    _device.MV_CODEREADER_SetEnumValue_NET("TriggerMode",
                        (uint)MvCodeReader.MV_CODEREADER_TRIGGER_MODE.MV_CODEREADER_TRIGGER_MODE_OFF);

                    _isConnected = true;
                    _currentDevice = targetDevice;
                    RaiseConnectionStatusChanged(true, $"已连接到 {ip}");
                    return true;
                }
                catch (Exception ex)
                {
                    RaiseException(-1, ex.Message);
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
                    // 先停止扫描，并等待线程结束
                    if (_isScanning)
                    {
                        StopScanInternal();
                    }

                    // 线程安全地关闭设备
                    if (_device != null)
                    {
                        _device.MV_CODEREADER_CloseDevice_NET();
                        _device.MV_CODEREADER_DestroyHandle_NET();
                        _device = null;
                    }

                    _isConnected = false;
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
                    throw new ObjectDisposedException(nameof(HikvisionScanner));
                if (!_isConnected || _isScanning) return false;

                try
                {
                    _isScanning = true;
                    _receiveThread = new Thread(ReceiveThreadProcess);
                    _receiveThread.IsBackground = true;
                    _receiveThread.Start();

                    int nRet = _device.MV_CODEREADER_StartGrabbing_NET();
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        _isScanning = false;
                        JoinThreadWithTimeout(_receiveThread);
                        RaiseException(nRet, "开始采集失败");
                        return false;
                    }
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
                _device?.MV_CODEREADER_StopGrabbing_NET();
                JoinThreadWithTimeout(_receiveThread);
                _receiveThread = null;
                return true;
            }
            catch (Exception ex)
            {
                RaiseException(-1, ex.Message);
                return false;
            }
        }

        private void JoinThreadWithTimeout(Thread thread)
        {
            if (thread != null && thread.IsAlive)
            {
                if (!thread.Join(ThreadJoinTimeout))
                {
                    RaiseException(-1, "接收线程退出超时");
                }
            }
        }

        public bool TriggerScan()
        {
            if (!_isConnected || !_isScanning) return false;

            int nRet = _device.MV_CODEREADER_SetCommandValue_NET("TriggerSoftware");
            if (nRet != MvCodeReader.MV_CODEREADER_OK)
            {
                RaiseException(nRet, "软触发失败");
                return false;
            }
            return true;
        }

        public bool SetTriggerMode(bool isTriggerMode)
        {
            if (!_isConnected) return false;

            uint mode = isTriggerMode 
                ? (uint)MvCodeReader.MV_CODEREADER_TRIGGER_MODE.MV_CODEREADER_TRIGGER_MODE_ON
                : (uint)MvCodeReader.MV_CODEREADER_TRIGGER_MODE.MV_CODEREADER_TRIGGER_MODE_OFF;

            int nRet = _device.MV_CODEREADER_SetEnumValue_NET("TriggerMode", mode);
            if (nRet != MvCodeReader.MV_CODEREADER_OK)
            {
                RaiseException(nRet, "设置触发模式失败");
                return false;
            }

            if (isTriggerMode)
            {
                _device.MV_CODEREADER_SetEnumValue_NET("TriggerSource",
                    (uint)MvCodeReader.MV_CODEREADER_TRIGGER_SOURCE.MV_CODEREADER_TRIGGER_SOURCE_SOFTWARE);
            }
            return true;
        }

        public float GetExposureTime()
        {
            if (!_isConnected) return 0;

            var stParam = new MvCodeReader.MV_CODEREADER_FLOATVALUE();
            int nRet = _device.MV_CODEREADER_GetFloatValue_NET("ExposureTime", ref stParam);
            return nRet == MvCodeReader.MV_CODEREADER_OK ? stParam.fCurValue : 0;
        }

        public bool SetExposureTime(float value)
        {
            if (!_isConnected) return false;

            _device.MV_CODEREADER_SetEnumValue_NET("ExposureAuto", 0);
            int nRet = _device.MV_CODEREADER_SetFloatValue_NET("ExposureTime", value);
            return nRet == MvCodeReader.MV_CODEREADER_OK;
        }

        public float GetGain()
        {
            if (!_isConnected) return 0;

            var stParam = new MvCodeReader.MV_CODEREADER_FLOATVALUE();
            int nRet = _device.MV_CODEREADER_GetFloatValue_NET("Gain", ref stParam);
            return nRet == MvCodeReader.MV_CODEREADER_OK ? stParam.fCurValue : 0;
        }

        public bool SetGain(float value)
        {
            if (!_isConnected) return false;

            _device.MV_CODEREADER_SetEnumValue_NET("GainAuto", 0);
            int nRet = _device.MV_CODEREADER_SetFloatValue_NET("Gain", value);
            return nRet == MvCodeReader.MV_CODEREADER_OK;
        }

        /// <summary>
        /// 同步触发扫码并等待结果
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>扫码结果，失败返回空字符串</returns>
        public string TriggerScanSync(int timeoutMs = 5000)
        {
            if (!_isConnected) return "";

            int originalTimeout = ReceiveTimeout;
            try
            {
                if (timeoutMs > 0)
                {
                    ReceiveTimeout = timeoutMs;
                }

                // 确保在触发模式下
                SetTriggerMode(true);

                // 开始采集
                if (!_isScanning)
                {
                    int nRet = _device.MV_CODEREADER_StartGrabbing_NET();
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        RaiseException(nRet, "开始采集失败");
                        return "";
                    }
                }

                // 触发扫码
                int triggerRet = _device.MV_CODEREADER_SetCommandValue_NET("TriggerSoftware");
                if (triggerRet != MvCodeReader.MV_CODEREADER_OK)
                {
                    RaiseException(triggerRet, "软触发失败");
                    return "";
                }

                // 等待结果
                IntPtr pData = IntPtr.Zero;
                var stFrameInfoEx2 = new MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2();
                IntPtr pstFrameInfoEx2 = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2)));

                try
                {
                    Marshal.StructureToPtr(stFrameInfoEx2, pstFrameInfoEx2, false);

                    int nRet = _device.MV_CODEREADER_GetOneFrameTimeoutEx2_NET(ref pData, pstFrameInfoEx2, (uint)ReceiveTimeout);
                    if (nRet != MvCodeReader.MV_CODEREADER_OK)
                    {
                        return "";
                    }

                    stFrameInfoEx2 = (MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2)Marshal.PtrToStructure(
                        pstFrameInfoEx2, typeof(MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2));

                    if (stFrameInfoEx2.nFrameLen <= 0)
                    {
                        return "";
                    }

                    var stBcrResult = (MvCodeReader.MV_CODEREADER_RESULT_BCR_EX2)Marshal.PtrToStructure(
                        stFrameInfoEx2.UnparsedBcrList.pstCodeListEx2, typeof(MvCodeReader.MV_CODEREADER_RESULT_BCR_EX2));

                    for (int i = 0; i < stBcrResult.nCodeNum; i++)
                    {
                        string code = System.Text.Encoding.UTF8.GetString(stBcrResult.stBcrInfoEx2[i].chCode);
                        code = code.TrimEnd('\0');

                        string normalized = ScanResultNormalizer.Normalize(code);
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            return normalized;
                        }
                    }

                    return "";
                }
                finally
                {
                    if (pstFrameInfoEx2 != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(pstFrameInfoEx2);
                    }
                }
            }
            finally
            {
                ReceiveTimeout = originalTimeout;
            }
        }

        /// <summary>
        /// 异步触发扫码
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>扫码结果任务</returns>
        public Task<string> TriggerScanAsync(int timeoutMs = 5000)
        {
            return Task.Run(() => TriggerScanSync(timeoutMs));
        }

        /// <summary>
        /// 应用配置
        /// </summary>
        /// <param name="config">扫码器配置</param>
        public void ApplyConfiguration(IScannerConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (config is HikvisionConfiguration hikvisionConfig)
            {
                ReceiveTimeout = hikvisionConfig.ReceiveTimeoutMs;
            }
            else
            {
                ReceiveTimeout = config.ReceiveTimeoutMs;
            }
        }

        private void ReceiveThreadProcess()
        {
            IntPtr pData = IntPtr.Zero;
            var stFrameInfoEx2 = new MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2();
            IntPtr pstFrameInfoEx2 = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2)));
            Marshal.StructureToPtr(stFrameInfoEx2, pstFrameInfoEx2, false);

            try
            {
                while (_isScanning)
                {
                    var device = _device;
                    if (device == null) break;

                    int nRet = device.MV_CODEREADER_GetOneFrameTimeoutEx2_NET(ref pData, pstFrameInfoEx2, (uint)ReceiveTimeout);
                    if (nRet != MvCodeReader.MV_CODEREADER_OK) continue;

                    stFrameInfoEx2 = (MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2)Marshal.PtrToStructure(
                        pstFrameInfoEx2, typeof(MvCodeReader.MV_CODEREADER_IMAGE_OUT_INFO_EX2));

                    if (stFrameInfoEx2.nFrameLen <= 0) continue;

                    var eventArgs = new ScanResultEventArgs
                    {
                        Results = new List<ScanResult>(),
                        ImageWidth = stFrameInfoEx2.nWidth,
                        ImageHeight = stFrameInfoEx2.nHeight
                    };

                    byte[] imageData = new byte[stFrameInfoEx2.nFrameLen];
                    Marshal.Copy(pData, imageData, 0, (int)stFrameInfoEx2.nFrameLen);
                    eventArgs.ImageData = imageData;

                    var stBcrResult = (MvCodeReader.MV_CODEREADER_RESULT_BCR_EX2)Marshal.PtrToStructure(
                        stFrameInfoEx2.UnparsedBcrList.pstCodeListEx2, typeof(MvCodeReader.MV_CODEREADER_RESULT_BCR_EX2));

                    for (int i = 0; i < stBcrResult.nCodeNum; i++)
                    {
                        string code = System.Text.Encoding.UTF8.GetString(stBcrResult.stBcrInfoEx2[i].chCode);
                        code = code.TrimEnd('\0');

                        eventArgs.Results.Add(new ScanResult
                        {
                            Code = string.IsNullOrEmpty(code) ? "NoRead" : code,
                            CodeType = GetBarType(stBcrResult.stBcrInfoEx2[i].nBarType),
                            ScanTime = DateTime.Now,
                            ProcessTime = (int)stBcrResult.stBcrInfoEx2[i].nTotalProcCost,
                            Quality = stBcrResult.stBcrInfoEx2[i].stCodeQuality.nOverQuality
                        });
                    }

                    RaiseScanResult(eventArgs);
                }
            }
            catch (Exception ex)
            {
                RaiseException(-1, $"接收线程异常: {ex.Message}");
            }
            finally
            {
                if (pstFrameInfoEx2 != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pstFrameInfoEx2);
                }
            }
        }

        private string GetBarType(uint nBarType)
        {
            switch ((HikBarType)nBarType)
            {
                case HikBarType.DM: return "DM码";
                case HikBarType.QR: return "QR码";
                case HikBarType.EAN8: return "EAN8码";
                case HikBarType.UPCE: return "UPCE码";
                case HikBarType.UPCA: return "UPCA码";
                case HikBarType.EAN13: return "EAN13码";
                case HikBarType.Code39: return "Code39码";
                case HikBarType.Code93: return "Code93码";
                case HikBarType.Code128: return "Code128码";
                case HikBarType.ITF25: return "ITF25码";
                case HikBarType.PDF417: return "PDF417码";
                default: return "未知";
            }
        }

        private void RaiseScanResult(ScanResultEventArgs args)
        {
            var handler = OnScanResult;
            handler?.Invoke(this, args);
        }

        private void RaiseException(int errorCode, string message)
        {
            var handler = OnException;
            handler?.Invoke(this, new ScannerExceptionEventArgs
            {
                ErrorCode = errorCode,
                ErrorMessage = message
            });
        }

        private void RaiseConnectionStatusChanged(bool isConnected, string message)
        {
            var handler = OnConnectionStatusChanged;
            handler?.Invoke(this, new ConnectionStatusChangedEventArgs
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

                    if (_device != null)
                    {
                        try
                        {
                            _device.MV_CODEREADER_CloseDevice_NET();
                            _device.MV_CODEREADER_DestroyHandle_NET();
                        }
                        catch
                        {
                            // 忽略释放时的异常
                        }
                        _device = null;
                    }

                    _isConnected = false;
                    _currentDevice = null;
                }
            }

            _disposed = true;
        }

        ~HikvisionScanner()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 海康威视连接信息（用于通过IP直接连接）
    /// </summary>
    public class HikvisionConnectionInfo
    {
        public string IpAddress { get; set; }
    }
#else
    /// <summary>
    /// 海康扫码器（缺少SDK时的占位实现）
    /// </summary>
    public class HikvisionScanner : IScanner
    {
        private const string MissingSdkMessage = "未检测到海康扫码器SDK(MvCodeReaderSDK.Net.dll)，请安装SDK或配置 MvCodeReaderSdkDll";

        public event EventHandler<ScanResultEventArgs> OnScanResult;
        public event EventHandler<ScannerExceptionEventArgs> OnException;
        public event EventHandler<ConnectionStatusChangedEventArgs> OnConnectionStatusChanged;

        public int ReceiveTimeout { get; set; } = 1000;

        public bool IsConnected => false;
        public bool IsScanning => false;
        public ScannerDeviceInfo CurrentDevice => null;

        public List<ScannerDeviceInfo> EnumerateDevices()
        {
            return new List<ScannerDeviceInfo>();
        }

        public static ScannerDeviceInfo CreateDeviceInfo(string ip, string name = null)
        {
            return new ScannerDeviceInfo
            {
                DeviceId = ip,
                DeviceName = name ?? $"Hikvision Scanner ({ip})",
                Manufacturer = "Hikvision",
                Model = "GigE Scanner",
                IpAddress = ip,
                RawDeviceInfo = new HikvisionConnectionInfo { IpAddress = ip }
            };
        }

        public bool ConnectByIp(string ip)
        {
            return Connect(CreateDeviceInfo(ip));
        }

        public bool Connect(ScannerDeviceInfo device)
        {
            RaiseException(-1, MissingSdkMessage);
            RaiseConnectionStatusChanged(false, MissingSdkMessage);
            return false;
        }

        public bool Disconnect()
        {
            RaiseConnectionStatusChanged(false, "已断开连接");
            return true;
        }

        public bool StartScan() => false;
        public bool StopScan() => false;
        public bool TriggerScan() => false;
        public bool SetTriggerMode(bool isTriggerMode) => false;
        public float GetExposureTime() => 0;
        public bool SetExposureTime(float value) => false;
        public float GetGain() => 0;
        public bool SetGain(float value) => false;

        public string TriggerScanSync(int timeoutMs = 5000)
        {
            RaiseException(-1, MissingSdkMessage);
            return "";
        }

        public Task<string> TriggerScanAsync(int timeoutMs = 5000)
        {
            return Task.FromResult(TriggerScanSync(timeoutMs));
        }

        public void ApplyConfiguration(IScannerConfiguration config)
        {
            if (config != null)
            {
                ReceiveTimeout = config.ReceiveTimeoutMs;
            }
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
                Device = null
            });
        }

        public void Dispose()
        {
        }
    }

    public class HikvisionConnectionInfo
    {
        public string IpAddress { get; set; }
    }
#endif
}
