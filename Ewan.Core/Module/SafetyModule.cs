using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.LogManager.Logger;
using Ewan.Model.Alarm;
using Ewan.Model.System;
using Ewan.Model.Safety;
using IOLibrary.Core.Layered;
using System;
using System.Diagnostics;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 安全模块 - 负责同步IO数据和监控报警
    /// </summary>
    public class SafetyModule : BaseModule<SafetyModule>
    {
        #region 私有字段

        private LayeredIOManager _ioManager;
        private LayeredIO _layeredIO;
        private MsgManager _msgManager;
        
        // 时间间隔设置
        private int _dataSyncInterval = 10;     // IO数据同步间隔(ms) - 保持快速响应
        
        // 性能监控
        private Stopwatch _performanceWatch = new Stopwatch();
        private long _syncCount = 0;
        private double _avgSyncTime = 0;
        
        // 报警监控状态记录 - 使用LayeredIO内置边缘检测，不再需要手动状态跟踪
        // private bool _lastEmergencyButtonState = false;
        // private bool _lastRobotAlarmState = false;
        // private bool _lastLowerCameraAlarmState = false;
        // private bool _lastCylinderAlarmState = false;
        // private bool _lastBin1LimitAlarmState = false;
        // private bool _lastBin2LimitAlarmState = false;
        // private bool _lastBin3LimitAlarmState = false;
        private int _alarmCheckInterval = 5; // 报警检查计数器（5次同步检查一次报警）
        private int _alarmCheckCounter = 0;

        #endregion

        #region BaseModule 实现

        /// <summary>
        /// 初始化模块
        /// </summary>
        protected override void OnInit()
        {
            try
            {
                // 获取管理器实例
                _ioManager = LayeredIOManager.Instance();
                _layeredIO = _ioManager.LayeredIO;
                _msgManager = MsgManager.Instance();

                // 如果未连接，则尝试连接
                if (!_ioManager.IsConnected)
                {
                    bool connected = _ioManager.Connect();
                    if (!connected)
                    {
                        _uiLogger.Warn(() => Ewan.Resources.LogMessages.IOConnectionFailed, "SafetyModule初始化");
                    }
                }

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "SafetyModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "SafetyModule", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 运行模块 - 执行DataSync
        /// </summary>
        protected override bool OnRun()
        {
            try
            {
                if (_layeredIO == null || !_ioManager.IsConnected)
                {
                    System.Threading.Thread.Sleep(_dataSyncInterval);
                    return true; // 跳过但继续运行
                }

                // 记录性能
                _performanceWatch.Restart();

                // 执行IO数据同步
                _layeredIO.DataSync();
                
                // 检查报警状态（减少频率避免过频检查）
                _alarmCheckCounter++;
                if (_alarmCheckCounter >= _alarmCheckInterval)
                {
                    CheckAlarmInputs();
                    _alarmCheckCounter = 0;
                }
                
                _performanceWatch.Stop();
                
                // 更新性能统计
                _syncCount++;
                _avgSyncTime = (_avgSyncTime * (_syncCount - 1) + _performanceWatch.ElapsedMilliseconds) / _syncCount;
                
                // 每1000次输出一次性能报告
                if (_syncCount % 100 == 0)
                {
                    IOLogger.Instance.LogRaw(LogLevel.Debug, 
                        $"IO同步性能: 平均{_avgSyncTime:F2}ms, 当前{_performanceWatch.ElapsedMilliseconds}ms");
                }

                // 延时控制循环速度
                System.Threading.Thread.Sleep(_dataSyncInterval);

                return true; // 继续运行
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "SafetyModule", ex.Message);
                return false; // 停止运行
            }
        }

        /// <summary>
        /// 销毁模块
        /// </summary>
        protected override void OnDestroy()
        {
            // 断开连接
            if (_ioManager != null && _ioManager.IsConnected)
            {
                _ioManager.Disconnect();
            }
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "SafetyModule");
        }

        #endregion

        #region 报警监控

        /// <summary>
        /// 检查报警输入信号
        /// </summary>
        private void CheckAlarmInputs()
        {
            try
            {
                // 检查暂停级报警 - X12, X13, X14
                CheckPauseAlarms();
                
                // 检查紧急停机报警 - X0, X15, X17, X19
                CheckEmergencyAlarms();
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "SafetyModule-CheckAlarmInputs", ex.Message);
            }
        }

        /// <summary>
        /// 检查暂停级报警 - X12, X13, X14
        /// </summary>
        private void CheckPauseAlarms()
        {
            // X12 - 料仓1下限位置信号（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.BIN1_LIMIT_ALARM))
            {
                SendPauseCommand("料仓1下限位置异常");
            }

            // X13 - 料仓2下限位置信号（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.BIN2_LIMIT_ALARM))
            {
                SendPauseCommand("料仓2下限位置异常");
            }

            // X14 - 料仓3下限位置信号（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.BIN3_LIMIT_ALARM))
            {
                SendPauseCommand("料仓3下限位置异常");
            }
        }

        /// <summary>
        /// 检查紧急停机报警 - X0, X15, X17, X19
        /// </summary>
        private void CheckEmergencyAlarms()
        {
            // X0 - 急停按钮（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.EMERGENCY_BUTTON))
            {
                SendEmergencyStopCommand("急停按钮被按下");
            }

            // X15 - 机械手报警信号（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.ROBOT_ALARM))
            {
                SendEmergencyStopCommand("机械手报警信号");
            }

            // X17 - 下相机报警信号（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.LOWER_CAMERA_ALARM))
            {
                SendEmergencyStopCommand("下相机报警信号");
            }

            // X19 - 机械臂气缸报警信号（使用LayeredIO内置上升沿检测）
            if (ReadRisingEdge(AlarmIOMapping.CYLINDER_ALARM))
            {
                SendEmergencyStopCommand("机械臂气缸报警信号");
            }
        }

        /// <summary>
        /// 读取输入点位
        /// </summary>
        private bool ReadInput(int index)
        {
            try
            {
                return _layeredIO?.ReadInBit(index, true) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 读取输入点位上升沿（使用LayeredIO内置边缘检测）
        /// </summary>
        private bool ReadRisingEdge(int index)
        {
            try
            {
                bool hasEdge = _layeredIO?.ReadRisingBit(index, true) ?? false;
                if (hasEdge)
                {
                    // 立即清除边缘标志，确保报警只处理一次
                    _layeredIO?.ClearRisingBit(index, true);
                }
                return hasEdge;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 清除指定输入点位的上升沿标志
        /// </summary>
        private void ClearRisingEdge(int index)
        {
            try
            {
                _layeredIO?.ClearRisingBit(index, true);
            }
            catch
            {
                // 忽略清除失败
            }
        }

        /// <summary>
        /// 发送暂停命令（双重消息：系统控制+状态指示）
        /// </summary>
        /// <param name="reason">暂停原因</param>
        private void SendPauseCommand(string reason)
        {
            try
            {
                // 发送系统控制命令到ProductionLineModule
                var systemControlMsg = new MessageModel(MsgSubject.SystemControl, SystemControlCommand.Pause);
                _msgManager.PushMsg(systemControlMsg);
                
                // 发送状态指示命令到SystemStatusIndicatorModule
                var statusIndicatorCommand = new StatusIndicatorCommand(SystemStatus.Warning, reason, false);
                var statusMsg = new MessageModel(MsgSubject.StatusIndicator, statusIndicatorCommand);
                _msgManager.PushMsg(statusMsg);
                
                // 记录日志
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                    $"系统暂停命令已发送: {reason}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "SafetyModule-SendPauseCommand", ex.Message);
            }
        }

        /// <summary>
        /// 发送紧急停机命令（双重消息：系统控制+状态指示）
        /// </summary>
        /// <param name="reason">紧急停机原因</param>
        private void SendEmergencyStopCommand(string reason)
        {
            try
            {
                // 发送系统控制命令到ProductionLineModule
                var systemControlMsg = new MessageModel(MsgSubject.SystemControl, SystemControlCommand.EmergencyStop);
                _msgManager.PushMsg(systemControlMsg);
                
                // 发送状态指示命令到SystemStatusIndicatorModule
                var statusIndicatorCommand = new StatusIndicatorCommand(SystemStatus.Critical, reason, true);
                var statusMsg = new MessageModel(MsgSubject.StatusIndicator, statusIndicatorCommand);
                _msgManager.PushMsg(statusMsg);
                
                // 记录日志
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                    $"紧急停机命令已发送: {reason}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "SafetyModule-SendEmergencyStopCommand", ex.Message);
            }
        }

        /// <summary>
        /// 发送安全报警消息给AutoProductionModule
        /// </summary>
        /// <param name="alertType">报警类型</param>
        /// <param name="alertLevel">报警级别</param>
        /// <param name="description">报警描述</param>
        /// <param name="requireEmergencyStop">是否需要急停</param>
        private void SendSafetyAlert(SafetyAlertType alertType, SafetyAlertLevel alertLevel, string description, bool requireEmergencyStop)
        {
            try
            {
                // 创建安全报警消息
                var safetyAlert = new SafetyAlert(alertType, alertLevel, description, requireEmergencyStop);
                var msg = new MessageModel(MsgSubject.SafetyAlert, safetyAlert);
                
                // 发送到消息队列
                _msgManager.PushMsg(msg);
                
                // 记录日志
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ErrorOccurred, 
                    "安全报警触发: " + description);
                
                // 同时发送给状态指示器（保持原有功能）
                var statusIndicatorCommand = new StatusIndicatorCommand(
                    alertLevel == SafetyAlertLevel.Critical ? SystemStatus.Critical : 
                    alertLevel == SafetyAlertLevel.Alarm ? SystemStatus.Alarm : SystemStatus.Warning, 
                    description, requireEmergencyStop);
                var statusMsg = new MessageModel(MsgSubject.StatusIndicator, statusIndicatorCommand);
                _msgManager.PushMsg(statusMsg);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "SafetyModule-SendSafetyAlert", ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置数据同步间隔
        /// </summary>
        /// <param name="milliseconds">间隔时间（毫秒）</param>
        public void SetDataSyncInterval(int milliseconds)
        {
            if (milliseconds >= 10 && milliseconds <= 1000)
            {
                _dataSyncInterval = milliseconds;
                _uiLogger.Info(() => $"IO同步间隔已设置为: {milliseconds}ms");
            }
        }

        /// <summary>
        /// 获取当前同步状态
        /// </summary>
        public bool IsConnected => _ioManager?.IsConnected ?? false;

        /// <summary>
        /// 获取LayeredIO实例（供其他模块使用）
        /// </summary>
        public LayeredIO GetLayeredIO() => _layeredIO;
        
        /// <summary>
        /// 手动发送安全报警（供其他模块调用）
        /// </summary>
        /// <param name="alertType">报警类型</param>
        /// <param name="alertLevel">报警级别</param>
        /// <param name="description">报警描述</param>
        /// <param name="requireEmergencyStop">是否需要急停</param>
        public void TriggerSafetyAlert(SafetyAlertType alertType, SafetyAlertLevel alertLevel, string description, bool requireEmergencyStop = false)
        {
            SendSafetyAlert(alertType, alertLevel, description, requireEmergencyStop);
        }

        #endregion
    }
}