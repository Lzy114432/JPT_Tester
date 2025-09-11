using System;
using System.Threading;
using Ewan.Model.Production;
using Ewan.Model.Safety;
using Ewan.Model.System;
using Ewan.Core.Msg;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 自动生产流程模块
    /// 负责协调机械手抓取、定位、扫码、分料等主流程
    /// </summary>
    public class AutoProductionModule : BaseModule<AutoProductionModule>
    {
        private AutoProductionState _currentState = AutoProductionState.Idle;
        private AutoProductionState _pausedFromState = AutoProductionState.Idle; // 暂停前的状态
        private SystemMode _currentMode = SystemMode.Manual; // 默认手动模式
        private readonly object _stateLock = new object();
        private int _scanInterval = 100; // 扫描间隔，毫秒
        private bool _emergencyStopTriggered = false;
        private bool _systemInitialized = false;
        private bool _pauseRequested = false;
        private bool _stopRequested = false;
        private bool _systemEnabled = false; // 系统启用状态
        
        // 消息队列相关
        private MsgManager _msgManager;
        private MsgListener _safetyListener;
        
        
        protected override void OnInit()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "AutoProductionModule");
            
            // 初始化消息管理器
            _msgManager = MsgManager.Instance();
            
            // 创建并注册安全报警消息监听器
            _safetyListener = new MsgListener(MsgSubject.SafetyAlert, OnSafetyAlertReceived);
            _msgManager.RegisterListener(_safetyListener);
            
            
            // 初始化系统状态
            InitializeSystem();
        }

        protected override bool OnRun()
        {
            try
            {
                // 优先检查急停信号
                if (CheckEmergencyStop())
                {
                    HandleEmergencyStop();
                    Thread.Sleep(_scanInterval);
                    return true; // 继续运行模块，但保持急停状态
                }

                // 检查系统是否已初始化
                if (!_systemInitialized)
                {
                    Thread.Sleep(_scanInterval);
                    return true; // 等待系统初始化完成
                }

                // 检查启用信号（在锁外检查）
                CheckEnableSignal();

                lock (_stateLock)
                {
                    // 优先处理暂停和停止请求
                    if (_pauseRequested && _currentState != AutoProductionState.Paused)
                    {
                        HandlePauseRequest();
                    }
                    else if (_stopRequested && _currentState != AutoProductionState.Stopping && _currentState != AutoProductionState.Stopped)
                    {
                        HandleStopRequest();
                    }

                    switch (_currentState)
                    {
                        case AutoProductionState.Idle:
                            CheckForSignals();
                            break;
                            
                        case AutoProductionState.WaitingForScan:
                            ProcessScanState();
                            break;
                            
                        case AutoProductionState.WaitingForExternalComplete:
                            CheckExternalComplete();
                            break;
                            
                        case AutoProductionState.ProcessingCartRequest:
                            ProcessCartRequest();
                            break;

                        case AutoProductionState.Paused:
                            // 暂停状态下不执行任何操作，只等待恢复
                            break;

                        case AutoProductionState.Stopping:
                            ProcessStoppingState();
                            break;

                        case AutoProductionState.Stopped:
                            // 已停止状态，等待重新启动
                            break;
                            
                        default:
                            _currentState = AutoProductionState.Idle;
                            break;
                    }
                }
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "AutoProductionModule: " + ex.Message);
                _currentState = AutoProductionState.Idle;
                Thread.Sleep(1000); // 错误时等待更长时间
                return true;
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "AutoProductionModule");
        }

        #region 核心流程处理

        /// <summary>
        /// 检查扫码信号和小车信号
        /// </summary>
        private void CheckForSignals()
        {
            // 只有在系统启用且自动模式下才自动响应信号
            if (!_systemEnabled)
            {
                return; // 系统未启用，不响应信号
            }

            if (_currentMode != SystemMode.Auto)
            {
                return; // 手动模式下不自动响应信号
            }

            // 只有在机械手完全空闲时才能开始新任务
            // 这样确保当前任务完整完成，避免料片无处放置的问题
            
            // 优先处理扫码信号（扫码放入料仓的优先级最高）
            if (HasScanSignal())
            {
                _currentState = AutoProductionState.WaitingForScan;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "自动模式收到扫码信号");
                return;
            }

            // 其次处理小车请求（小车有请求时）
            if (HasCartRequest())
            {
                _currentState = AutoProductionState.ProcessingCartRequest;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "自动模式小车取料请求");
            }
        }

        /// <summary>
        /// 处理扫码状态 - 本系统负责
        /// </summary>
        private void ProcessScanState()
        {
            // 执行扫码识别 - 本系统实际操作
            string partCode = PerformScanning();
            
            if (!string.IsNullOrEmpty(partCode))
            {
                // 发送扫码完成信号给外包系统
                SendScanCompleteSignal(partCode);
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "扫码识别-" + partCode);
                
                // 等待外包流程完成（分料等）
                _currentState = AutoProductionState.WaitingForExternalComplete;
            }
            else
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "扫码识别失败");
                // 扫码失败，重试或回到空闲状态
                HandleScanFailure();
            }
        }

        /// <summary>
        /// 检查外包流程是否完成
        /// </summary>
        private void CheckExternalComplete()
        {
            // TODO: 检查外包系统是否完成分料等操作
            if (IsExternalProcessComplete())
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "外包流程完成");
                _currentState = AutoProductionState.Idle;
            }
        }

        #endregion

        #region 小车流程处理

        /// <summary>
        /// 处理小车取料请求
        /// </summary>
        private void ProcessCartRequest()
        {
            // 一旦开始小车取料流程，必须完整执行完毕，不能被中断
            // 在这个状态下，新的扫码信号需要等待或与外包沟通延迟
            
            if (ProcessCartPickup())
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "小车取料完成");
                _currentState = AutoProductionState.Idle;
            }
            // 如果返回false，保持当前状态继续处理
        }

        /// <summary>
        /// 执行小车取料操作
        /// </summary>
        /// <returns>是否完成</returns>
        private bool ProcessCartPickup()
        {
            try
            {
                // TODO: 实现小车取料逻辑
                // 1. 检查机械手是否空闲
                // 2. 根据小车请求的料号，从对应料仓取料
                // 3. 扫码确认料号正确
                // 4. 送入小车

                return true; // 暂时返回true，等待实际实现
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "小车取料失败: " + ex.Message);
                return false;
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取当前流程状态
        /// </summary>
        public AutoProductionState GetCurrentState()
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }

        /// <summary>
        /// 检查系统是否繁忙
        /// </summary>
        public bool IsSystemBusy()
        {
            return _currentState != AutoProductionState.Idle && 
                   _currentState != AutoProductionState.Stopped && 
                   _currentState != AutoProductionState.Paused;
        }

        /// <summary>
        /// 暂停流程 - 保持当前状态，可恢复
        /// </summary>
        public void PauseFlow()
        {
            lock (_stateLock)
            {
                if (_currentState != AutoProductionState.Error && 
                    _currentState != AutoProductionState.Paused && 
                    _currentState != AutoProductionState.Stopped)
                {
                    _pauseRequested = true;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "流程暂停请求");
                }
            }
        }

        /// <summary>
        /// 恢复流程 - 从暂停状态恢复
        /// </summary>
        public void ResumeFlow()
        {
            lock (_stateLock)
            {
                if (_currentState == AutoProductionState.Paused)
                {
                    _currentState = _pausedFromState;
                    _pauseRequested = false;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "流程恢复运行");
                }
            }
        }

        /// <summary>
        /// 停止流程 - 完成当前步骤后停止
        /// </summary>
        public void StopFlow()
        {
            lock (_stateLock)
            {
                if (_currentState != AutoProductionState.Error && 
                    _currentState != AutoProductionState.Stopped)
                {
                    _stopRequested = true;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "流程停止请求");
                }
            }
        }

        /// <summary>
        /// 强制停止流程 - 立即停止到空闲状态
        /// </summary>
        public void ForceStopFlow()
        {
            lock (_stateLock)
            {
                _currentState = AutoProductionState.Idle;
                _pauseRequested = false;
                _stopRequested = false;
            }
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.StreamStopped, "自动流程强制停止");
        }

        #endregion

        #region 信号检查方法

        /// <summary>
        /// 检查是否有扫码信号
        /// </summary>
        /// <returns>是否有扫码信号</returns>
        private bool HasScanSignal()
        {
            // TODO: 检查外包系统发送的扫码信号（料片已到扫码位置）
            // return IOManager.GetInput(AutoProductionIO.ScanPermissionGranted);
            return false; // 暂时返回false，等待IO实现
        }

        /// <summary>
        /// 检查是否有小车请求
        /// </summary>
        /// <returns>是否有小车请求</returns>
        private bool HasCartRequest()
        {
            // TODO: 检查小车请求信号
            // return IOManager.GetInput(AutoProductionIO.CartPickupRequest);
            return false; // 暂时返回false，等待IO实现
        }

        /// <summary>
        /// 检查外包流程是否完成
        /// </summary>
        /// <returns>外包流程是否完成</returns>
        private bool IsExternalProcessComplete()
        {
            // TODO: 检查外包系统是否完成分料等后续操作
            // return IOManager.GetInput(AutoProductionIO.SortingCompleted);
            return false; // 暂时返回false，等待IO实现
        }

        /// <summary>
        /// 检查机械臂是否忙碌（正在执行任何流程）
        /// </summary>
        /// <returns>机械臂是否忙碌</returns>
        private bool IsRobotBusy()
        {
            // 机械臂在以下状态时忙碌，不能开始新的任务：
            // 1. 正在等待扫码或执行扫码
            // 2. 正在等待外包流程完成（料片还在机械臂上）
            // 3. 正在处理小车取料请求
            lock (_stateLock)
            {
                return _currentState == AutoProductionState.WaitingForScan ||
                       _currentState == AutoProductionState.WaitingForExternalComplete ||
                       _currentState == AutoProductionState.ProcessingCartRequest;
            }
            
            // TODO: 也可以通过IO信号检查机械臂实际状态
            // return IOManager.GetInput(AutoProductionIO.RobotBusy);
        }

        #endregion

        #region 扫码相关方法 - 本系统负责


        /// <summary>
        /// 执行扫码识别 - 本系统实际操作
        /// </summary>
        /// <returns>扫码结果，失败返回空字符串</returns>
        private string PerformScanning()
        {
            try
            {
                // TODO: 调用扫码设备进行识别
                // 这里是您需要实现的扫码逻辑
                // return ScannerDevice.Scan();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "执行扫码识别");
                return ""; // 暂时返回空，等待实际扫码实现
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "扫码识别: " + ex.Message);
                return "";
            }
        }

        /// <summary>
        /// 发送扫码完成信号给外包系统
        /// </summary>
        /// <param name="partCode">扫码结果</param>
        private void SendScanCompleteSignal(string partCode)
        {
            try
            {
                // TODO: 发送扫码完成信号和料号给外包系统
                // IOManager.SetOutput(AutoProductionIO.SendScanCompleteSignal, true);
                // IOManager.SetPartCode(partCode); // 传递料号给外包系统
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "发送扫码结果-" + partCode);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "发送扫码结果: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理扫码失败
        /// </summary>
        private void HandleScanFailure()
        {
            try
            {
                // TODO: 扫码失败处理逻辑
                // 1. 重试机制
                // 2. 报警提示  
                // 3. 停止流程等
                
                _uiLogger.Error(() => Ewan.Resources.LogMessages.WarningMessage, "扫码失败，回到空闲状态");
                _currentState = AutoProductionState.Idle; // 暂时回到空闲状态
                // 系统已进入急停状态
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "扫码失败处理: " + ex.Message);
            }
        }

        #endregion

        #region 系统初始化和控制

        /// <summary>
        /// 初始化系统
        /// </summary>
        private void InitializeSystem()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationStarted, "自动流程系统初始化开始");

                // TODO: 检查所有设备连接状态
                // CheckDeviceConnections();

                // TODO: 检查安全条件
                // CheckSafetyConditions();

                // TODO: 初始化IO状态
                // InitializeIOStates();

                // 设置初始状态
                lock (_stateLock)
                {
                    _currentState = AutoProductionState.Idle;
                    _emergencyStopTriggered = false;
                }

                _systemInitialized = true;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "自动流程系统初始化完成");
            }
            catch (Exception ex)
            {
                _systemInitialized = false;
                _uiLogger.Error(() => Ewan.Resources.LogMessages.InitializationFailed, "系统初始化失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 启动自动流程 - 外部调用
        /// </summary>
        public bool StartAutoFlow()
        {
            lock (_stateLock)
            {
                if (_emergencyStopTriggered)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "系统处于急停状态，无法启动流程");
                    return false;
                }

                if (!_systemInitialized)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "系统未初始化，无法启动流程");
                    return false;
                }

                // 重置停止和暂停标志
                _stopRequested = false;
                _pauseRequested = false;
                _currentState = AutoProductionState.Idle;
                _pausedFromState = AutoProductionState.Idle;
                
                // 通过消息队列通知系统启动
                var startMsg = SystemStatusMessage.CreateStartedMessage(true, "自动流程启动");
                _msgManager.PushMsg(new MessageModel(MsgSubject.SystemStatus, startMsg));
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "自动流程启动");
                return true;
            }
        }

        /// <summary>
        /// 停止自动流程 - 外部调用
        /// </summary>
        public void StopAutoFlow()
        {
            lock (_stateLock)
            {
                _currentState = AutoProductionState.Idle;
            }
            
            // 通过消息队列通知系统停止
            var stopMsg = SystemStatusMessage.CreateStartedMessage(false, "自动流程停止");
            _msgManager.PushMsg(new MessageModel(MsgSubject.SystemStatus, stopMsg));
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "自动流程停止");
        }

        /// <summary>
        /// 软件急停 - 外部调用
        /// </summary>
        public void EmergencyStop()
        {
            lock (_stateLock)
            {
                if (!_emergencyStopTriggered)
                {
                    _emergencyStopTriggered = true;
                    _currentState = AutoProductionState.Error;
                    
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ErrorOccurred, "软件急停触发");
                    
                    // 执行软件层面的停止操作
                    ExecuteSoftwareStop();
                }
            }
        }

        /// <summary>
        /// 急停复位 - 外部调用，需要手动复位才能重新启动
        /// </summary>
        public bool ResetEmergencyStop()
        {
            lock (_stateLock)
            {
                if (_emergencyStopTriggered)
                {
                    try
                    {
                        _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemStartup, "急停复位开始");

                        // TODO: 检查是否可以安全复位
                        // if (!CheckSafetyConditions())
                        // {
                        //     _uiLogger.Error(() => Ewan.Resources.LogMessages.WarningMessage, "安全条件不满足，无法复位");
                        //     return false;
                        // }

                        _emergencyStopTriggered = false;
                        _currentState = AutoProductionState.Idle;

                        _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemStartup, "急停复位完成，系统恢复空闲状态");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "急停复位失败: " + ex.Message);
                        return false;
                    }
                }
                else
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.WarningMessage, "系统未处于急停状态");
                    return false;
                }
            }
        }

        /// <summary>
        /// 执行软件层面的停止操作
        /// </summary>
        private void ExecuteSoftwareStop()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "执行软件停止序列");

                // TODO: 停止扫码设备
                // StopScannerDevice();

                // TODO: 发送停止信号给外包系统
                // SendStopSignalToExternalSystem();

                // 清理内部状态
                _currentState = AutoProductionState.Error;

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "软件停止序列完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "软件停止失败: " + ex.Message);
            }
        }

        #endregion

        #region 暂停和停止处理

        /// <summary>
        /// 处理暂停请求
        /// </summary>
        private void HandlePauseRequest()
        {
            try
            {
                if (_currentState != AutoProductionState.Paused)
                {
                    _pausedFromState = _currentState;
                    _currentState = AutoProductionState.Paused;
                    _pauseRequested = false;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "流程已暂停");
                    
                    // TODO: 通知外包系统暂停操作
                    // NotifyExternalSystemPause();
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "暂停处理: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理停止请求
        /// </summary>
        private void HandleStopRequest()
        {
            try
            {
                // 优雅停止：让当前操作完成后再停止
                if (CanSafelyStop())
                {
                    _currentState = AutoProductionState.Stopped;
                    _stopRequested = false;
                    _pauseRequested = false;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "流程已停止");
                    
                    // TODO: 通知外包系统停止操作
                    // NotifyExternalSystemStop();
                }
                else
                {
                    // 不能立即停止，进入停止中状态
                    _currentState = AutoProductionState.Stopping;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "流程正在停止中");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "停止处理: " + ex.Message);
            }
        }

        /// <summary>
        /// 处理停止中状态
        /// </summary>
        private void ProcessStoppingState()
        {
            try
            {
                // 检查是否可以安全停止
                if (CanSafelyStop())
                {
                    _currentState = AutoProductionState.Stopped;
                    _stopRequested = false;
                    _pauseRequested = false;
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "流程停止完成");
                    
                    // TODO: 执行停止清理操作
                    // ExecuteStopCleanup();
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "停止状态处理: " + ex.Message);
            }
        }

        /// <summary>
        /// 检查是否可以安全停止
        /// </summary>
        /// <returns>是否可以安全停止</returns>
        private bool CanSafelyStop()
        {
            // 在空闲状态或等待状态下可以安全停止
            return _currentState == AutoProductionState.Idle ||
                   _currentState == AutoProductionState.WaitingForScan ||
                   _currentState == AutoProductionState.WaitingForExternalComplete;
        }

        /// <summary>
        /// 获取流程是否处于暂停状态
        /// </summary>
        public bool IsPaused()
        {
            lock (_stateLock)
            {
                return _currentState == AutoProductionState.Paused;
            }
        }

        /// <summary>
        /// 获取流程是否处于停止状态
        /// </summary>
        public bool IsStopped()
        {
            lock (_stateLock)
            {
                return _currentState == AutoProductionState.Stopped;
            }
        }

        /// <summary>
        /// 获取流程是否正在停止中
        /// </summary>
        public bool IsStopping()
        {
            lock (_stateLock)
            {
                return _currentState == AutoProductionState.Stopping;
            }
        }

        #endregion

        #region 急停处理 - 安全关键功能

        /// <summary>
        /// 检查急停信号 - 电气已实现硬件急停
        /// </summary>
        /// <returns>是否触发急停</returns>
        private bool CheckEmergencyStop()
        {
            // TODO: 检查来自电气的急停信号
            // return IOManager.GetInput(AutoProductionIO.EmergencyStop);
            return false; // 暂时返回false，等待IO实现
        }

        /// <summary>
        /// 处理急停 - 软件层面处理
        /// </summary>
        private void HandleEmergencyStop()
        {
            if (!_emergencyStopTriggered)
            {
                _emergencyStopTriggered = true;
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ErrorOccurred, "急停信号检测到！");

                lock (_stateLock)
                {
                    _currentState = AutoProductionState.Error;
                    
                    // 执行软件停止
                    ExecuteSoftwareStop();
                }
            }
        }

        /// <summary>
        /// 检查系统是否处于急停状态
        /// </summary>
        public bool IsEmergencyStopActive()
        {
            return _emergencyStopTriggered;
        }

        /// <summary>
        /// 获取系统初始化状态
        /// </summary>
        public bool IsSystemInitialized()
        {
            return _systemInitialized;
        }

        /// <summary>
        /// 获取机械手是否忙碌状态 - 供外部系统查询
        /// </summary>
        /// <returns>机械手是否正在执行任务</returns>
        public bool IsRobotBusyForExternal()
        {
            return IsRobotBusy();
        }

        /// <summary>
        /// 设置系统运行模式
        /// </summary>
        /// <param name="mode">运行模式</param>
        public void SetSystemMode(SystemMode mode)
        {
            lock (_stateLock)
            {
                if (_currentMode != mode)
                {
                    _currentMode = mode;
                    
                    // 通过消息队列同步模式
                    var modeMsg = SystemStatusMessage.CreateModeChangedMessage(mode, "系统模式切换");
                    _msgManager.PushMsg(new MessageModel(MsgSubject.SystemStatus, modeMsg));
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        mode == SystemMode.Auto ? "切换到自动模式" : "切换到手动模式");
                }
            }
        }

        /// <summary>
        /// 获取当前系统运行模式
        /// </summary>
        /// <returns>当前运行模式</returns>
        public SystemMode GetCurrentMode()
        {
            lock (_stateLock)
            {
                return _currentMode;
            }
        }

        /// <summary>
        /// 检查系统是否处于自动模式
        /// </summary>
        /// <returns>是否为自动模式</returns>
        public bool IsAutoMode()
        {
            return _currentMode == SystemMode.Auto;
        }

        /// <summary>
        /// 检查系统是否处于手动模式
        /// </summary>
        /// <returns>是否为手动模式</returns>
        public bool IsManualMode()
        {
            return _currentMode == SystemMode.Manual;
        }

        #region 系统启用控制方法

        /// <summary>
        /// 启用系统 - 允许系统响应信号和执行流程
        /// </summary>
        public void EnableSystem()
        {
            lock (_stateLock)
            {
                if (!_systemEnabled)
                {
                    _systemEnabled = true;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "系统已启用");
                }
            }
        }

        /// <summary>
        /// 禁用系统 - 停止响应信号，但不影响当前正在执行的流程
        /// </summary>
        public void DisableSystem()
        {
            lock (_stateLock)
            {
                if (_systemEnabled)
                {
                    _systemEnabled = false;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统已禁用");
                }
            }
        }

        /// <summary>
        /// 获取系统启用状态
        /// </summary>
        /// <returns>系统是否已启用</returns>
        public bool IsSystemEnabled()
        {
            lock (_stateLock)
            {
                return _systemEnabled;
            }
        }

        /// <summary>
        /// 检查启用信号并自动启用/禁用系统
        /// </summary>
        private void CheckEnableSignal()
        {
            try
            {
                // TODO: 检查外部启用信号
                // bool enableSignal = IOManager.GetInput(AutoProductionIO.SystemEnable);
                // if (enableSignal && !_systemEnabled)
                // {
                //     EnableSystem();
                // }
                // else if (!enableSignal && _systemEnabled)
                // {
                //     DisableSystem();
                // }
                
                // 暂时可以通过其他方式控制启用状态，比如UI或配置
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "检查启用信号: " + ex.Message);
            }
        }

        #endregion

        #region 手动模式控制方法

        /// <summary>
        /// 手动启动扫码流程 - 手动模式下使用
        /// </summary>
        /// <returns>是否成功启动</returns>
        public bool ManualStartScanProcess()
        {
            lock (_stateLock)
            {
                if (!_systemEnabled)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "系统未启用，无法启动扫码流程");
                    return false;
                }

                if (_currentMode != SystemMode.Manual)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "非手动模式，无法手动启动扫码流程");
                    return false;
                }

                if (IsRobotBusy())
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "机械手忙碌，无法启动扫码流程");
                    return false;
                }

                _currentState = AutoProductionState.WaitingForScan;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "手动启动扫码流程");
                return true;
            }
        }

        /// <summary>
        /// 手动启动小车取料流程 - 手动模式下使用
        /// </summary>
        /// <returns>是否成功启动</returns>
        public bool ManualStartCartProcess()
        {
            lock (_stateLock)
            {
                if (!_systemEnabled)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "系统未启用，无法启动小车取料");
                    return false;
                }

                if (_currentMode != SystemMode.Manual)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "非手动模式，无法手动启动小车取料");
                    return false;
                }

                if (IsRobotBusy())
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "机械手忙碌，无法启动小车取料");
                    return false;
                }

                _currentState = AutoProductionState.ProcessingCartRequest;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "手动启动小车取料流程");
                return true;
            }
        }

        /// <summary>
        /// 手动停止当前流程 - 手动模式下使用
        /// </summary>
        /// <returns>是否成功停止</returns>
        public bool ManualStopCurrentProcess()
        {
            lock (_stateLock)
            {
                if (_currentMode != SystemMode.Manual)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.OperationFailed, "非手动模式，无法手动停止流程");
                    return false;
                }

                if (_currentState == AutoProductionState.Idle)
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统已处于空闲状态");
                    return true;
                }

                _currentState = AutoProductionState.Idle;
                _pauseRequested = false;
                _stopRequested = false;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "手动停止流程");
                return true;
            }
        }

        #endregion

        #region 安全消息处理

        /// <summary>
        /// 处理安全报警消息
        /// </summary>
        /// <param name="msg">安全报警消息</param>
        private void OnSafetyAlertReceived(MessageModel msg)
        {
            try
            {
                if (msg.Data is SafetyAlert safetyAlert)
                {
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ErrorOccurred, 
                        "安全报警: " + safetyAlert.Description);

                    // 根据报警级别执行相应操作
                    switch (safetyAlert.AlertLevel)
                    {
                        case SafetyAlertLevel.Critical:
                            // 危险级别 - 立即急停
                            HandleCriticalSafetyAlert(safetyAlert);
                            break;
                            
                        case SafetyAlertLevel.Alarm:
                            // 报警级别 - 停止流程但允许完成当前步骤
                            HandleAlarmSafetyAlert(safetyAlert);
                            break;
                            
                        case SafetyAlertLevel.Warning:
                            // 警告级别 - 记录日志继续运行
                            HandleWarningSafetyAlert(safetyAlert);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "处理安全报警消息", ex.Message);
            }
        }

        /// <summary>
        /// 处理危险级别的安全报警
        /// </summary>
        /// <param name="safetyAlert">安全报警</param>
        private void HandleCriticalSafetyAlert(SafetyAlert safetyAlert)
        {
            lock (_stateLock)
            {
                // 立即执行软件急停
                if (!_emergencyStopTriggered)
                {
                    _emergencyStopTriggered = true;
                    _currentState = AutoProductionState.Error;
                    
                    _uiLogger.Error(() => Ewan.Resources.LogMessages.ErrorOccurred, 
                        "安全系统触发急停: " + safetyAlert.Description);
                    
                    // 执行软件停止操作
                    ExecuteSoftwareStop();
                }
            }
        }

        /// <summary>
        /// 处理报警级别的安全报警
        /// </summary>
        /// <param name="safetyAlert">安全报警</param>
        private void HandleAlarmSafetyAlert(SafetyAlert safetyAlert)
        {
            // 停止流程但允许当前步骤完成
            _stopRequested = true;
            _uiLogger.Error(() => Ewan.Resources.LogMessages.WarningMessage, 
                "安全报警触发停止流程: " + safetyAlert.Description);
        }

        /// <summary>
        /// 处理警告级别的安全报警
        /// </summary>
        /// <param name="safetyAlert">安全报警</param>
        private void HandleWarningSafetyAlert(SafetyAlert safetyAlert)
        {
            // 只记录日志，不影响流程运行
            _uiLogger.Error(() => Ewan.Resources.LogMessages.WarningMessage, 
                "安全警告: " + safetyAlert.Description);
        }

        #endregion

        #endregion
    }
}