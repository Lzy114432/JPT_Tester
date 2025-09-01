using IOLibrary.Core.Interfaces;
using PlcCommunication.Interfaces;
using System;
using System.Collections;
using Ewan.LogManager.Logger;

namespace IOLibrary.Hardware.Mitsubishi
{
    /// <summary>
    /// 三菱PLC MC协议 IO实现
    /// 实现IHardwareIO接口，提供X区（输入）和Y区（输出）的访问
    /// </summary>
    public class MCPlc : IHardwareIO
    {
        private IPlcBase? plc;
        private string xAreaBaseAddress = "X0";  // X区基地址
        private string yAreaBaseAddress = "Y0";  // Y区基地址
        private int ioCount = 64;  // 默认64个IO点
        
        // 缓存输入输出状态
        private BitArray? inputCache;
        private BitArray? outputCache;
        private BitArray? risingEdgeCache;
        private BitArray? fallingEdgeCache;
        private BitArray? lastInputState;
        
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
        
        public string ConnectionInfo => plc != null ? $"{ipAddress}:{port}" : "Not Connected";
        
        public bool IsConnected => plc?.IsConnected ?? false;
        
        public int InputCount => ioCount;
        
        public int OutputCount => ioCount;
        
        #endregion
        
        #region 初始化和配置
        
        /// <summary>
        /// 初始化缓存
        /// </summary>
        private void InitializeCaches()
        {
            inputCache = new BitArray(ioCount);
            outputCache = new BitArray(ioCount);
            risingEdgeCache = new BitArray(ioCount);
            fallingEdgeCache = new BitArray(ioCount);
            lastInputState = new BitArray(ioCount);
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
                    IOLogger.Instance.LogPLCCommunication(connectionString, "Connect", "Invalid connection string", false);
                    return false;
                }
                
                ipAddress = parts[0];
                port = int.Parse(parts[1]);
                
                // 如果PLC实例还未创建，创建它
                if (plc == null)
                {
                    // 这里假设使用MCProtocol实现
                    // 实际使用时需要根据具体的PLC类型创建相应的实例
                    // plc = new MCProtocolPlc(); // 需要具体的实现类
                    IOLogger.Instance.LogPLCCommunication("", "Connect", "PLC instance not provided", false);
                    return false;
                }
                
                // 初始化PLC
                var initResult = plc.Initialize(connectionString);
                if (!initResult.Success)
                {
                    IOLogger.Instance.LogPLCCommunication(connectionString, "Initialize", initResult.ErrorMessage, false);
                    return false;
                }
                
                // 连接PLC
                var connectResult = plc.Connect();
                if (!connectResult.Success)
                {
                    IOLogger.Instance.LogPLCCommunication($"{ipAddress}:{port}", "Connect", connectResult.ErrorMessage, false);
                    return false;
                }
                
                IOLogger.Instance.LogPLCCommunication($"{ipAddress}:{port}", "Connect", "Connected successfully", true);
                return true;
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogPLCCommunication($"{ipAddress}:{port}", "Connect", ex.Message, false);
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
                    if (result.Success)
                    {
                        IOLogger.Instance.LogPLCCommunication($"{ipAddress}:{port}", "Disconnect", "Disconnected successfully", true);
                        return true;
                    }
                    else
                    {
                        IOLogger.Instance.LogPLCCommunication($"{ipAddress}:{port}", "Disconnect", result.ErrorMessage, false);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogPLCCommunication($"{ipAddress}:{port}", "Disconnect", ex.Message, false);
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
        
        /// <summary>
        /// 同步输入数据（X区）
        /// </summary>
        private void IO_InputDataSync()
        {
            if (!IsConnected) return;
            
            try
            {
                // 批量读取X区数据
                var result = plc.Read<bool>(xAreaBaseAddress, (uint)ioCount);
                
                if (result.Success)
                {
                    // 更新输入缓存并检测边沿
                    for (int i = 0; i < ioCount && i < result.Data.Length; i++)
                    {
                        bool currentState = result.Data[i];
                        bool lastState = lastInputState[i];
                        
                        // 检测上升沿 (false -> true)
                        if (!lastState && currentState)
                        {
                            risingEdgeCache[i] = true;
                        }
                        
                        // 检测下降沿 (true -> false)
                        if (lastState && !currentState)
                        {
                            fallingEdgeCache[i] = true;
                        }
                        
                        inputCache[i] = currentState;
                        lastInputState[i] = currentState;
                    }
                }
                else
                {
                    IOLogger.Instance.LogPLCCommunication(xAreaBaseAddress, "ReadInput", result.ErrorMessage, false);
                }
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogPLCCommunication(xAreaBaseAddress, "InputSync", ex.Message, false);
            }
        }
        
        /// <summary>
        /// 同步输出数据（Y区）
        /// </summary>
        private void IO_OutputDataSync()
        {
            if (!IsConnected) return;
            
            try
            {
                // 将BitArray转换为bool数组
                bool[] outputBools = new bool[ioCount];
                for (int i = 0; i < ioCount; i++)
                {
                    outputBools[i] = outputCache[i];
                }
                
                // 批量写入Y区数据
                var result = plc.Write(yAreaBaseAddress, outputBools);
                
                if (!result.Success)
                {
                    IOLogger.Instance.LogPLCCommunication(yAreaBaseAddress, "WriteOutput", result.ErrorMessage, false);
                }
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogPLCCommunication(yAreaBaseAddress, "OutputSync", ex.Message, false);
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
        public bool WriteOutBit(int bit, bool sts)
        {
            if (bit < 0 || bit >= ioCount) return false;
            
            try
            {
                outputCache[bit] = sts;           
                return true;
            }
            catch (Exception ex)
            {
                IOLogger.Instance.LogPLCCommunication($"Y{bit}", "WriteOutBit", ex.Message, false);
                return false;
            }
        }
        
        #endregion
        
        #region 边沿检测
        
        /// <summary>
        /// 读取上升沿
        /// </summary>
        public bool ReadRisingBit(int bit)
        {
            if (bit < 0 || bit >= ioCount) return false;
            return risingEdgeCache[bit];
        }
        
        /// <summary>
        /// 清除上升沿
        /// </summary>
        public void ClearRisingBit(int bit)
        {
            if (bit < 0 || bit >= ioCount) return;
            risingEdgeCache[bit] = false;
        }
        
        /// <summary>
        /// 读取下降沿
        /// </summary>
        public bool ReadFallingBit(int bit)
        {
            if (bit < 0 || bit >= ioCount) return false;
            return fallingEdgeCache[bit];
        }
        
        /// <summary>
        /// 清除下降沿
        /// </summary>
        public void ClearFallingBit(int bit)
        {
            if (bit < 0 || bit >= ioCount) return;
            fallingEdgeCache[bit] = false;
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
    }
}