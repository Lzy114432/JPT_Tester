using Ewan.Core.IO;
using Ewan.Model.System;
using EwanCore.Messaging;
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

        private IDisposable _statusSubscription;
        private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
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
                // 订阅状态指示器消息
                _statusSubscription = MessageHub.Current.Subscribe<StatusIndicatorCommand>(OnStatusIndicatorReceived);

                // 初始化状态 - 设置为待机
                SetStandbyStatus();

                _appLogger.Info("SystemStatusIndicatorModule 初始化完成");
            }
            catch (Exception ex)
            {
                _appLogger.Error($"SystemStatusIndicatorModule 初始化失败: {ex.Message}");
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
            // 取消订阅
            _statusSubscription?.Dispose();
            _statusSubscription = null;

            // 停止所有任务
            StopAllTasks();

            // 程序关闭时 - 黄灯常亮
            SetLights(false, true, false);

            _appLogger.Info("SystemStatusIndicatorModule 已销毁");
        }

        #endregion

        #region 消息处理

        /// <summary>
        /// 处理外部发送的指示灯信号
        /// </summary>
        private void OnStatusIndicatorReceived(StatusIndicatorCommand command)
        {
            try
            {
                if (command != null)
                {
                    ProcessStatusCommand(command);
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"SystemStatusIndicator 消息回调错误: {ex.Message}");
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
                    _appLogger.Debug($"系统状态指示器: 初始化 - {command.Description}");
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
                    // 停止状态 - 黄灯常亮
                    StopAllTasks();
                    SetLights(false, true, false);
                    _appLogger.Debug($"系统状态指示器: 停止 - {command.Description}");
                    break;
            }
        }

        #endregion


        #region 公共方法

        /// <summary>
        /// 设置待机状态 - 黄灯常亮
        /// </summary>
        public void SetStandbyStatus()
        {
            StopAllTasks();
            SetLights(false, true, false);
            _appLogger.Debug("三色灯: 待机状态 - 黄灯常亮");
        }

        /// <summary>
        /// 设置运行状态 - 绿灯常亮
        /// </summary>
        public void SetRunningStatus()
        {
            StopAllTasks();
            SetLights(false, false, true);
            _appLogger.Debug("三色灯: 运行状态 - 绿灯常亮");
        }

        /// <summary>
        /// 设置暂停状态 - 黄灯常亮
        /// </summary>
        public void SetPausedStatus()
        {
            StopAllTasks();
            SetLights(false, true, false);
            _appLogger.Debug("三色灯: 暂停状态 - 黄灯常亮");
        }

        /// <summary>
        /// 设置警告状态 - 黄灯常亮
        /// </summary>
        public void SetWarningStatus()
        {
            StopAllTasks();
            SetLights(false, true, false);
            _appLogger.Debug("三色灯: 警告状态 - 黄灯常亮");
        }

        /// <summary>
        /// 设置报警状态 - 红灯常亮 + 蜂鸣器
        /// </summary>
        public void SetAlarmStatus(bool critical = false)
        {
            StopAllTasks();

            // 所有报警统一为红灯常亮 + 蜂鸣器
            SetLights(true, false, false);

            if (critical)
            {
                // 严重报警 - 红灯常亮 + 蜂鸣器10秒
                StartBuzzer(10000);
                _appLogger.Error("三色灯: 严重报警 - 红灯常亮 + 蜂鸣器");
            }
            else
            {
                // 一般报警 - 红灯常亮 + 蜂鸣器5秒
                StartBuzzer(5000);
                _appLogger.Warn("三色灯: 报警状态 - 红灯常亮 + 蜂鸣器");
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
                // 实际控制IO输出
                if (_ioManager != null && _ioManager.IsConnected)
                {
                    var ctx = _ioManager.Ctx;
                    if (ctx == null)
                    {
                        return;
                    }

                    if (red)
                    {
                        ctx.On(x => x.红灯);
                    }
                    else
                    {
                        ctx.Off(x => x.红灯);
                    }

                    if (yellow)
                    {
                        ctx.On(x => x.黄灯);
                    }
                    else
                    {
                        ctx.Off(x => x.黄灯);
                    }

                    if (green)
                    {
                        ctx.On(x => x.绿灯);
                    }
                    else
                    {
                        ctx.Off(x => x.绿灯);
                    }
                }
                else
                {
                    _appLogger.Warn("IO管理器未连接，无法控制三色灯");
                    _appLogger.Debug($"预期三色灯状态: 红={red}, 黄={yellow}, 绿={green}");
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"三色灯IO写入错误: {ex.Message}");
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
                    _appLogger.Error($"SystemStatusIndicator 灯光闪烁错误: {ex.Message}");
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
            try
            {
                var cancellation = _flashingCancellation;
                if (cancellation != null)
                {
                    _flashingCancellation = null; // 先置空，避免重复调用

                    if (!cancellation.IsCancellationRequested)
                    {
                        cancellation.Cancel();
                    }
                    cancellation.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // 对象已释放，忽略
            }
            catch (Exception ex)
            {
                _appLogger.Error($"停止闪烁时发生错误: {ex.Message}");
            }
            finally
            {
                _lightFlashingActive = false;
            }
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
                // 实际控制蜂鸣器IO输出
                if (_ioManager != null && _ioManager.IsConnected)
                {
                    var ctx = _ioManager.Ctx;
                    if (ctx != null)
                    {
                        ctx.On(x => x.蜂鸣器);
                        _appLogger.Debug("蜂鸣器启动");
                    }
                }
                else
                {
                    _appLogger.Warn("IO管理器未连接，无法启动蜂鸣器");
                }
            }
            catch (Exception ex)
            {
                _appLogger.Error($"蜂鸣器IO写入错误: {ex.Message}");
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
                    _appLogger.Error($"SystemStatusIndicator 蜂鸣器超时错误: {ex.Message}");
                }
            }, _buzzerCancellation.Token);

            _appLogger.Debug($"蜂鸣器将在 {duration}ms 后自动停止");
        }

        /// <summary>
        /// 停止蜂鸣器
        /// </summary>
        private void StopBuzzer()
        {
            try
            {
                var cancellation = _buzzerCancellation;
                if (cancellation != null)
                {
                    _buzzerCancellation = null; // 先置空，避免重复调用

                    if (!cancellation.IsCancellationRequested)
                    {
                        cancellation.Cancel();
                    }
                    cancellation.Dispose();
                }

                // 关闭蜂鸣器IO输出
                if (_ioManager != null && _ioManager.IsConnected)
                {
                    var ctx = _ioManager.Ctx;
                    if (ctx != null)
                    {
                        ctx.Off(x => x.蜂鸣器);
                        _appLogger.Debug("蜂鸣器停止");
                    }
                }
                else
                {
                    _appLogger.Warn("IO管理器未连接，无法停止蜂鸣器");
                }
            }
            catch (ObjectDisposedException)
            {
                // 对象已释放，忽略
            }
            catch (Exception ex)
            {
                _appLogger.Error($"蜂鸣器IO写入错误: {ex.Message}");
            }
            finally
            {
                _buzzerActive = false;
            }
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
