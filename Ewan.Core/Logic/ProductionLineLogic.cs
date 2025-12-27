using Ewan.Core.IO;
using Ewan.Core.Module;
using Ewan.Model.Messages;
using Ewan.Model.System;
using EwanCommon.Logging;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;

namespace Ewan.Core.Logic
{
    /// <summary>
    /// 生产线主流程逻辑状态机
    /// 管理物料装载和卸载的协调工作
    /// 基于 LogicBase + SwitchIndex 模式实现
    /// </summary>
    /// <remarks>
    /// 状态流程：
    /// 初始状态 → 等待初始化命令 → 执行硬件初始化 → 等待启动命令 → 生产运行中
    /// → (暂停/恢复) → 停止/紧急停止 → 结束状态
    /// </remarks>
    public class ProductionLineLogic : LogicBase
    {
        #region 私有字段

        private readonly UILogger _uiLogger = new UILogger();
        private readonly ProductionLineSharedState _sharedState;
        private readonly SystemParametersManager _parametersManager;

        private MaterialLoadingLogic _loadingLogic;
        private MaterialUnloadingLogic _unloadingLogic;
        private IBinElevator _binElevator;
        private LayeredIOManager _ioManager;

        private bool _initialized = false;
        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _loadingEnabled = true;
        private bool _unloadingEnabled = true;

        private IDisposable _systemControlSubscription;

        // 脉冲宽度配置
        private const int RECOVERY_PULSE_WIDTH_MS = 200;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public ProductionLineLogic() : this(new ProductionLineSharedState())
        {
        }

        public ProductionLineLogic(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
            _parametersManager = SystemParametersManager.Instance;
            _ioManager = LayeredIOManager.Instance();

            // 创建子逻辑
            _loadingLogic = new MaterialLoadingLogic(_sharedState);
            _unloadingLogic = new MaterialUnloadingLogic(_sharedState);

            // 读取初始配置
            var parameters = _parametersManager?.Parameters;
            if (parameters != null)
            {
                _loadingEnabled = parameters.EnableLoadingModule;
                _unloadingEnabled = parameters.EnableUnloadingModule;
            }

            // 订阅系统控制消息
            _systemControlSubscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControlMessage);
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取共享状态
        /// </summary>
        public ProductionLineSharedState SharedState => _sharedState;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 是否已暂停
        /// </summary>
        public bool IsPaused => _isPaused;

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置料仓升降模块
        /// </summary>
        public void SetBinElevatorModule(IBinElevator binElevator)
        {
            _binElevator = binElevator;
            _unloadingLogic?.SetBinElevatorModule(binElevator);
        }

        /// <summary>
        /// 销毁资源
        /// </summary>
        public void Dispose()
        {
            _systemControlSubscription?.Dispose();
            _systemControlSubscription = null;
            _unloadingLogic?.Dispose();
        }

        #endregion

        #region LogicBase 实现

        /// <summary>
        /// 状态机处理器
        /// </summary>
        public override void Handler()
        {
            switch (SwitchIndex)
            {
                case "初始状态":
                    ProcessInitialState();
                    break;

                case "等待初始化命令":
                    // 等待外部发送初始化命令
                    break;

                case "执行硬件初始化":
                    ProcessHardwareInitialization();
                    break;

                case "等待启动命令":
                    // 等待外部发送启动命令
                    break;

                case "生产运行中":
                    ProcessProduction();
                    break;

                case "暂停中":
                    // 暂停状态，等待恢复命令
                    break;

                case "停止中":
                    ProcessStopping();
                    break;

                case "紧急停止":
                    ProcessEmergencyStop();
                    break;

                case "结束状态":
                    // 完成
                    break;
            }
        }

        /// <summary>
        /// 复位状态机
        /// </summary>
        public override void Rset()
        {
            _initialized = false;
            _isRunning = false;
            _isPaused = false;
            _loadingLogic?.Rset();
            _unloadingLogic?.Rset();
            _sharedState?.ResetAllStates();
            base.Rset();
        }

        #endregion

        #region 状态处理方法

        /// <summary>
        /// 处理初始状态
        /// </summary>
        private void ProcessInitialState()
        {
            _uiLogger.InfoRaw("状态机启动: {0}", "ProductionLineLogic");
            SendStatusMessage(SystemStatus.Standby, "生产线待机中");
            SwitchIndex = "等待初始化命令";
        }

