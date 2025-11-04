using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.LogManager.Logger;
using Ewan.Model.Alarm;
using Ewan.Model.System;
using Ewan.Model.Safety;
using IOLibrary.Core.Layered;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
        private MsgListener _systemControlListener;

        private const int ROBOT_PAUSE_OUTPUT = 8;
        private readonly object _robotPausePulseLock = new object();
        private bool _robotPausePulseInProgress = false;
        private DateTime _lastRobotPausePulseTime = DateTime.MinValue;
        private readonly TimeSpan _robotPausePulseInterval = TimeSpan.FromMilliseconds(300);
        private const int ROBOT_PAUSE_PULSE_WIDTH_MS = 200;
        private const int ROBOT_RECOVERY_OUTPUT = 7;
        private readonly object _robotRecoveryPulseLock = new object();
        private bool _robotRecoveryPulseInProgress = false;
        private DateTime _lastRobotRecoveryPulseTime = DateTime.MinValue;
        private readonly TimeSpan _robotRecoveryPulseInterval = TimeSpan.FromMilliseconds(300);
        private const int ROBOT_RECOVERY_PULSE_WIDTH_MS = 200;
        private bool _robotRecoveryPending = false;
        
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
        private int _alarmCheckInterval = 2; // 报警检查计数器（2次同步检查一次报警，约20ms）- 优化：减少边沿遗漏风险
        private int _alarmCheckCounter = 0;

        // 报警防抖机制 - 避免同一报警在短时间内重复触发，防止日志刷屏
        private DateTime _lastEmergencyButtonTime = DateTime.MinValue;
        private DateTime _lastRobotAlarmTime = DateTime.MinValue;
        private DateTime _lastLowerCameraAlarmTime = DateTime.MinValue;
        private DateTime _lastCylinderAlarmTime = DateTime.MinValue;
        private DateTime _lastBin1LimitTime = DateTime.MinValue;
        private DateTime _lastBin2LimitTime = DateTime.MinValue;
        private DateTime _lastBin3LimitTime = DateTime.MinValue;
        private TimeSpan _alarmDebounceTime = TimeSpan.FromMilliseconds(500); // 报警防抖时间500ms

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

                _systemControlListener = new MsgListener(MsgSubject.SystemControl, OnSystemControlMessage);
                _msgManager.RegisterListener(_systemControlListener);

                SetRobotPauseOutput(false, "系统启动");

                // 如果未连接，则尝试连接
                if (!_ioManager.IsConnected)
                {
                    bool connected = _ioManager.Connect();
                    if (!connected)
                    {
                        _uiLogger.Warn("IO连接失败: {0}", "SafetyModule初始化");
                    }
                }

                _uiLogger.Info("模块初始化成功: {0}", "SafetyModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块初始化失败: {0} - {1}", "SafetyModule", ex.Message);
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
                _uiLogger.Error("模块运行错误: {0} - {1}", "SafetyModule", ex.Message);
                return false; // 停止运行
            }
        }

        /// <summary>
        /// 销毁模块
        /// </summary>
        protected override void OnDestroy()
        {
            try
            {
                if (_systemControlListener != null)
                {
                    _msgManager?.UnRegisterListener(_systemControlListener);
                    _systemControlListener = null;
                }

                SetRobotPauseOutput(false, "SafetyModule销毁");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块运行错误: {0} - {1}", "SafetyModule-OnDestroy", ex.Message);
            }

            // 断开连接
            if (_ioManager != null && _ioManager.IsConnected)
            {
                _ioManager.Disconnect();
            }
            
            _uiLogger.Info("模块已销毁: {0}", "SafetyModule");
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
                _uiLogger.Error("模块运行错误: {0} - {1}", 
                    "SafetyModule-CheckAlarmInputs", ex.Message);
            }
        }

        /// <summary>
        /// 检查暂停级报警 - X12, X13, X14
        /// </summary>
        private void CheckPauseAlarms()
        {
            // X12 - 料仓1下限位置信号（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.BIN1_LIMIT_ALARM))
            {
                // 无论是否通过防抖，都清除边沿标志，避免重复检测
                ClearRisingEdge(AlarmIOMapping.BIN1_LIMIT_ALARM);

                // 防抖检查通过才发送命令和日志
                if (CanTriggerAlarm(ref _lastBin1LimitTime))
                {
                    SendPauseCommand("料仓1下限位置异常");
                }
            }

            // X13 - 料仓2下限位置信号（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.BIN2_LIMIT_ALARM))
            {
                ClearRisingEdge(AlarmIOMapping.BIN2_LIMIT_ALARM);

                if (CanTriggerAlarm(ref _lastBin2LimitTime))
                {
                    SendPauseCommand("料仓2下限位置异常");
                }
            }

            // X14 - 料仓3下限位置信号（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.BIN3_LIMIT_ALARM))
            {
                ClearRisingEdge(AlarmIOMapping.BIN3_LIMIT_ALARM);

                if (CanTriggerAlarm(ref _lastBin3LimitTime))
                {
                    SendPauseCommand("料仓3下限位置异常");
                }
            }
        }

        /// <summary>
        /// 检查紧急停机报警 - X0, X15, X17, X19
        /// </summary>
        private void CheckEmergencyAlarms()
        {
            // X0 - 急停按钮（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.EMERGENCY_BUTTON))
            {
                // 无论是否通过防抖，都清除边沿标志，避免重复检测
                ClearRisingEdge(AlarmIOMapping.EMERGENCY_BUTTON);

                // 防抖检查通过才发送命令和日志
                if (CanTriggerAlarm(ref _lastEmergencyButtonTime))
                {
                    SendEmergencyStopCommand("急停按钮被按下");
                }
            }

            // X15 - 机械手报警信号（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.ROBOT_ALARM))
            {
                ClearRisingEdge(AlarmIOMapping.ROBOT_ALARM);

                if (CanTriggerAlarm(ref _lastRobotAlarmTime))
                {
                    SendEmergencyStopCommand("机械手报警信号");
                }
            }

            // X17 - 下相机报警信号（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.LOWER_CAMERA_ALARM))
            {
                ClearRisingEdge(AlarmIOMapping.LOWER_CAMERA_ALARM);

                if (CanTriggerAlarm(ref _lastLowerCameraAlarmTime))
                {
                    SendEmergencyStopCommand("下相机报警信号");
                }
            }

            // X19 - 机械臂气缸报警信号（使用LayeredIO内置上升沿检测 + 防抖）
            if (ReadRisingEdge(AlarmIOMapping.CYLINDER_ALARM))
            {
                ClearRisingEdge(AlarmIOMapping.CYLINDER_ALARM);

                if (CanTriggerAlarm(ref _lastCylinderAlarmTime))
                {
                    SendEmergencyStopCommand("机械臂气缸报警信号");
                }
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
        /// 注意：不立即清除边沿标志，由调用方在处理后清除，避免边沿遗漏
        /// </summary>
        private bool ReadRisingEdge(int index)
        {
            try
            {
                // 只读取边沿状态，不清除标志
                return _layeredIO?.ReadRisingBit(index, true) ?? false;
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
        /// 检查是否可以触发报警（防抖机制）
        /// </summary>
        /// <param name="lastTriggerTime">上次触发时间的引用</param>
        /// <returns>true=可以触发，false=在防抖时间内，忽略</returns>
        private bool CanTriggerAlarm(ref DateTime lastTriggerTime)
        {
            DateTime now = DateTime.Now;
            TimeSpan elapsed = now - lastTriggerTime;

            // 如果距离上次触发超过防抖时间，允许触发
            if (elapsed >= _alarmDebounceTime)
            {
                lastTriggerTime = now; // 更新触发时间
                return true;
            }

            // 在防抖时间内，忽略此次触发
            return false;
        }

        /// <summary>
        /// 发送暂停命令（双重消息：系统控制+状态指示）
        /// </summary>
        /// <param name="reason">暂停原因</param>
        private void SendPauseCommand(string reason)
        {
            try
            {
                TriggerRobotPausePulse(reason);

                // 发送系统控制命令到ProductionLineModule
                var systemControlMsg = new MessageModel(MsgSubject.SystemControl, SystemControlCommand.Pause);
                _msgManager.PushMsg(systemControlMsg);
                
                // 发送状态指示命令到SystemStatusIndicatorModule
                var statusIndicatorCommand = new StatusIndicatorCommand(SystemStatus.Warning, reason, false);
                var statusMsg = new MessageModel(MsgSubject.StatusIndicator, statusIndicatorCommand);
                _msgManager.PushMsg(statusMsg);
                
                // 记录日志
                _uiLogger.Warn("处理已完成: {0}", 
                    $"系统暂停命令已发送: {reason}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块运行错误: {0} - {1}", 
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
                TriggerRobotPausePulse(reason);

                // 发送系统控制命令到ProductionLineModule
                var systemControlMsg = new MessageModel(MsgSubject.SystemControl, SystemControlCommand.EmergencyStop);
                _msgManager.PushMsg(systemControlMsg);
                
                // 发送状态指示命令到SystemStatusIndicatorModule
                var statusIndicatorCommand = new StatusIndicatorCommand(SystemStatus.Critical, reason, true);
                var statusMsg = new MessageModel(MsgSubject.StatusIndicator, statusIndicatorCommand);
                _msgManager.PushMsg(statusMsg);
                
                // 记录日志
                _uiLogger.Error("处理已完成: {0}", 
                    $"紧急停机命令已发送: {reason}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块运行错误: {0} - {1}", 
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
                _uiLogger.Error("发生错误: {0}", 
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
                _uiLogger.Error("模块运行错误: {0} - {1}", 
                    "SafetyModule-SendSafetyAlert", ex.Message);
            }
        }

        private void OnSystemControlMessage(MessageModel message)
        {
            try
            {
                if (message?.Data is SystemControlCommand command)
                {
                    switch (command)
                    {
                        case SystemControlCommand.Pause:
                        case SystemControlCommand.EmergencyStop:
                            TriggerRobotPausePulse(command.ToString(), false);
                            break;
                        case SystemControlCommand.Initialize:
                            SetRobotPauseOutput(false, command.ToString());
                            TriggerRobotRecoveryPulse(command.ToString());
                            break;
                        case SystemControlCommand.Resume:
                        case SystemControlCommand.Start:
                        case SystemControlCommand.Stop:
                            SetRobotPauseOutput(false, command.ToString());
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块运行错误: {0} - {1}", "SafetyModule-SystemControlMessage", ex.Message);
            }
        }

        private void TriggerRobotPausePulse(string context, bool scheduleRecovery = true)
        {
            if (_layeredIO == null)
            {
                return;
            }

            lock (_robotPausePulseLock)
            {
                if (_robotPausePulseInProgress)
                {
                    return;
                }

                if ((DateTime.Now - _lastRobotPausePulseTime) < _robotPausePulseInterval)
                {
                    return;
                }

                _robotPausePulseInProgress = true;
                _lastRobotPausePulseTime = DateTime.Now;
                if (scheduleRecovery)
                {
                    _robotRecoveryPending = true;
                }
            }

            Task.Run(() =>
            {
                try
                {
                    bool setResult = _layeredIO.WriteOutBit(ROBOT_PAUSE_OUTPUT, true, true);
                    if (setResult)
                    {
                        Thread.Sleep(ROBOT_PAUSE_PULSE_WIDTH_MS);
                        _layeredIO.WriteOutBit(ROBOT_PAUSE_OUTPUT, false, true);
                        _uiLogger.Info("处理已完成: {0}", $"暂停输出脉冲发送: {context}");
                    }
                    else
                    {
                        _uiLogger.Warn("处理错误: {0}", $"暂停输出脉冲置位失败: {context}");
                    }
                }
                catch (Exception ex)
                {
                    _uiLogger.Error("模块运行错误: {0} - {1}", "SafetyModule-TriggerRobotPausePulse", ex.Message);
                }
                finally
                {
                    lock (_robotPausePulseLock)
                    {
                        _robotPausePulseInProgress = false;
                    }
                }
            });
        }

        private void TriggerRobotRecoveryPulse(string context)
        {
            if (_layeredIO == null)
            {
                return;
            }

            lock (_robotRecoveryPulseLock)
            {
                if (!_robotRecoveryPending)
                {
                    return;
                }

                if (_robotRecoveryPulseInProgress)
                {
                    return;
                }

                if ((DateTime.Now - _lastRobotRecoveryPulseTime) < _robotRecoveryPulseInterval)
                {
                    return;
                }

                _robotRecoveryPulseInProgress = true;
                _lastRobotRecoveryPulseTime = DateTime.Now;
                _robotRecoveryPending = false;
            }

            Task.Run(() =>
            {
                try
                {
                    bool setResult = _layeredIO.WriteOutBit(ROBOT_RECOVERY_OUTPUT, true, true);
                    if (setResult)
                    {
                        Thread.Sleep(ROBOT_RECOVERY_PULSE_WIDTH_MS);
                        _layeredIO.WriteOutBit(ROBOT_RECOVERY_OUTPUT, false, true);
                        _uiLogger.Info("处理已完成: {0}", $"暂停复原脉冲发送: {context}");
                    }
                    else
                    {
                        _uiLogger.Warn("处理错误: {0}", $"暂停复原脉冲置位失败: {context}");
                    }
                }
                catch (Exception ex)
                {
                    _uiLogger.Error("模块运行错误: {0} - {1}", "SafetyModule-TriggerRobotRecoveryPulse", ex.Message);
                }
                finally
                {
                    lock (_robotRecoveryPulseLock)
                    {
                        _robotRecoveryPulseInProgress = false;
                    }
                }
            });
        }

        private void SetRobotPauseOutput(bool activate, string context)
        {
            if (_layeredIO == null)
            {
                return;
            }

            try
            {
                bool result = _layeredIO.WriteOutBit(ROBOT_PAUSE_OUTPUT, activate, true);
                if (result)
                {
                    string status = activate ? "置位" : "复位";
                    _uiLogger.Info("处理已完成: {0}", $"暂停输出{status}: {context}");
                }
                else
                {
                    _uiLogger.Warn("处理错误: {0}", $"暂停输出写入失败: {context}");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块运行错误: {0} - {1}", "SafetyModule-SetRobotPauseOutput", ex.Message);
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