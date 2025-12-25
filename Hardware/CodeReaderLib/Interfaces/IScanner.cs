using System;
using System.Collections.Generic;

namespace CodeReaderLib.Interfaces
{
    /// <summary>
    /// 扫描结果
    /// </summary>
    public class ScanResult
    {
        /// <summary>
        /// 条码内容
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 条码类型
        /// </summary>
        public string CodeType { get; set; }

        /// <summary>
        /// 扫描时间
        /// </summary>
        public DateTime ScanTime { get; set; }

        /// <summary>
        /// 扫描耗时（毫秒）
        /// </summary>
        public int ProcessTime { get; set; }

        /// <summary>
        /// 质量评分
        /// </summary>
        public int Quality { get; set; }
    }

    /// <summary>
    /// 设备信息
    /// </summary>
    public class ScannerDeviceInfo
    {
        /// <summary>
        /// 设备ID/序列号
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 厂商名称
        /// </summary>
        public string Manufacturer { get; set; }

        /// <summary>
        /// 设备型号
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// IP地址（网络设备）
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// 原始设备信息对象
        /// </summary>
        public object RawDeviceInfo { get; set; }
    }

    /// <summary>
    /// 扫描结果事件参数
    /// </summary>
    public class ScanResultEventArgs : EventArgs
    {
        public List<ScanResult> Results { get; set; }
        public byte[] ImageData { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
    }

    /// <summary>
    /// 扫描器异常事件参数
    /// </summary>
    public class ScannerExceptionEventArgs : EventArgs
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 设备信息
        /// </summary>
        public ScannerDeviceInfo Device { get; set; }
    }

    /// <summary>
    /// 通用扫描器接口
    /// </summary>
    public interface IScanner : IDisposable
    {
        /// <summary>
        /// 扫描结果事件
        /// </summary>
        event EventHandler<ScanResultEventArgs> OnScanResult;

        /// <summary>
        /// 异常事件
        /// </summary>
        event EventHandler<ScannerExceptionEventArgs> OnException;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        event EventHandler<ConnectionStatusChangedEventArgs> OnConnectionStatusChanged;

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 是否正在扫描
        /// </summary>
        bool IsScanning { get; }

        /// <summary>
        /// 当前设备信息
        /// </summary>
        ScannerDeviceInfo CurrentDevice { get; }

        /// <summary>
        /// 枚举所有可用设备
        /// </summary>
        List<ScannerDeviceInfo> EnumerateDevices();

        /// <summary>
        /// 连接设备
        /// </summary>
        bool Connect(ScannerDeviceInfo device);

        /// <summary>
        /// 断开连接
        /// </summary>
        bool Disconnect();

        /// <summary>
        /// 开始扫描
        /// </summary>
        bool StartScan();

        /// <summary>
        /// 停止扫描
        /// </summary>
        bool StopScan();

        /// <summary>
        /// 软件触发扫描
        /// </summary>
        bool TriggerScan();

        /// <summary>
        /// 设置触发模式
        /// </summary>
        /// <param name="isTriggerMode">true为触发模式，false为连续模式</param>
        bool SetTriggerMode(bool isTriggerMode);

        /// <summary>
        /// 获取曝光时间
        /// </summary>
        float GetExposureTime();

        /// <summary>
        /// 设置曝光时间
        /// </summary>
        bool SetExposureTime(float value);

        /// <summary>
        /// 获取增益
        /// </summary>
        float GetGain();

        /// <summary>
        /// 设置增益
        /// </summary>
        bool SetGain(float value);
    }
}
