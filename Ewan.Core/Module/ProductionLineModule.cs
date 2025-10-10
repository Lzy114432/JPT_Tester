using System;
using System.Threading;
using Ewan.Core.Msg;
using Ewan.Model.System;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 生产线统一控制模块
    /// 管理物料装载和料仓升降的协调工作
    /// </summary>
    public class ProductionLineModule : BaseModule<ProductionLineModule>
    {
        #region 私有字段

        private MaterialLoadingModule _materialLoading;
        private BinElevatorModule _binElevator;
        private ProductionLineSharedState _sharedState;
        
        private int _scanInterval = 100; // 扫描间隔，毫秒
        private bool _systemReady = false;
        private bool _initialized = false; // 硬件初始化标志
        private bool _isRunning = false; // 控制OnRun是否进行循环的变量
        private bool _isPaused = false; // 暂停状态标志
        private readonly object _stateLock = new object();
        private MsgListener _systemControlListener; // 系统控制消息监听器

        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "ProductionLineModule");
                
                // 注册系统控制消息监听器
                RegisterSystemControlListener();
                
                // 创建共享状态
                _sharedState = new ProductionLineSharedState();
                
                // 创建子模块并传递共享状态
                _materialLoading = new MaterialLoadingModule(_sharedState);
                _binElevator = new BinElevatorModule(_sharedState);
                
                // 初始化子模块
                _materialLoading.Init();
                _binElevator.Init();
                
                _systemReady = true;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "生产线控制系统");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, 
                    "ProductionLineModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            if (!_systemReady || !_isRunning) return true;
            
            try
            {
                Thread.Sleep(_scanInterval);

                // 顺序调用子模块，让它们各自处理状态机
                // 通过共享状态自动协调工作
                // 添加空值检查，防止关闭时的空引用异常
                if (_materialLoading != null)
                    _materialLoading.Run();
                    
                if (_binElevator != null)
                    _binElevator.Run();
                
                // 添加必要的延时
               
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "ProductionLineModule", ex.Message);
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                // 停止运行
                _isRunning = false;
                
                // 取消注册消息监听器
                UnregisterSystemControlListener();
                
                if (_materialLoading != null)
                {
                    _materialLoading.Destroy();
                    _materialLoading = null;
                }
                
                if (_binElevator != null)
                {
                    _binElevator.Destroy();
                    _binElevator = null;
                }
                
                _systemReady = false;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "ProductionLineModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "ProductionLineModule销毁", ex.Message);
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 执行硬件初始化（独立命令）
        /// </summary>
        public void PerformHardwareInitialization()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "生产线硬件初始化开始");

                // 发送初始化中状态
                SendStatusMessage(SystemStatus.Initializing, "生产线硬件初始化中");

                _materialLoading?.PerformHardwareInitialization();
                _binElevator?.PerformHardwareInitialization();

                _initialized = true;
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线硬件初始化完成");

                // 初始化完成后进入待机状态
                SendStatusMessage(SystemStatus.Standby, "生产线初始化完成，等待启动");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "生产线硬件初始化", ex.Message);
                _initialized = false;

                // 初始化失败，发送严重故障状态
                SendStatusMessage(SystemStatus.Critical, "生产线初始化失败: " + ex.Message, true);
            }
        }

        /// <summary>
        /// 启动生产线运行（独立命令）
        /// </summary>
        public void StartProduction()
        {
            if (!_initialized)
            {
                _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError,
                    "生产线未初始化", "请先执行硬件初始化");
                SendStatusMessage(SystemStatus.Warning, "生产线未初始化，无法启动");
                return;
            }

            _isRunning = true;
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线启动");

            // 发送运行状态
            SendStatusMessage(SystemStatus.Running, "生产线运行中");
        }

        /// <summary>
        /// 停止生产线运行
        /// </summary>
        public void StopProduction()
        {
            _isRunning = false;
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线停止");

            // 发送停止状态
            SendStatusMessage(SystemStatus.Stopped, "生产线已停止");
        }

        /// <summary>
        /// 紧急停止所有流程
        /// </summary>
        public void EmergencyStop()
        {
            try
            {
                _isRunning = false;
                _materialLoading?.ForceStopLoading();
                // 如果BinElevatorModule有紧急停止方法，可以在这里调用

                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线紧急停止");

                // 发送严重报警状态
                SendStatusMessage(SystemStatus.Critical, "生产线紧急停止", true);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "生产线紧急停止", ex.Message);

                // 发送严重故障状态
                SendStatusMessage(SystemStatus.Critical, "紧急停止失败: " + ex.Message, true);
            }
        }

        /// <summary>
        /// 暂停生产线运行
        /// </summary>
        public void PauseProduction()
        {
            try
            {
                lock (_stateLock)
                {
                    _isPaused = true;
                    _sharedState?.SetSystemPaused(true);

                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线暂停");

                    // 发送暂停状态
                    SendStatusMessage(SystemStatus.Paused, "生产线已暂停");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "生产线暂停", ex.Message);
            }
        }

        /// <summary>
        /// 恢复生产线运行
        /// </summary>
        public void ResumeProduction()
        {
            try
            {
                lock (_stateLock)
                {
                    _isPaused = false;
                    _sharedState?.SetSystemPaused(false);

                    // 触发BinElevator重新初始化
                    _sharedState?.SetRequireReinit(true);

                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "生产线恢复，料仓重新初始化");

                    // 发送运行状态
                    SendStatusMessage(SystemStatus.Running, "生产线已恢复运行");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError,
                    "生产线恢复", ex.Message);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 发送系统状态消息
        /// </summary>
        private void SendStatusMessage(SystemStatus status, string description, bool isCritical = false)
        {
            try
            {
                var command = new StatusIndicatorCommand(status, description, isCritical);
                var message = new MessageModel(MsgSubject.StatusIndicator, command);

                MsgManager.Instance().PushMsg(message);

                _uiLogger.Debug(() => $"发送系统状态消息: {status} - {description}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError,
                    "ProductionLineModule-SendStatusMessage", ex.Message);
            }
        }

        /// <summary>
        /// 注册系统控制消息监听器
        /// </summary>
        private void RegisterSystemControlListener()
        {
            try
            {
                _systemControlListener = new MsgListener(MsgSubject.SystemControl, OnSystemControlMessage);
                MsgManager.Instance().RegisterListener(_systemControlListener);
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统控制消息监听器注册");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "系统控制消息监听器注册", ex.Message);
            }
        }

        /// <summary>
        /// 取消注册系统控制消息监听器
        /// </summary>
        private void UnregisterSystemControlListener()
        {
            try
            {
                if (_systemControlListener != null)
                {
                    MsgManager.Instance().UnRegisterListener(_systemControlListener);
                    _systemControlListener = null;
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统控制消息监听器取消注册");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "系统控制消息监听器取消注册", ex.Message);
            }
        }

        /// <summary>
        /// 处理系统控制消息
        /// </summary>
        /// <param name="message">消息模型</param>
        private void OnSystemControlMessage(MessageModel message)
        {
            try
            {
                if (message.Data is SystemControlCommand command)
                {
                    switch (command)
                    {
                        case SystemControlCommand.Initialize:
                            PerformHardwareInitialization();
                            break;
                        case SystemControlCommand.Start:
                            StartProduction();
                            break;
                        case SystemControlCommand.Stop:
                            StopProduction();
                            break;
                        case SystemControlCommand.EmergencyStop:
                            EmergencyStop();
                            break;
                        case SystemControlCommand.Pause:
                            PauseProduction();
                            break;
                        case SystemControlCommand.Resume:
                            ResumeProduction();
                            break;
                        default:
                            _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError,
                                "未知系统控制命令", command.ToString());
                            break;
                    }
                }
                else
                {
                    _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingError, 
                        "系统控制消息数据类型错误", message.Data?.GetType().Name ?? "null");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "处理系统控制消息", ex.Message);
            }
        }

        #endregion
    }
}
