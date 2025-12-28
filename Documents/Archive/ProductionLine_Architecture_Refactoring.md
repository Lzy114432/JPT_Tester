# 生产线控制架构重构设计

## 目录

- [一、架构设计图](#一架构设计图)
- [二、核心组件设计](#二核心组件设计)
  - [2.1 ProductionLineOperator 类](#21-productionlineoperator-类)
  - [2.2 HomeLogic 类](#22-homelogic-类)
- [三、StreamController 改造方案](#三streamcontroller-改造方案)
- [四、文件清单](#四文件清单)
- [五、报警集成设计](#五报警集成设计)
- [六、状态广播设计](#六状态广播设计)
- [七、设计决策说明](#七设计决策说明)

---

## 一、架构设计图

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              StreamController                                     │
│                         (Manager, 流程编排入口)                                    │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                   ProductionLineOperator (新增)                          │    │
│  │                    封装 LogicRunner + MachineOperator                     │    │
│  │  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────────┐    │    │
│  │  │   AlarmService   │  │   LogicRunner    │  │   MachineOperator   │    │    │
│  │  │   (报警服务)      │  │   (逻辑执行器)    │  │   (操作接口)         │    │    │
│  │  └─────────────────┘  └────────┬─────────┘  └─────────────────────┘    │    │
│  │                                 │                                        │    │
│  │                    ┌────────────┼────────────┐                          │    │
│  │                    ▼            ▼            ▼                          │    │
│  │  ┌─────────────────────────────────────────────────────────────────┐   │    │
│  │  │                        LogicBase 队列                            │   │    │
│  │  │  ┌─────────────────┐  ┌────────────────────┐  ┌───────────────┐│   │    │
│  │  │  │ProductionLine   │  │MaterialLoadingLogic│  │MaterialUnload ││   │    │
│  │  │  │    Logic        │  │    (子Logic)       │  │  ingLogic     ││   │    │
│  │  │  │  (主流程状态机)  │  │                    │  │  (子Logic)    ││   │    │
│  │  │  └─────────────────┘  └────────────────────┘  └───────────────┘│   │    │
│  │  └─────────────────────────────────────────────────────────────────┘   │    │
│  │                                 ▲                                        │    │
│  │                                 │ StepChanged                            │    │
│  │                                 │ LogicException                         │    │
│  │                    ┌────────────┴────────────┐                          │    │
│  │                    ▼                          ▼                          │    │
│  │  ┌─────────────────────┐     ┌─────────────────────────────────────┐   │    │
│  │  │  MessageHub.Current  │     │         UI/监控订阅                  │   │    │
│  │  │  (事件广播)          │────▶│  - StepChangedEventArgs 显示步骤    │   │    │
│  │  │                     │     │  - AlarmChanged 显示报警            │   │    │
│  │  └─────────────────────┘     └─────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │                   辅助模块 (保持 StreamRunner + Module 模式)              │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌────────────┐  │    │
│  │  │ SafetyModule │  │StatusIndicator│  │ RingLineModule│  │  MesModule │  │    │
│  │  │  (IO同步)    │  │   (三色灯)   │  │  (环线通信)   │  │  (MES)    │  │    │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  └────────────┘  │    │
│  │  ┌──────────────┐  ┌──────────────┐                                    │    │
│  │  │BeltConveyor  │  │StationHeart  │                                    │    │
│  │  │   Module     │  │  beatModule  │                                    │    │
│  │  │  (皮带控制)   │  │  (站心跳)    │                                    │    │
│  │  └──────────────┘  └──────────────┘                                    │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                   │
│  ┌─────────────────────────────────────────────────────────────────────────┐    │
│  │              BinElevatorModule (保留为独立 Module)                        │    │
│  │              - 轴控制需要持续轮询，不适合状态机模式                         │    │
│  │              - 通过 SharedState 与 Logic 协调                             │    │
│  └─────────────────────────────────────────────────────────────────────────┘    │
│                                                                                   │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 二、核心组件设计

### 2.1 ProductionLineOperator 类

**文件位置**: `Ewan.Core\Operator\ProductionLineOperator.cs`

```csharp
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using Ewan.Core.Logic;
using Ewan.Core.Module;
using EwanCommon.Logging;
using log4net;
using System;

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
        private readonly BinElevatorModule _binElevator;

        private IDisposable _stepChangedSubscription;
        private IDisposable _exceptionSubscription;
        private bool _disposed;

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

        public ProductionLineOperator()
        {
            // 创建报警服务
            _alarmService = new AlarmService();
            _alarmService.AlarmChanged += OnAlarmChanged;

            // 创建共享状态
            _sharedState = new ProductionLineSharedState();

            // 创建 BinElevator（保留 Module 模式）
            _binElevator = new BinElevatorModule(_sharedState);
            _binElevator.Init();

            // 创建逻辑执行器
            _runner = new LogicRunner();

            // 订阅异常事件 -> 转为报警
            _runner.LogicException += OnLogicException;

            // 创建操作器
            _operator = new MachineOperator(_alarmService, _runner);

            // 订阅步骤变化事件（可选，用于UI显示）
            _stepChangedSubscription = MessageHub.Current.Subscribe<StepChangedEventArgs>(OnStepChanged);

            s_logger.Info("ProductionLineOperator 初始化完成");
        }

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

        /// <summary>
        /// 运行 BinElevator（需要在独立线程/StreamRunner中调用）
        /// </summary>
        public void RunBinElevator()
        {
            _binElevator.Run();
        }

        #endregion

        #region Logic 工厂方法

        private ProductionLineLogic CreateProductionLineLogic()
        {
            var logic = new ProductionLineLogic();
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

        private void OnStepChanged(StepChangedEventArgs args)
        {
            s_logger.Debug($"步骤变化: {args.LogicName}: {args.FromStep} -> {args.ToStep}");
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stepChangedSubscription?.Dispose();
            _runner?.Dispose();
            _binElevator?.Destroy();

            s_logger.Info("ProductionLineOperator 已销毁");
        }
    }
}
```

### 2.2 HomeLogic 类

**文件位置**: `Ewan.Core\Logic\HomeLogic.cs`

```csharp
using EwanCore.StateMachine;
using Ewan.Core.IO;
using Ewan.Core.Module;
using EwanCommon.Logging;

namespace Ewan.Core.Logic
{
    /// <summary>
    /// 复位流程状态机
    /// 执行硬件初始化序列
    /// </summary>
    public class HomeLogic : LogicBase
    {
        private readonly UILogger _uiLogger = new UILogger();
        private readonly ProductionLineSharedState _sharedState;
        private readonly BinElevatorModule _binElevator;
        private LayeredIOManager _ioManager;

        public HomeLogic(ProductionLineSharedState sharedState, BinElevatorModule binElevator)
        {
            _sharedState = sharedState;
            _binElevator = binElevator;
        }

        public override void Handler()
        {
            switch (SwitchIndex)
            {
                case "初始状态":
                    _uiLogger.InfoRaw("状态机启动: {0}", "HomeLogic");
                    _ioManager = LayeredIOManager.Instance();
                    SwitchIndex = "上料机初始化";
                    break;

                case "上料机初始化":
                    PerformFeederInit();
                    SwitchIndex = "料仓初始化";
                    break;

                case "料仓初始化":
                    _binElevator?.PerformHardwareInitialization();
                    SwitchIndex = "等待料仓完成";
                    Tw.Start(SwitchIndex);
                    break;

                case "等待料仓完成":
                    // 等待料仓初始化完成（简化处理，实际可检测完成信号）
                    if (Tw.StartCheckIsTimeout(SwitchIndex, 10000))
                    {
                        _uiLogger.InfoRaw("处理已完成: {0}", "HomeLogic 复位完成");
                        Complete();
                    }
                    break;
            }
        }

        private void PerformFeederInit()
        {
            if (_ioManager?.Ctx == null) return;

            // 上料机初始化序列
            _ioManager.Ctx.On(x => x.停止输出);
            System.Threading.Thread.Sleep(500);
            _ioManager.Ctx.Off(x => x.停止输出);
            System.Threading.Thread.Sleep(500);

            _ioManager.Ctx.On(x => x.开始);
            System.Threading.Thread.Sleep(500);
            _ioManager.Ctx.Off(x => x.开始);

            _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);

            _uiLogger.InfoRaw("处理已完成: {0}", "上料机硬件初始化完成");
        }
    }
}
```

---

## 三、StreamController 改造方案

```csharp
// StreamController 关键改动

[Manager(Priority = 3)]
public class StreamController : IManager
{
    // 移除 ProductionLineModule
    // private List<IModule> _mainModules = new List<IModule>();
    // private StreamRunner _mainRunner;

    // 新增 ProductionLineOperator
    private ProductionLineOperator _productionOperator;

    // 保留辅助流程的 StreamRunner
    private StreamRunner _safetyRunner;           // IO同步
    private StreamRunner _statusIndicatorRunner;  // 三色灯
    private StreamRunner _beltConveyorRunner;     // 皮带控制
    private StreamRunner _ringLineRunner;         // 环线通信
    private StreamRunner _mesRunner;              // MES
    private StreamRunner _stationHeartbeatRunner; // 站心跳
    private StreamRunner _binElevatorRunner;      // 料仓升降（新增，独立运行BinElevator）

    public bool Init()
    {
        // 创建 ProductionLineOperator
        _productionOperator = new ProductionLineOperator();

        // 订阅报警事件（可选，用于UI）
        _productionOperator.Alarms.AlarmChanged += OnAlarmChanged;

        // 辅助模块初始化（保持原有模式）
        _safetyModules.Add(new SafetyModule());
        _safetyRunner = new StreamRunner(_safetyModules);

        _statusIndicatorModules.Add(new SystemStatusIndicatorModule());
        _statusIndicatorRunner = new StreamRunner(_statusIndicatorModules);

        // ... 其他辅助模块

        // 创建 BinElevator 独立运行器
        // 使用包装模块，调用 Operator 的 RunBinElevator
        _binElevatorModules.Add(new BinElevatorWrapperModule(_productionOperator));
        _binElevatorRunner = new StreamRunner(_binElevatorModules);

        return true;
    }

    public void StartRun()
    {
        // 启动辅助流程
        _safetyRunner?.Start();
        _statusIndicatorRunner?.Start();
        _binElevatorRunner?.Start();  // 启动料仓升降
        // ... 其他辅助流程

        // 启动生产线（使用 Operator）
        _productionOperator?.Start();
    }

    public void StopRun()
    {
        // 停止生产线
        _productionOperator?.Stop();

        // 停止辅助流程
        _binElevatorRunner?.Stop();
        // ... 其他

        _safetyRunner?.Stop();  // 最后停止
    }

    // 新增：暴露 Operator 控制方法
    public void PauseProduction() => _productionOperator?.Pause();
    public void ResumeProduction() => _productionOperator?.Resume();
    public void EmergencyStop() => _productionOperator?.EmergencyStop();
    public void Home() => _productionOperator?.Home();
    public void ClearAlarm() => _productionOperator?.ClearAlarm();
}
```

---

## 四、文件清单

### 4.1 新增文件

| 文件路径                                     | 说明                               |
|----------------------------------------------|------------------------------------|
| `Ewan.Core\Operator\ProductionLineOperator.cs` | 生产线操作器，封装 MachineOperator |
| `Ewan.Core\Logic\HomeLogic.cs`                 | 复位流程状态机                     |
| `Ewan.Core\Module\BinElevatorWrapperModule.cs` | BinElevator 包装模块（可选）       |

### 4.2 修改文件

| 文件路径                                  | 改动说明                                              |
|-------------------------------------------|-------------------------------------------------------|
| `Ewan.Core\Logic\ProductionLineLogic.cs`    | 添加 `SetBinElevatorModule()` 公共方法                  |
| `Ewan.Core\Logic\MaterialLoadingLogic.cs`   | 添加 `ForceCleanup()` 公共方法、超时报警集成            |
| `Ewan.Core\Logic\MaterialUnloadingLogic.cs` | 添加 `ForceCleanup()` 公共方法、超时报警集成            |
| `Ewan.BusinessBonding\StreamController.cs`  | 使用 ProductionLineOperator 替代 ProductionLineModule |
| `Ewan.Core\Ewan.Core.csproj`                | 添加新文件引用                                        |

### 4.3 保留不变的文件

| 文件路径                               | 原因                                 |
|----------------------------------------|--------------------------------------|
| `Ewan.Core\Module\BinElevatorModule.cs`  | 轴控制需要持续轮询，保持 Module 模式 |
| `Ewan.Core\Module\SafetyModule.cs`       | IO同步模块，保持 Module 模式         |
| `Ewan.Core\Module\BeltConveyorModule.cs` | 皮带控制，保持 Module 模式           |
| 其他辅助 Module                        | 保持现有架构                         |

---

## 五、报警集成设计

### 5.1 报警 Key 规范

| Key                  | 触发场景     | NeedReset | Level |
|----------------------|--------------|-----------|-------|
| `Logic.Exception`      | 流程逻辑异常 | true      | H     |
| `System.EmergencyStop` | 紧急停止     | true      | H     |
| `Loading.Timeout`      | 装料超时     | false     | M     |
| `Unloading.Timeout`    | 卸料超时     | false     | M     |
| `Scan.Failed`          | 扫码失败     | false     | L     |
| `BinElevator.Timeout`  | 料仓升降超时 | true      | H     |

### 5.2 Logic 中的报警集成示例

```csharp
// MaterialLoadingLogic.cs 超时处理
private void ProcessWaitForMaterial()
{
    if (_ioManager?.Ctx?.R.检测到料片信号 == true)
    {
        // 正常处理...
    }
    else if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_MATERIAL_TIMEOUT))
    {
        // 超时报警
        MessageHub.Current.Post(new AlarmMessage
        {
            Key = "Loading.Timeout",
            Content = "等待料片信号超时",
            Level = AlarmLevel.M,
            NeedReset = false
        });

        ForceCleanup("等待料片超时");
    }
}
```

---

## 六、状态广播设计

### 6.1 StepChanged 事件订阅

```csharp
// UI 层订阅示例 (MainWindowViewModel.cs)
public MainWindowViewModel()
{
    // 订阅步骤变化
    _stepChangedSubscription = MessageHub.Current.Subscribe<StepChangedEventArgs>(args =>
    {
        // 更新UI显示
        CurrentStep = $"{args.LogicName}: {args.ToStep}";
        LastStepTime = args.Timestamp;
    });

    // 订阅报警变化
    _alarmSubscription = StreamController.Instance()
        .ProductionOperator.Alarms.AlarmChanged += (s, e) =>
    {
        // 更新报警列表
        RefreshAlarmList();
    };
}
```

---

## 七、设计决策说明

### 7.1 BinElevatorModule 保留为 Module 的原因

1. **轴控制特性**：需要持续 Jog 运动和实时感应器检测
2. **阻塞调用**：`RaiseToSensor()` 是同步阻塞方法，不适合状态机模式
3. **独立生命周期**：需要独立于主流程的初始化/销毁
4. **协调方式**：通过 `ProductionLineSharedState` 与 Logic 协调

### 7.2 辅助模块保留 StreamRunner 的原因

1. **并行执行**：SafetyModule、RingLineModule 等需要独立并行运行
2. **低耦合**：与主生产流程无直接状态依赖
3. **简单性**：这些模块逻辑简单，无需状态机

### 7.3 Operator 模式的优势

1. **统一接口**：Start/Stop/Pause/Home/ClearAlarm
2. **报警集成**：有报警时自动阻止启动
3. **异常处理**：LogicException 自动转为报警并停机
4. **可测试性**：Logic 可独立单元测试

---

## 附录：关键点总结

| 组件                       | 说明                                           |
|----------------------------|------------------------------------------------|
| **ProductionLineOperator** | 新增核心类，封装 LogicRunner + MachineOperator + AlarmService |
| **HomeLogic**              | 新增复位流程状态机                             |
| **BinElevatorModule**      | 保留为 Module，因轴控制需持续轮询，不适合状态机 |
| **辅助 Module**            | SafetyModule 等继续使用 StreamRunner           |
| **报警集成**               | 异常自动转报警，有报警阻止启动                 |
| **状态广播**               | StepChangedEventArgs 通过 MessageHub 订阅     |
