using Ewan.Core;
using Ewan.Core.Logic;
using Ewan.Core.Module;
using Ewan.Model.Messages;
using Ewan.Model.System;
using EwanCommon.Logging;
using EwanCore.AlarmSystem;
using EwanCore.Attribute;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using log4net;
using System;
using System.Threading;

namespace Ewan.Core.Manager
{
    /// <summary>
    /// 逻辑管理器：作为逻辑队列/控制器的统一入口（参考 ScribingV3 LogicManager）。
    /// - 直接控制 LogicThread/ControllerBox（不依赖消息驱动启动）
    /// - 兼容 MessageHub：订阅 AlarmMessage/SystemControlMessage
    /// </summary>
    [Manager(Priority = 5)]
    public class LogicManager : BaseManager<LogicManager>
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(LogicManager));

        private readonly object _controlLock = new object();
        private readonly AlarmService _alarmService = new AlarmService();

        private LogicThread _logicThread;
        private ControllerBox _controllerBox;

        private ProductionLineSharedState _sharedState;
        private IBinElevator _binElevator;

        private IDisposable _alarmMessageSubscription;
        private IDisposable _systemControlSubscription;

        private Thread _binElevatorThread;
        private volatile bool _binElevatorRunning;

        private MainLogic _mainLogic;
        private bool _disposed;

        public IAlarmService Alarms => _alarmService;

        public RunTimeTag RunState => _logicThread?.RunTag ?? RunTimeTag.Stop;

        public string CurrentLogicState => _logicThread?.CurLogicStateStr ?? string.Empty;

        public bool HasAlarm => _alarmService.HasAlarm;

        public bool HasNeedResetAlarm => _alarmService.HasNeedResetAlarm;

        public ProductionLineSharedState SharedState => _sharedState;

        public IBinElevator BinElevator => _binElevator;

        public override bool Init()
        {
            lock (_controlLock)
            {
                if (_logicThread != null)
                {
                    return true;
                }

                _logicThread = new LogicThread();
                _logicThread.LogicException += OnLogicException;

                _controllerBox = new ControllerBox();
                _controllerBox.AddLogicManger(_logicThread);

                _sharedState = new ProductionLineSharedState();

                var binElevatorModule = new BinElevatorModule(_sharedState);
                binElevatorModule.Init();
                _binElevator = binElevatorModule;

                _alarmMessageSubscription = MessageHub.Current.Subscribe<AlarmMessage>(OnAlarmMessage);
                _systemControlSubscription = MessageHub.Current.Subscribe<SystemControlMessage>(OnSystemControlMessage);

                MachineParameters.Instance.MarkNeedHome();

                s_logger.Info("LogicManager 初始化完成");
                return base.Init();
            }
        }

        public void Home()
        {
            HomeInternal(publishSystemControl: true);
        }

        public bool Start()
        {
            return StartInternal(publishSystemControl: true);
        }

        public void Stop()
        {
            StopInternal(publishSystemControl: true);
        }

        public void Pause()
        {
            PauseInternal(publishSystemControl: true);
        }

        public void Resume()
        {
            ResumeInternal(publishSystemControl: true);
        }

        public void EmergencyStop(string reason = null)
        {
            EmergencyStopInternal(publishSystemControl: true, reason: reason);
        }

        public void ClearAlarm()
        {
            _alarmService.Clear();
            MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Standby, "报警已清除，待机"));
        }

        public string GetCurLogicState()
        {
            return CurrentLogicState;
        }

        public bool IsRunning()
        {
            return RunState == RunTimeTag.Run;
        }

        public void SetStep()
        {
            lock (_controlLock)
            {
                _controllerBox?.Step();
            }
        }

        public void AddDebugLogic(LogicBase logic)
        {
            if (logic == null) throw new ArgumentNullException(nameof(logic));

            lock (_controlLock)
            {
                _logicThread?.AddAction(logic);
            }
        }

        private void HomeInternal(bool publishSystemControl)
        {
            lock (_controlLock)
            {
                if (_logicThread == null)
                {
                    _uiLogger.WarnRaw("LogicManager 未初始化，无法复位");
                    return;
                }

                if (MachineParameters.Instance.IsHomeing || _logicThread.ExistAction(typeof(HomeLogic)))
                {
                    _uiLogger.WarnRaw("复位中，请勿重复点击");
                    return;
                }

                // 先停止，再设置复位状态（避免 StopInternal 重置 IsHomeing）
                StopInternal(publishSystemControl: false);
                MachineParameters.Instance.BeginHome();
                Thread.Sleep(500);

                DisposeMainLogicIfNeeded();
                _logicThread.ClearAction();

                _sharedState?.ResetAllStates();
                MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.ForceStopAll(nameof(LogicManager)));
                MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.InitializeAll(nameof(LogicManager)));

                StartBinElevatorThread();

                _logicThread.AddAction(new HomeLogic(_binElevator));
                _controllerBox.Start();

                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Initializing, "复位中"));
                if (publishSystemControl)
                {
                    MessageHub.Current.Post(SystemControlMessage.Initialize(nameof(LogicManager), "LogicManager.Home"));
                }

                s_logger.Info("复位开始");
            }
        }

        private bool StartInternal(bool publishSystemControl)
        {
            lock (_controlLock)
            {
                if (_logicThread == null)
                {
                    _uiLogger.WarnRaw("LogicManager 未初始化，无法启动");
                    return false;
                }

                if (MachineParameters.Instance.IsHomeing)
                {
                    _uiLogger.WarnRaw("复位中，请等待复位完成后再启动");
                    return false;
                }

                if (MachineParameters.Instance.NeedHome)
                {
                    _uiLogger.WarnRaw("需要先复位，才能启动");
                    return false;
                }

                if (HasAlarm)
                {
                    _uiLogger.WarnRaw("存在报警，无法启动");
                    MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Alarm, "存在报警，无法启动"));
                    return false;
                }

                StartBinElevatorThread();

                if (_logicThread.Count == 0)
                {
                    _mainLogic = new MainLogic(_sharedState, _binElevator);
                    _logicThread.AddAction(_mainLogic);
                }

                _controllerBox.Start();

                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Running, "运行中"));
                if (publishSystemControl)
                {
                    MessageHub.Current.Post(SystemControlMessage.Start(nameof(LogicManager), "LogicManager.Start"));
                }

                return true;
            }
        }

        private void StopInternal(bool publishSystemControl)
        {
            lock (_controlLock)
            {
                _controllerBox?.Stop();
                StopBinElevatorThread();

                _sharedState?.SetSystemPaused(false);
                _sharedState?.ResetAllStates();
                MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.ForceStopAll(nameof(LogicManager)));

                MachineParameters.Instance.MarkNeedHome();

                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Stopped, "已停止（需复位）"));
                if (publishSystemControl)
                {
                    MessageHub.Current.Post(SystemControlMessage.Stop(nameof(LogicManager), "LogicManager.Stop"));
                }

                s_logger.Info("已停止");
            }
        }

        private void PauseInternal(bool publishSystemControl)
        {
            lock (_controlLock)
            {
                _sharedState?.SetSystemPaused(true);
                _controllerBox?.Pause();

                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Paused, "暂停中"));
                if (publishSystemControl)
                {
                    MessageHub.Current.Post(SystemControlMessage.Pause(nameof(LogicManager), "LogicManager.Pause"));
                }
            }
        }

        private void ResumeInternal(bool publishSystemControl)
        {
            lock (_controlLock)
            {
                _sharedState?.SetSystemPaused(false);
                _controllerBox?.Start();

                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Running, "运行中"));
                if (publishSystemControl)
                {
                    MessageHub.Current.Post(SystemControlMessage.Resume(nameof(LogicManager), "LogicManager.Resume"));
                }
            }
        }

        private void EmergencyStopInternal(bool publishSystemControl, string reason)
        {
            lock (_controlLock)
            {
                StopInternal(publishSystemControl: false);

                var msg = string.IsNullOrWhiteSpace(reason) ? "紧急停止" : "紧急停止: " + reason;
                _alarmService.AddAlarm(msg, AlarmLevel.H, unit: "System", needReset: true, key: "System.EmergencyStop");
                MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Critical, msg, isCritical: true));

                if (publishSystemControl)
                {
                    MessageHub.Current.Post(SystemControlMessage.EmergencyStop(nameof(LogicManager), reason));
                }

                s_logger.Warn("紧急停止");
            }
        }

        private void StartBinElevatorThread()
        {
            if (_binElevator == null)
            {
                return;
            }

            if (_binElevatorRunning && _binElevatorThread?.IsAlive == true)
            {
                return;
            }

            _binElevatorRunning = true;
            _binElevatorThread = new Thread(BinElevatorPollingLoop)
            {
                IsBackground = true,
                Name = "BinElevatorPolling"
            };
            _binElevatorThread.Start();
        }

        private void StopBinElevatorThread()
        {
            _binElevatorRunning = false;
            if (_binElevatorThread != null && _binElevatorThread.IsAlive)
            {
                _binElevatorThread.Join(1000);
            }
            _binElevatorThread = null;
        }

        private void BinElevatorPollingLoop()
        {
            while (_binElevatorRunning)
            {
                try
                {
                    _binElevator.Run();
                }
                catch (Exception ex)
                {
                    s_logger.Error("BinElevator 轮询异常", ex);
                    _alarmService.AddAlarm(
                        content: $"料仓升降异常：{ex.Message}",
                        level: AlarmLevel.H,
                        unit: "BinElevator",
                        needReset: true,
                        key: "BinElevator.Exception");
                    Thread.Sleep(1000);
                }
            }
        }

        private void DisposeMainLogicIfNeeded()
        {
            try
            {
                _mainLogic?.Dispose();
            }
            catch (Exception ex)
            {
                s_logger.Warn("释放 MainLogic 资源时异常", ex);
            }
            finally
            {
                _mainLogic = null;
            }
        }

        private void OnLogicException(object sender, LogicExceptionEventArgs e)
        {
            var content = $"流程异常：{e.LogicName}/{e.Step} - {e.Exception?.Message}";
            _alarmService.AddAlarm(
                content: content,
                level: AlarmLevel.H,
                unit: "Logic",
                needReset: true,
                key: "Logic.Exception");

            StopInternal(publishSystemControl: false);

            MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Critical, content, isCritical: true));
            s_logger.Error(content, e.Exception);
        }

        private void OnAlarmMessage(AlarmMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return;
            }

            _alarmService.AddAlarm(
                content: message.Content,
                level: message.Level,
                unit: message.Unit,
                needReset: message.NeedReset,
                key: message.Key);
        }

        private void OnSystemControlMessage(SystemControlMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (string.Equals(message.Source, nameof(LogicManager), StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                switch (message.Command)
                {
                    case SystemControlCommand.Initialize:
                        HomeInternal(publishSystemControl: false);
                        break;
                    case SystemControlCommand.Start:
                        StartInternal(publishSystemControl: false);
                        break;
                    case SystemControlCommand.Stop:
                        StopInternal(publishSystemControl: false);
                        break;
                    case SystemControlCommand.Pause:
                        PauseInternal(publishSystemControl: false);
                        break;
                    case SystemControlCommand.Resume:
                        ResumeInternal(publishSystemControl: false);
                        break;
                    case SystemControlCommand.EmergencyStop:
                        EmergencyStopInternal(publishSystemControl: false, reason: message.Reason);
                        break;
                }
            }
            catch (Exception ex)
            {
                s_logger.Error("处理 SystemControlMessage 异常", ex);
            }
        }

        public override void Destroy()
        {
            lock (_controlLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                try
                {
                    StopInternal(publishSystemControl: false);
                }
                catch
                {
                    // ignored
                }

                StopBinElevatorThread();

                _logicThread?.Dispose();
                _logicThread = null;

                _alarmMessageSubscription?.Dispose();
                _alarmMessageSubscription = null;

                _systemControlSubscription?.Dispose();
                _systemControlSubscription = null;

                _binElevator?.Destroy();
                _binElevator = null;

                DisposeMainLogicIfNeeded();

                s_logger.Info("LogicManager 已销毁");
                base.Destroy();
            }
        }
    }
}
