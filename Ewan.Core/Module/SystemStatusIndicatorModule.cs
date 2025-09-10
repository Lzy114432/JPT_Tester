using Ewan.Core.Msg;
using Ewan.Model.Alarm;
using Ewan.Model.System;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 系统状态指示器模块 - 统一控制三色灯和蜂鸣器
    /// 根据系统状态（待机、运行、报警等）显示不同的灯光效果
    /// </summary>
    public class SystemStatusIndicatorModule : BaseModule<SystemStatusIndicatorModule>
    {
        #region 私有字段

        private MsgListener _msgListener;
        // IOController引用移除，避免循环依赖
        private SystemStatus _currentStatus = SystemStatus.Initializing;
        
        // 蜂鸣器控制
        private bool _buzzerActive = false;
        private CancellationTokenSource _buzzerCancellation;
        
        // 灯光控制
        private bool _lightFlashingActive = false;
        private CancellationTokenSource _flashingCancellation;
        
        
        // 更新间隔
        private int _updateInterval = 200;

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                // 注册消息监听器
                _msgListener = new MsgListener(MsgSubject.StatusIndicator, CallBackStatusIndicator);
                MsgManager.Instance().RegisterListener(_msgListener);
                
                // 初始化状态 - 设置为待机
                SetStandbyStatus();

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "SystemStatusIndicatorModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "SystemStatusIndicatorModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            // OnRun部分不需要实现具体逻辑，只通过消息传递控制
            Thread.Sleep(200);
            return true;
        }

        protected override void OnDestroy()
        {
            // 注销消息监听器
            if (_msgListener != null)
            {
                MsgManager.Instance().UnRegisterListener(_msgListener);
            }
            
            // 停止所有任务
            StopAllTasks();
            
            // 关闭所有指示器
            SetLights(false, false, false);
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "SystemStatusIndicatorModule");
        }

        #endregion

        #region 消息处理

        /// <summary>
        /// 处理外部发送的指示灯信号
        /// </summary>
        private void CallBackStatusIndicator(MessageModel msg)
        {
            try
            {
                var command = msg.GetData<StatusIndicatorCommand>();
                if (command != null)
                {
                    ProcessStatusCommand(command);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "SystemStatusIndicator-MessageCallback", ex.Message);
            }
        }

        /// <summary>
        /// 处理状态控制命令
        /// </summary>
        private void ProcessStatusCommand(StatusIndicatorCommand command)
        {
            _currentStatus = command.Status;
            
            switch (command.Status)
            {
                case SystemStatus.Initializing:
                    // 初始化状态 - 所有灯关闭
                    SetLights(false, false, false);
                    _uiLogger.Info(() => $"状态指示器: 初始化状态 - {command.Description}");
                    break;
                    
                case SystemStatus.Standby:
                    SetStandbyStatus();
                    break;
                    
                case SystemStatus.Running:
                    SetRunningStatus();
                    break;
                    
                case SystemStatus.Paused:
                    SetPausedStatus();
                    break;
                    
                case SystemStatus.Warning:
                    SetWarningStatus();
                    break;
                    
                case SystemStatus.Alarm:
                case SystemStatus.Critical:
                    SetAlarmStatus(command.IsCritical || command.Status == SystemStatus.Critical);
                    break;
                    
                case SystemStatus.Stopped:
                    // 停止状态 - 所有灯关闭
                    StopAllTasks();
                    SetLights(false, false, false);
                    _uiLogger.Info(() => $"状态指示器: 停止状态 - {command.Description}");
                    break;
            }
        }

        #endregion


        #region 公共方法

        /// <summary>
        /// 设置待机状态 - 绿灯常亮
        /// </summary>
        public void SetStandbyStatus()
        {
            StopAllTasks();
            SetLights(false, false, true);
            _uiLogger.Info(() => "三色灯: 待机状态 - 绿灯常亮");
        }

        /// <summary>
        /// 设置运行状态 - 绿灯闪烁
        /// </summary>
        public void SetRunningStatus()
        {
            StopAllTasks();
            StartGreenLightFlashing();
            _uiLogger.Info(() => "三色灯: 运行状态 - 绿灯闪烁");
        }

        /// <summary>
        /// 设置暂停状态 - 黄灯常亮
        /// </summary>
        public void SetPausedStatus()
        {
            StopAllTasks();
            SetLights(false, true, false);
            _uiLogger.Info(() => "三色灯: 暂停状态 - 黄灯常亮");
        }

        /// <summary>
        /// 设置警告状态 - 黄灯闪烁
        /// </summary>
        public void SetWarningStatus()
        {
            StopAllTasks();
            StartYellowLightFlashing();
            _uiLogger.Info(() => "三色灯: 警告状态 - 黄灯闪烁");
        }

        /// <summary>
        /// 设置报警状态 - 红灯 + 蜂鸣器
        /// </summary>
        public void SetAlarmStatus(bool critical = false)
        {
            StopAllTasks();
            
            if (critical)
            {
                // 严重报警 - 红灯常亮 + 蜂鸣器10秒
                SetLights(true, false, false);
                StartBuzzer(10000);
                _uiLogger.Error(() => "三色灯: 严重报警 - 红灯常亮 + 蜂鸣器");
            }
            else
            {
                // 一般报警 - 红灯闪烁 + 蜂鸣器5秒
                StartRedLightFlashing();
                StartBuzzer(5000);
                _uiLogger.Warn(() => "三色灯: 报警状态 - 红灯闪烁 + 蜂鸣器");
            }
        }

        /// <summary>
        /// 报警复位 - 回到待机状态
        /// </summary>
        public void ResetAlarm()
        {
            SetStandbyStatus();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 设置三色灯状态
        /// </summary>
        private void SetLights(bool red, bool yellow, bool green)
        {
            try
            {
                // 记录需要设置的IO状态（实际IO操作在StreamController中实现）
                _uiLogger.Info(() => $"需要设置三色灯: 红灯={red}, 黄灯={yellow}, 绿灯={green}");
                _uiLogger.Debug(() => $"IO点位: 红灯={AlarmIOMapping.RED_LIGHT}, 黄灯={AlarmIOMapping.YELLOW_LIGHT}, 绿灯={AlarmIOMapping.GREEN_LIGHT}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOWriteError, "Lights", ex.Message);
            }
        }

        /// <summary>
        /// 开始绿灯闪烁
        /// </summary>
        private void StartGreenLightFlashing()
        {
            StartLightFlashing(() => SetLights(false, false, true), () => SetLights(false, false, false));
        }

        /// <summary>
        /// 开始黄灯闪烁
        /// </summary>
        private void StartYellowLightFlashing()
        {
            StartLightFlashing(() => SetLights(false, true, false), () => SetLights(false, false, false));
        }

        /// <summary>
        /// 开始红灯闪烁
        /// </summary>
        private void StartRedLightFlashing()
        {
            StartLightFlashing(() => SetLights(true, false, false), () => SetLights(false, false, false));
        }


        /// <summary>
        /// 通用灯光闪烁方法
        /// </summary>
        private void StartLightFlashing(Action onAction, Action offAction)
        {
            StopFlashing();
            
            _lightFlashingActive = true;
            _flashingCancellation = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                try
                {
                    bool lightState = false;
                    while (!_flashingCancellation.Token.IsCancellationRequested)
                    {
                        if (lightState)
                            onAction?.Invoke();
                        else
                            offAction?.Invoke();
                            
                        lightState = !lightState;
                        await Task.Delay(500, _flashingCancellation.Token); // 500ms闪烁间隔
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                        "SystemStatusIndicator-LightFlashing", ex.Message);
                }
                finally
                {
                    _lightFlashingActive = false;
                }
            }, _flashingCancellation.Token);
        }

        /// <summary>
        /// 停止闪烁
        /// </summary>
        private void StopFlashing()
        {
            if (_flashingCancellation != null)
            {
                _flashingCancellation.Cancel();
                _flashingCancellation.Dispose();
                _flashingCancellation = null;
            }
            _lightFlashingActive = false;
        }

        /// <summary>
        /// 启动蜂鸣器
        /// </summary>
        private void StartBuzzer(int duration)
        {
            StopBuzzer();
            
            _buzzerActive = true;
            _buzzerCancellation = new CancellationTokenSource();
            
            try
            {
                // 记录需要启动蜂鸣器（实际IO操作在StreamController中实现）
                _uiLogger.Info(() => $"需要启动蜂鸣器: IO点位={AlarmIOMapping.BUZZER}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOWriteError, "Buzzer", ex.Message);
            }
            
            // 异步停止蜂鸣器
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(duration, _buzzerCancellation.Token);
                    StopBuzzer();
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
                catch (Exception ex)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                        "SystemStatusIndicator-BuzzerTimeout", ex.Message);
                }
            }, _buzzerCancellation.Token);
            
            _uiLogger.Debug(() => $"蜂鸣器启动，持续时间: {duration}ms");
        }

        /// <summary>
        /// 停止蜂鸣器
        /// </summary>
        private void StopBuzzer()
        {
            if (_buzzerCancellation != null)
            {
                _buzzerCancellation.Cancel();
                _buzzerCancellation.Dispose();
                _buzzerCancellation = null;
            }
            
            try
            {
                // 记录需要停止蜂鸣器（实际IO操作在StreamController中实现）
                _uiLogger.Info(() => $"需要停止蜂鸣器: IO点位={AlarmIOMapping.BUZZER}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOWriteError, "Buzzer", ex.Message);
            }
            
            _buzzerActive = false;
        }

        /// <summary>
        /// 停止所有异步任务
        /// </summary>
        private void StopAllTasks()
        {
            StopBuzzer();
            StopFlashing();
        }

        #endregion

        #region 状态查询

        /// <summary>
        /// 蜂鸣器是否激活
        /// </summary>
        public bool IsBuzzerActive()
        {
            return _buzzerActive;
        }

        /// <summary>
        /// 灯光是否在闪烁
        /// </summary>
        public bool IsLightFlashing()
        {
            return _lightFlashingActive;
        }

        #endregion
    }
}