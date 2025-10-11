using System;
using System.Threading;
using Ewan.Core.Plc;

namespace Ewan.Core.Module
{
    /// <summary>
    /// Station心跳模块 - 每1秒向寄存器170写入1
    /// STATION-ALIVE: 设备定时写入1,自动清零
    /// </summary>
    public class StationHeartbeatModule : BaseModule<StationHeartbeatModule>
    {
        private ModbusRTUManager _modbusManager;
        private const int HEARTBEAT_INTERVAL = 1000; // 1秒
        private const string HEARTBEAT_ADDRESS = "170"; // STATION-ALIVE寄存器地址
        private const ushort HEARTBEAT_VALUE = 1; // 心跳值

        protected override void OnInit()
        {
            _modbusManager = ModbusRTUManager.Instance();

            if (_modbusManager != null)
            {
                _appLogger.Info("StationHeartbeat模块初始化成功");
            }
            else
            {
                _appLogger.Warn("StationHeartbeat模块初始化失败 - ModbusRTU Manager不可用");
            }
        }

        protected override bool OnRun()
        {
            try
            {
                // 检查Modbus管理器是否连接
                if (_modbusManager != null && _modbusManager.IsConnected())
                {
                    // 向寄存器170写入1
                    var result = _modbusManager.WriteAny(HEARTBEAT_ADDRESS, HEARTBEAT_VALUE);

                    if (result.IsSuccess)
                    {
                        _appLogger.Debug($"心跳写入成功: 寄存器{HEARTBEAT_ADDRESS} = {HEARTBEAT_VALUE}");
                    }
                    else
                    {
                        _appLogger.Warn($"心跳写入失败: {result.Message}");
                    }
                }
                else
                {
                    _appLogger.Debug("心跳模块: Modbus未连接,跳过心跳写入");
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"StationHeartbeat模块运行错误: {ex.Message}", ex);
            }

            // 间隔1秒
            Thread.Sleep(HEARTBEAT_INTERVAL);

            return true;
        }

        protected override void OnDestroy()
        {
            _appLogger.Info("StationHeartbeat模块已销毁");
        }
    }
}
