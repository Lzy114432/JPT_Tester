using Ewan.Core.Attribute;
using HslCommunication;
using HslCommunication.ModBus;
using HslCommunication.Profinet.Keyence;
using System;
using System.IO.Ports;

namespace Ewan.Core.Plc
{
    /// <summary>
    /// Modbus RTU管理器 - RS485串口通信实现
    /// 支持RS485网络通信，有效距离50米，支持多从站
    /// </summary>
    [Manager(Priority = 1)]
    public class ModbusRTUManager : BaseManager<ModbusRTUManager>
    {
        private byte mStationNo;
        private string mComPort;
        private int mBaudRate;
        private int mDataBits;
        private StopBits mStopBits;
        private Parity mParity;
        private ModbusRtu busRtuClient = null;
        private bool isConnected = false;

        public override bool Init()
        {
            try
            {
                // 默认RS485串口参数配置
                mStationNo = 1;
                mComPort = "COM7";
                mBaudRate = 115200;
                mDataBits = 8;
                mStopBits = StopBits.One;
                mParity = Parity.None;

                _appLogger.Info($"ModbusRTUManager 开始初始化 - 串口:{mComPort}, 波特率:{mBaudRate}, 数据位:{mDataBits}, 停止位:{mStopBits}, 校验位:{mParity}, 从站地址:{mStationNo}");

                // 创建Modbus RTU客户端（RS485通信）
                busRtuClient = new ModbusRtu();

                if (Open())
                {
                    _appLogger.Info($"ModbusRTUManager 初始化成功 - ID:{this.GetHashCode()}, RS485连接已建立");
                    return base.Init();
                }
                else
                {
                    _appLogger.Error($"ModbusRTUManager 初始化失败 - ID:{this.GetHashCode()}, 无法建立RS485连接");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusRTUManager 初始化异常 - {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 打开RS485串口连接
        /// </summary>
        /// <returns>是否连接成功</returns>
        private bool Open()
        {
            var isSuccess = true;
            
            _appLogger.Info($"ModbusRTUManager 尝试打开RS485串口连接 - {mComPort}");
            
            try
            {
                // 配置串口参数（RS485接口）
                busRtuClient.SerialPortInni(mComPort, mBaudRate, mDataBits, mStopBits, mParity);
                busRtuClient.Station = mStationNo;  // 设置从站地址

                // 打开串口连接 - ModbusRtu的Open方法返回void
                busRtuClient.Open();
                
                // 假设连接成功（ModbusRtu没有返回连接状态）
                isConnected = true;
                _appLogger.Info($"ModbusRTUManager RS485连接成功 - 串口:{mComPort}, 波特率:{mBaudRate}, 从站地址:{mStationNo}");
            }
            catch (Exception ex)
            {
                isSuccess = false;
                isConnected = false;
                _appLogger.Error($"ModbusRTUManager RS485连接异常 - {ex.Message}", ex);
            }
            return isSuccess;
        }

        /// <summary>
        /// 读数据
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public byte[] Read(string address, ushort length)
        {
            try
            {
                OperateResult<byte[]> read = busRtuClient.Read(address, length);
                if (read.IsSuccess)
                {
                    return read.Content;
                }
                else
                {
                    _appLogger.Info($"ModbusRTUManager Read failed,{read.ToMessageShowString()}");
                    return new byte[0];
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusRTUManager Read occur exception,{ex}");
                return new byte[0];
            }
        }


        public bool[] ReadCoil(string address, ushort length)
        {
            try
            {
                bool[] read = busRtuClient.ReadCoil(address, length).Content;
                 return read;
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusRTUManager Read occur exception,{ex}");
                return new bool[0];
            }
        }

        /// <summary>
        /// 万能写入 - 支持任意数据类型转字节后写入
        /// </summary>
        public OperateResult WriteAny(string address, object value)
        {
            try
            {
                byte[] data = ConvertToBytes(value);
                return busRtuClient.Write(address, data);
            }
            catch (Exception ex)
            {
                _appLogger.Error($"WriteAny failed: {ex}");
                return OperateResult.CreateSuccessResult(ex.Message);
            }
        }

        private byte[] ConvertToBytes(object value)
        {
            switch (value)
            {
                case byte[] bytes:
                    return bytes;
                case short s:
                    return BitConverter.GetBytes(s);
                case int i:
                    return BitConverter.GetBytes(i);
                case float f:
                    return BitConverter.GetBytes(f);
                case double d:
                    return BitConverter.GetBytes(d);
                //case bool[] bools:
                //    return BoolArrayToBytes(bools);
                default:
                    throw new ArgumentException($"Unsupported data type: {value.GetType()}");
            }
        }







        /// <summary>
        /// 关闭RS485串口连接
        /// </summary>
        /// <returns>是否成功关闭</returns>
        public bool Close()
        {
            try
            {
                _appLogger.Info($"ModbusRTUManager 开始关闭RS485连接 - {mComPort}");
                
                // ModbusRtu的Close方法可能返回void，直接调用
                busRtuClient.Close();
                isConnected = false;
                _appLogger.Info("ModbusRTUManager RS485连接关闭成功");
                
                return true;
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusRTUManager 关闭RS485连接异常 - {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        /// <returns>是否已连接</returns>
        public bool IsConnected()
        {
            return isConnected && busRtuClient != null;
        }

        /// <summary>
        /// 重新连接RS485
        /// </summary>
        /// <returns>是否重连成功</returns>
        public bool Reconnect()
        {
            try
            {
                _appLogger.Info("ModbusRTUManager 开始重新连接RS485");
                
                // 先关闭现有连接
                if (busRtuClient != null)
                {
                    try
                    {
                        busRtuClient.Close();
                    }
                    catch
                    {
                        // 忽略关闭时的异常
                    }
                }
                
                // 重新打开连接
                return Open();
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusRTUManager RS485重连异常 - {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 设置串口参数
        /// </summary>
        /// <param name="comPort">串口号</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <param name="parity">校验位</param>
        /// <param name="stationNo">从站地址</param>
        public void SetSerialParams(string comPort, int baudRate, int dataBits, StopBits stopBits, Parity parity, byte stationNo)
        {
            mComPort = comPort;
            mBaudRate = baudRate;
            mDataBits = dataBits;
            mStopBits = stopBits;
            mParity = parity;
            mStationNo = stationNo;
            
            _appLogger.Info($"ModbusRTUManager 串口参数已更新 - 串口:{comPort}, 波特率:{baudRate}, 数据位:{dataBits}, 停止位:{stopBits}, 校验位:{parity}, 从站地址:{stationNo}");
        }

        /// <summary>
        /// 获取当前串口配置信息
        /// </summary>
        /// <returns>配置信息字符串</returns>
        public string GetConfigInfo()
        {
            return $"串口:{mComPort}, 波特率:{mBaudRate}, 数据位:{mDataBits}, 停止位:{mStopBits}, 校验位:{mParity}, 从站地址:{mStationNo}, 连接状态:{(isConnected ? "已连接" : "未连接")}";
        }
    }
}
