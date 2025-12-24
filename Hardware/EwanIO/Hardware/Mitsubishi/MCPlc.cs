using EwanIO.Core.Interfaces;
using PlcCommunication.Interfaces;
using System;
using System.Collections;

namespace EwanIO.Hardware.Mitsubishi
{
    /// <summary>
    /// 三菱PLC MC协议 IO实现
    /// 实现IHardwareIO接口，提供X区（输入）和Y区（输出）的访问
    /// 边缘检测由上层 EdgeDetector 统一处理
    /// </summary>
    public class MCPlc : IHardwareIOExtended
    {
        private IPlcBase? plc;
        private string xAreaBaseAddress = "X0";  // X区基地址
        private string yAreaBaseAddress = "Y0";  // Y区基地址
        private int ioCount = 64;  // 默认64个IO点

        // 缓存输入输出状态
        private BitArray inputCache;
        private BitArray outputCache;

        // 连接信息
        private string ipAddress = "";
        private int port = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="plc">PLC通信接口</param>
        /// <param name="ioCount">IO点数量，默认64</param>
        public MCPlc(IPlcBase plc, int ioCount = 64)
        {
            this.plc = plc ?? throw new ArgumentNullException(nameof(plc));
            this.ioCount = ioCount;

            InitializeCaches();
        }

        #region IHardwareIO 属性实现

        public string HardwareType => "MC_PLC";

        public string ConnectionInfo => $"{ipAddress}:{port}";

        public bool IsConnected => plc?.IsConnected ?? false;

        public int InputCount => ioCount;

        public int OutputCount => ioCount;

        public HardwareCapabilities Capabilities => HardwareCapabilities.FullySeparatedSync;

        #endregion

        #region 初始化和配置

        /// <summary>
        /// 初始化缓存
        /// </summary>
        private void InitializeCaches()
        {
            inputCache = new BitArray(ioCount);
            outputCache = new BitArray(ioCount);
        }

        #endregion

        #region 连接管理

        /// <summary>
        /// 连接到PLC
        /// </summary>
        /// <param name="connectionString">连接字符串，格式："IP:Port" 或 "IP:Port:PlcType"</param>
        public bool Connect(string connectionString)
        {
            try
            {
                // 解析连接字符串
                var parts = connectionString.Split(':');
                if (parts.Length < 2)
                {
                    return false;
                }

                ipAddress = parts[0];
                port = int.Parse(parts[1]);

                if (plc == null)
                {
                    return false;
                }

                // 初始化PLC
                var initResult = plc.Initialize(connectionString);
                if (!initResult.Success)
                {
                    return false;
                }

                // 连接PLC
                var connectResult = plc.Connect();
                if (!connectResult.Success)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 断开PLC连接
        /// </summary>
        public bool Disconnect()
        {
            try
            {
                if (plc != null && plc.IsConnected)
                {
                    var result = plc.Disconnect();
                    return result.Success;
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion

        #region 数据同步

        /// <summary>
        /// 同步IO数据
        /// </summary>
        public void DataSync()
        {
            if (!IsConnected) return;

            // 同步输入数据
            IO_InputDataSync();

            // 同步输出数据
            IO_OutputDataSync();
        }

        public void InputSync()
        {
            IO_InputDataSync();
        }

        public void OutputSync()
        {
            IO_OutputDataSync();
        }

        public void WriteBulkOutputPort(int portIndex, uint portValue)
        {
            throw new NotSupportedException("MCPlc does not support bulk output port writes.");
        }

        /// <summary>
        /// 同步输入数据（X区）
        /// </summary>
        private void IO_InputDataSync()
        {
            if (!IsConnected || plc == null) return;

            try
            {
                // 批量读取X区数据
                var result = plc.Read<bool>(xAreaBaseAddress, (uint)ioCount);

                if (result.Success)
                {
                    // 更新输入缓存
                    for (int i = 0; i < ioCount && i < result.Data.Length; i++)
                    {
                        inputCache[i] = result.Data[i];
                    }
                }
            }
            catch (Exception ex)
            {
                // InputSync失败，静默处理
            }
        }

        /// <summary>
        /// 同步输出数据（Y区）
        /// </summary>
        private void IO_OutputDataSync()
        {
            if (!IsConnected || plc == null) return;

            try
            {
                // 将BitArray转换为bool数组
                bool[] outputBools = new bool[ioCount];
                for (int i = 0; i < ioCount; i++)
                {
                    outputBools[i] = outputCache[i];
                }

                // 批量写入Y区数据
                plc.Write(yAreaBaseAddress, outputBools);
            }
            catch (Exception ex)
            {
                // OutputSync失败，静默处理
            }
        }

        #endregion

        #region IO操作

        /// <summary>
        /// 读取输入位
        /// </summary>
        public bool ReadInBit(int bit)
        {
            if (bit < 0 || bit >= ioCount) return false;
            return inputCache[bit];
        }

        /// <summary>
        /// 读取输出位
        /// </summary>
        public bool ReadOutBit(int bit)
        {
            if (bit < 0 || bit >= ioCount) return false;
            return outputCache[bit];
        }

        /// <summary>
        /// 写入输出位
        /// </summary>
        public bool WriteOutBit(int bit, bool value)
        {
            if (bit < 0 || bit >= ioCount) return false;

            try
            {
                outputCache[bit] = value;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion

        #region 批量操作

        /// <summary>
        /// 获取所有输入状态
        /// </summary>
        public BitArray GetInAll()
        {
            return new BitArray(inputCache);
        }

        /// <summary>
        /// 获取所有输出状态
        /// </summary>
        public BitArray GetOutAll()
        {
            return new BitArray(outputCache);
        }

        #endregion

        #region IDisposable 实现

        private bool _disposed = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 断开PLC连接
                Disconnect();

                // 释放PLC资源（如果它实现了 IDisposable）
                if (plc is IDisposable disposablePlc)
                {
                    disposablePlc.Dispose();
                }

                plc = null;
            }

            _disposed = true;
        }

        #endregion
    }
}