        /// <summary>
        /// 执行硬件初始化
        /// </summary>
        private void ProcessHardwareInitialization()
        {
            try
            {
                _uiLogger.InfoRaw("处理已开始: {0}", "生产线硬件初始化开始");
                SendStatusMessage(SystemStatus.Initializing, "生产线硬件初始化中");

                // 清除暂停状态
                _isPaused = false;
                _sharedState?.SetSystemPaused(false);
                _sharedState?.ResetAllStates();

                // 执行硬件初始化序列
                PerformHardwareInit();

                _initialized = true;
                _uiLogger.InfoRaw("处理已完成: {0}", "生产线硬件初始化完成");
                SendStatusMessage(SystemStatus.Standby, "生产线初始化完成，等待启动");

                SwitchIndex = "等待启动命令";
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "生产线硬件初始化", ex.Message);
                _initialized = false;
                SendStatusMessage(SystemStatus.Critical, "生产线初始化失败: " + ex.Message, true);
                SwitchIndex = "等待初始化命令";
            }
        }

        /// <summary>
        /// 处理生产运行
        /// </summary>
        private void ProcessProduction()
        {
            // 刷新模块配置
            RefreshModuleConfiguration();

            // 运行子逻辑
            if (_loadingEnabled)
            {
                _loadingLogic?.Handler();

                // 如果装载完成，复位以便下一个周期
                if (_loadingLogic?.IsFinish == true)
                {
                    _loadingLogic.Rset();
                }
            }

            if (_unloadingEnabled)
            {
                _unloadingLogic?.Handler();

                // 如果卸载完成，复位以便下一个周期
                if (_unloadingLogic?.IsFinish == true)
                {
                    _unloadingLogic.Rset();
                }
            }

            // 运行料仓升降逻辑（如果有）
            // _binElevator?.Run();
        }

        /// <summary>
        /// 处理停止
        /// </summary>
        private void ProcessStopping()
        {
            _isRunning = false;
            _initialized = false;
            _isPaused = false;

            _sharedState?.SetSystemPaused(false);
            _sharedState?.ResetAllStates();

            // 强制停止子模块
            ForceStopAllSubLogics();

            _uiLogger.InfoRaw("处理已完成: {0}", "生产线停止");
            SendStatusMessage(SystemStatus.Stopped, "生产线已停止");

            SwitchIndex = "等待初始化命令";
        }

        /// <summary>
        /// 处理紧急停止
        /// </summary>
        private void ProcessEmergencyStop()
        {
            _isRunning = false;
            _initialized = false;
            _isPaused = false;

            _sharedState?.SetSystemPaused(false);
            _sharedState?.ResetAllStates();

            // 强制停止子模块
            ForceStopAllSubLogics();

            _uiLogger.InfoRaw("处理已完成: {0}", "生产线紧急停止");
            SendStatusMessage(SystemStatus.Critical, "生产线紧急停止", true);

            SwitchIndex = "等待初始化命令";
        }

        #endregion

        #region 命令处理

        /// <summary>
        /// 处理系统控制消息
        /// </summary>
        private void OnSystemControlMessage(SystemControlMessage message)
        {
            try
            {
                switch (message.Command)
                {
                    case SystemControlCommand.Initialize:
                        HandleInitializeCommand();
                        break;

                    case SystemControlCommand.Start:
                        HandleStartCommand();
                        break;

                    case SystemControlCommand.Stop:
                        HandleStopCommand();
                        break;

                    case SystemControlCommand.EmergencyStop:
                        HandleEmergencyStopCommand();
                        break;

                    case SystemControlCommand.Pause:
                        HandlePauseCommand();
                        break;

                    case SystemControlCommand.Resume:
                        HandleResumeCommand();
                        break;

                    default:
                        _uiLogger.WarnRaw("处理错误: {0} - {1}",
                            "未知系统控制命令", message.Command.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}",
                    "处理系统控制消息", ex.Message);
            }
        }

