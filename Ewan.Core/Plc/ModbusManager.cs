using HslCommunication;
using HslCommunication.ModBus;
using System;

namespace Ewan.Core.Plc
{
    public class ModbusManager :BaseManager<ModbusManager>
    {
        private byte mStationNo;
        private string mPlcIp;
        private int mPlcPort;
        private ModbusTcpNet busTcpClient = null;
        private bool isConnected = false;


        public override bool Init()
        {
            try
            {
                mStationNo = 1;
                mPlcIp = "192.168.1.10";
                mPlcPort = 6000;

                _appLogger.Info($"ModbusManager 开始初始化 - 站号:{mStationNo}, IP:{mPlcIp}, 端口:{mPlcPort}");

                busTcpClient = new ModbusTcpNet(mPlcIp, mPlcPort, mStationNo);

                if (Open())
                {
                    _appLogger.Info($"ModbusManager 初始化成功 - ID:{this.GetHashCode()}, 连接已建立");
                    return base.Init();
                }
                else
                {
                    _appLogger.Error($"ModbusManager 初始化失败 - ID:{this.GetHashCode()}, 无法建立连接");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusManager 初始化异常 - {ex.Message}", ex);
                return false;
            }
        }

        private bool Open()
        {
            var isSuccess = true;
            
            _appLogger.Info($"ModbusManager 尝试连接服务器 - {mPlcIp}:{mPlcPort}");
            
            // 先关闭之前的连接（如果存在）
            busTcpClient.ConnectClose();
            
            try
            {
                OperateResult connect = busTcpClient.ConnectServer();
                if (connect.IsSuccess)
                {
                    isConnected = true;
                    _appLogger.Info($"ModbusManager 连接成功 - 服务器:{mPlcIp}:{mPlcPort}, 站号:{mStationNo}");
                }
                else
                {
                    isSuccess = false;
                    isConnected = false;
                    _appLogger.Error($"ModbusManager 连接失败 - 错误信息:{connect.Message}");
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                isConnected = false;
                _appLogger.Error($"ModbusManager 连接异常 - {ex.Message}", ex);
            }
            return isSuccess;
        }



        /// <summary>
        /// 关闭Modbus连接
        /// </summary>
        /// <returns>是否成功关闭</returns>
        public bool Close()
        {
            try
            {
                _appLogger.Info($"ModbusManager 开始关闭连接 - {mPlcIp}:{mPlcPort}");
                
                var result = busTcpClient.ConnectClose();
                
                if (result.IsSuccess)
                {
                    isConnected = false;
                    _appLogger.Info("ModbusManager 连接关闭成功");
                }
                else
                {
                    _appLogger.Error($"ModbusManager 连接关闭失败 - 错误信息:{result.Message}");
                }
                
                return result.IsSuccess;
            }
            catch (Exception ex)
            {
                _appLogger.Error($"ModbusManager 关闭连接异常 - {ex.Message}", ex);
                return false;
            }
        }



    }
}
