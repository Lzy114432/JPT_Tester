using Ewan.Core.Logic;
using Ewan.Core.Module;
using EwanCommon.Logging;
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using Ewan.Model.Messages;
using log4net;
using System;
using System.Threading;

namespace Ewan.Core.Operator
{
    /// <summary>
    /// 生产线操作器 - 封装 MachineOperator 模式
    /// 提供统一的 Start/Stop/Pause/Home 接口
    /// </summary>
    public sealed class ProductionLineOperator : IDisposable
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(ProductionLineOperator));

        private readonly AlarmService _alarmService;
        private readonly LogicRunner _runner;
        private readonly MachineOperator _operator;
        private readonly ProductionLineSharedState _sharedState;
        private readonly IBinElevator _binElevator;

        private IDisposable _stepChangedSubscription;
        private IDisposable _alarmMessageSubscription;
        private Thread _binElevatorThread;
        private volatile bool _binElevatorRunning;
        private bool _disposed;

        #region 公共属性

        /// <summary>
        /// 报警服务（供外部订阅）
        /// </summary>
        public IAlarmService Alarms => _alarmService;

        /// <summary>
        /// 当前运行状态
        /// </summary>
        public RunTimeTag RunState => _runner.RunTag;

        /// <summary>
        /// 当前逻辑状态字符串
        /// </summary>
        public string CurrentLogicState => _runner.CurLogicStateStr;

        /// <summary>
        /// 是否有报警
        /// </summary>
        public bool HasAlarm => _alarmService.HasAlarm;

        /// <summary>
        /// 是否有需要复位的报警
        /// </summary>
        public bool HasNeedResetAlarm => _alarmService.HasNeedResetAlarm;

        /// <summary>
        /// 共享状态
        /// </summary>
        public ProductionLineSharedState SharedState => _sharedState;

        /// <summary>
        /// 料仓升降模块
        /// </summary>
        public IBinElevator BinElevator => _binElevator;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public ProductionLineOperator()
        {
            // 创建报警服务
            _alarmService = new AlarmService();
            _alarmService.AlarmChanged += OnAlarmChanged;

            // 创建共享状态
            _sharedState = new ProductionLineSharedState();

            // 创建 BinElevator（保留 Module 模式）
            var binElevatorModule = new BinElevatorModule(_sharedState);
            binElevatorModule.Init();
            _binElevator = binElevatorModule;

            // 创建逻辑执行器
            _runner = new LogicRunner();

            // 订阅异常事件 -> 转为报警
            _runner.LogicException += OnLogicException;

            // 创建操作器
            _operator = new MachineOperator(_alarmService, _runner);

            // 订阅步骤变化事件（可选，用于UI显示）
            _stepChangedSubscription = MessageHub.Current.Subscribe<StepChangedEventArgs>(OnStepChanged);
            _alarmMessageSubscription = MessageHub.Current.Subscribe<AlarmMessage>(OnAlarmMessage);

            s_logger.Info("ProductionLineOperator 初始化完成");
        }

        /// <summary>
        /// 带自定义 BinElevator 的构造函数（用于测试）
        /// </summary>
        /// <param name="binElevator">料仓升降模块</param>
        public ProductionLineOperator(IBinElevator binElevator)
        {
            _alarmService = new AlarmService();
            _alarmService.AlarmChanged += OnAlarmChanged;

            _sharedState = new ProductionLineSharedState();
            _binElevator = binElevator ?? throw new ArgumentNullException(nameof(binElevator));
            _binElevator.Init();

            _runner = new LogicRunner();
            _runner.LogicException += OnLogicException;

            _operator = new MachineOperator(_alarmService, _runner);
            _stepChangedSubscription = MessageHub.Current.Subscribe<StepChangedEventArgs>(OnStepChanged);
            _alarmMessageSubscription = MessageHub.Current.Subscribe<AlarmMessage>(OnAlarmMessage);

            s_logger.Info("ProductionLineOperator 初始化完成 (自定义 BinElevator)");
        }

        #endregion

        #region 公共控制方法

        /// <summary>
        /// 启动生产线
        /// </summary>
        /// <returns>是否启动成功（有报警时返回false）</returns>
        public bool Start()
        {
            if (HasAlarm)
            {
                s_logger.Warn("存在报警，无法启动");
                return false;
            }

            // 启动料仓升降轮询线程
            StartBinElevatorThread();

            return _operator.Start(() => CreateProductionLineLogic());
        }

        /// <summary>
        /// 暂停生产线
        /// </summary>
        public void Pause()
        {
            _operator.Pause();
            _sharedState.SetSystemPaused(true);
            s_logger.Info("生产线已暂停");
        }

        /// <summary>
        /// 恢复生产线（暂停后恢复）
        /// </summary>
        public void Resume()
        {
            _sharedState.SetSystemPaused(false);
            _sharedState.SetRequireReinit(true);
            _runner.Start();
            s_logger.Info("生产线已恢复");
        }

        /// <summary>
        /// 停止生产线
        /// </summary>
        /// <param name="clearQueue">是否清空队列</param>
        public void Stop(bool clearQueue = true)
        {
            _operator.Stop(clearQueue);
            _sharedState.ResetAllStates();
            _binElevator.ForceStopAllBins();
            StopBinElevatorThread();
            s_logger.Info("生产线已停止");
        }

        /// <summary>
        /// 紧急停止
        /// </summary>
        public void EmergencyStop()
        {
            Stop(clearQueue: true);
            _alarmService.AddAlarm(
                content: "紧急停止",
                level: AlarmLevel.H,
                unit: "System",
                needReset: true,
                key: "System.EmergencyStop");
            s_logger.Warn("生产线紧急停止");
        }

        /// <summary>
        /// 复位/回原
        /// </summary>
        /// <param name="clearAlarm">是否清除报警</param>
        public void Home(bool clearAlarm = true)
        {
            // 确保料仓轮询线程运行
            StartBinElevatorThread();

            _operator.Home(
                homeLogicFactory: () => CreateHomeLogic(),
                clearAlarm: clearAlarm,
                beforeHome: () =>
                {
                    _sharedState.ResetAllStates();
                    _binElevator.ForceStopAllBins();
                });
            s_logger.Info("生产线开始复位");
        }

        /// <summary>
        /// 清除报警
        /// </summary>
        public void ClearAlarm()
        {
            _operator.ClearAlarm();
            s_logger.Info("报警已清除");
        }

        /// <summary>
        /// 单步执行（调试用）
        /// </summary>
        public void Step()
        {
            _operator.Step();
        }

        #endregion

        #region BinElevator 轮询线程管理

        private void StartBinElevatorThread()
        {
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
            s_logger.Info("BinElevator 轮询线程已启动");
        }

        private void StopBinElevatorThread()
        {
            _binElevatorRunning = false;
            if (_binElevatorThread != null && _binElevatorThread.IsAlive)
            {
                _binElevatorThread.Join(1000);
            }
            _binElevatorThread = null;
            s_logger.Info("BinElevator 轮询线程已停止");
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

        #endregion

        #region Logic 工厂方法

        private ProductionLineLogic CreateProductionLineLogic()
        {
            var logic = new ProductionLineLogic(_sharedState);
            logic.SetBinElevatorModule(_binElevator);
            return logic;
        }

        private HomeLogic CreateHomeLogic()
        {
            return new HomeLogic(_sharedState, _binElevator);
        }

        #endregion

        #region 事件处理

        private void OnLogicException(object sender, LogicExceptionEventArgs e)
        {
            // 逻辑异常转为报警
            _alarmService.AddAlarm(
                content: $"流程异常：{e.LogicName}/{e.Step} - {e.Exception?.Message}",
                level: AlarmLevel.H,
                unit: "Logic",
                needReset: true,
                key: "Logic.Exception");

            // 停机
            _runner.Stop();

            s_logger.Error($"逻辑异常: {e.LogicName}/{e.Step}", e.Exception);
        }

        private void OnAlarmChanged(object sender, AlarmChangedEventArgs e)
        {
            var key = e.Alarm?.Key ?? "(null)";
            var content = e.Alarm?.Content ?? "(cleared)";
            s_logger.Info($"报警变化: kind={e.Kind}, key={key}, content={content}");
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

        private void OnStepChanged(StepChangedEventArgs args)
        {
            s_logger.Debug($"步骤变化: {args.LogicName}: {args.FromStep} -> {args.ToStep}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopBinElevatorThread();
            _stepChangedSubscription?.Dispose();
            _alarmMessageSubscription?.Dispose();
            _runner?.Dispose();
            _binElevator?.Destroy();

            s_logger.Info("ProductionLineOperator 已销毁");
        }

        #endregion
    }
}