        /// <summary>
        /// 处理初始化命令
        /// </summary>
        private void HandleInitializeCommand()
        {
            if (SwitchIndex == "等待初始化命令" || SwitchIndex == "初始状态")
            {
                SwitchIndex = "执行硬件初始化";
            }
            else
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}",
                    "初始化命令", $"当前状态 {SwitchIndex} 不允许初始化");
            }
        }

        /// <summary>
        /// 处理启动命令
        /// </summary>
        private void HandleStartCommand()
        {
            if (!_initialized)
            {
                _uiLogger.WarnRaw("处理错误: {0} - {1}",
                    "启动命令", "生产线未初始化，请先执行硬件初始化");
                SendStatusMessage(SystemStatus.Warning, "生产线未初始化，无法启动");
                return;
            }

            if (SwitchIndex == "等待启动命令" || SwitchIndex == "暂停中")
            {
                bool wasPaused = _isPaused;

                _isPaused = false;
                _sharedState?.SetSystemPaused(false);

                if (wasPaused)
                {
                    SendRecoveryPulse();
                }

                _isRunning = true;
                SwitchIndex = "生产运行中";
                _uiLogger.InfoRaw("处理已完成: {0}", "生产线启动");
                SendStatusMessage(SystemStatus.Running, "生产线运行中");
            }
        }

        /// <summary>
        /// 处理停止命令
        /// </summary>
        private void HandleStopCommand()
        {
            if (SwitchIndex == "生产运行中" || SwitchIndex == "暂停中")
            {
                SwitchIndex = "停止中";
            }
        }

        /// <summary>
        /// 处理紧急停止命令
        /// </summary>
        private void HandleEmergencyStopCommand()
        {
            SwitchIndex = "紧急停止";
        }

        /// <summary>
        /// 处理暂停命令
        /// </summary>
        private void HandlePauseCommand()
        {
            if (SwitchIndex == "生产运行中")
            {
                _isPaused = true;
                _sharedState?.SetSystemPaused(true);

                SwitchIndex = "暂停中";
                _uiLogger.InfoRaw("处理已完成: {0}", "生产线暂停");
                SendStatusMessage(SystemStatus.Paused, "生产线已暂停");
            }
        }

        /// <summary>
        /// 处理恢复命令
        /// </summary>
        private void HandleResumeCommand()
        {
            if (SwitchIndex == "暂停中")
            {
                _isPaused = false;
                _sharedState?.SetSystemPaused(false);
                _sharedState?.SetRequireReinit(true);

                SwitchIndex = "生产运行中";
                _uiLogger.InfoRaw("处理已完成: {0}", "生产线恢复，料仓重新初始化");
                SendStatusMessage(SystemStatus.Running, "生产线已恢复运行");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 执行硬件初始化
        /// </summary>
        private void PerformHardwareInit()
        {
            if (_ioManager?.Ctx == null)
            {
                _ioManager = LayeredIOManager.Instance();
            }

            // 上料机初始化序列
            _ioManager.Ctx.On(x => x.停止输出);
            System.Threading.Thread.Sleep(500);
            _ioManager.Ctx.Off(x => x.停止输出);
            System.Threading.Thread.Sleep(500);

            _ioManager.Ctx.On(x => x.开始);
            System.Threading.Thread.Sleep(500);
            _ioManager.Ctx.Off(x => x.开始);

            _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);
        }

        /// <summary>
        /// 发送复原脉冲
        /// </summary>
        private void SendRecoveryPulse()
        {
            try
            {
                if (_ioManager?.Ctx == null)
                {
                    _ioManager = LayeredIOManager.Instance();
                }

                _ioManager?.Ctx?.Pulse(x => x.复位, RECOVERY_PULSE_WIDTH_MS);
                _uiLogger.InfoRaw("处理已完成: {0}", "发送复原脉冲");
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "发送复原脉冲", ex.Message);
            }
        }

        /// <summary>
        /// 刷新模块配置
        /// </summary>
        private void RefreshModuleConfiguration()
        {
            var parameters = _parametersManager?.Parameters;
            if (parameters == null) return;

            if (parameters.EnableLoadingModule != _loadingEnabled)
            {
                _loadingEnabled = parameters.EnableLoadingModule;
                if (!_loadingEnabled)
                {
                    _loadingLogic?.Rset();
                }
            }

            if (parameters.EnableUnloadingModule != _unloadingEnabled)
            {
                _unloadingEnabled = parameters.EnableUnloadingModule;
                if (!_unloadingEnabled)
                {
                    _unloadingLogic?.Rset();
                }
            }
        }

        /// <summary>
        /// 强制停止所有子逻辑
        /// </summary>
        private void ForceStopAllSubLogics()
        {
            _loadingLogic?.Rset();
            _unloadingLogic?.Rset();

            // 清除所有IO信号
            try
            {
                if (_ioManager?.Ctx != null)
                {
                    _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);
                    _ioManager.Ctx.Off(x => x.发送取料指令);
                    _ioManager.Ctx.Off(x => x.触发机械手放置料仓);
                    _ioManager.Ctx.Off(x => x.发送扫码完成信号);
                    _ioManager.Ctx.Off(x => x.发送放入小车指令);
                    _ioManager.Ctx.Off(x => x.料仓1选择信号);
                    _ioManager.Ctx.Off(x => x.料仓2选择信号);
                    _ioManager.Ctx.Off(x => x.料仓3选择信号);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "强制停止清除IO", ex.Message);
            }
        }

        /// <summary>
        /// 发送系统状态消息
        /// </summary>
        private void SendStatusMessage(SystemStatus status, string description, bool isCritical = false)
        {
            try
            {
                var command = new StatusIndicatorCommand(status, description, isCritical);
                MessageHub.Current.Post(command);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "发送状态消息", ex.Message);
            }
        }

        #endregion
    }
}
