# 冗余代码移除与系统控制统一化重构

## 概述

本文档描述移除冗余的 `ProductionLineSharedState` 和 `SystemControlService`，将系统控制功能统一到 `LogicManager` 的重构方案。

## 问题分析

### 当前架构问题

#### 1. 暂停状态的三重冗余

```
┌─────────────────────────────────────────────────────────────┐
│                     暂停状态存储位置                         │
├─────────────────────────────────────────────────────────────┤
│ 1. LogicRunner.RunTag == RunTimeTag.Pause    ← 状态机核心   │
│ 2. ProductionLineSharedState._systemPaused   ← 冗余         │
│ 3. SystemControlService._isPaused            ← 冗余         │
└─────────────────────────────────────────────────────────────┘
```

#### 2. 调用方式混乱

```
MainWindowViewModel
    ├─ LogicManager.Instance().Home()      ← 直接调用
    ├─ LogicManager.Instance().Start()     ← 直接调用
    ├─ LogicManager.Instance().Stop()      ← 直接调用
    ├─ _systemControlService.PauseSystem() ← 消息驱动
    └─ _systemControlService.ResumeSystem()← 消息驱动
```

#### 3. 职责重叠

| 功能 | LogicManager | SystemControlService |
|------|-------------|---------------------|
| 启动/停止/暂停/恢复 | ✓ 核心实现 | ✓ 包装+消息 (冗余) |
| IO 脉冲 | ✗ | ✓ |
| 状态管理 | ✓ RunTag | ✓ _isPaused (冗余) |

---

## 重构方案

### 目标架构

```
┌─────────────────────────────────────────────────────────────┐
│                    MainWindowViewModel                       │
│                                                              │
│  所有操作统一直接调用 LogicManager                           │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ LogicManager.Instance().Home()                       │    │
│  │ LogicManager.Instance().Start()                      │    │
│  │ LogicManager.Instance().Stop()                       │    │
│  │ LogicManager.Instance().Pause()                      │    │
│  │ LogicManager.Instance().Resume()                     │    │
│  │ LogicManager.Instance().EmergencyStop()              │    │
│  │ LogicManager.Instance().SetHighSpeedMode()           │    │
│  │ LogicManager.Instance().ClearHardwareAlarm()         │    │
│  │ LogicManager.Instance().AreSafetyDoorsClosed()       │    │
│  └─────────────────────────────────────────────────────┘    │
│                            │                                 │
│                            ▼                                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                   LogicManager                       │    │
│  │  - 状态机控制 (LogicRunner.RunTag)                   │    │
│  │  - IO 脉冲操作 (通过 LayeredIOManager)               │    │
│  │  - 报警管理                                          │    │
│  │  - 消息发布 (供外部订阅)                             │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## 删除清单

### 1. 删除文件

| 文件路径 | 说明 |
|----------|------|
| `Ewan.Core\Module\ProductionLineSharedState.cs` | 完全冗余 |
| `Ewan.BusinessBonding\SystemControlService.cs` | 功能合并到 LogicManager |

---

## 修改清单

### 2.1 LogicManager.cs - 合并 SystemControlService 功能

**位置**: `Ewan.Core\Manager\LogicManager.cs`

**新增 IO 操作常量和字段**:

```csharp
private const int DEFAULT_PULSE_WIDTH_MS = 200;

/// <summary>
/// 获取 IO 上下文（简化访问）
/// </summary>
private IoContext<MarkingMachineFeederIOModel> Ctx => LayeredIOManager.Instance()?.Ctx;
```

**新增 IO 脉冲方法**:

```csharp
#region IO 脉冲操作

/// <summary>
/// 发送停止脉冲
/// </summary>
public void SendStopPulse()
{
    Ctx?.Pulse(x => x.停止输出, DEFAULT_PULSE_WIDTH_MS, now: true);
}

/// <summary>
/// 发送复位脉冲
/// </summary>
public void SendRecoveryPulse()
{
    Ctx?.Pulse(x => x.复位, DEFAULT_PULSE_WIDTH_MS);
}

/// <summary>
/// 发送暂停脉冲
/// </summary>
private void SendPausePulse()
{
    Ctx?.Pulse(x => x.暂停, DEFAULT_PULSE_WIDTH_MS);
}

#endregion
```

**新增硬件控制方法**:

```csharp
#region 硬件控制

/// <summary>
/// 设置高速/低速运行模式
/// </summary>
public void SetHighSpeedMode(bool enabled)
{
    try
    {
        var ioManager = LayeredIOManager.Instance();
        if (ioManager == null)
        {
            _uiLogger.WarnRaw("设置速度模式失败: IO管理器未初始化");
            return;
        }

        if (!ioManager.IsConnected)
        {
            ioManager.Connect();
        }

        var ctx = ioManager.Ctx;
        if (ctx == null)
        {
            _uiLogger.WarnRaw("设置速度模式失败: 未获取到IO上下文实例");
            return;
        }

        if (enabled)
        {
            ctx.On(x => x.高速运行);
        }
        else
        {
            ctx.Off(x => x.高速运行);
        }
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("设置速度模式失败: {0}", ex.Message);
    }
}

/// <summary>
/// 清除硬件报警（IO 脉冲）
/// </summary>
public async Task<bool> ClearHardwareAlarm(int pulseWidthMs = 100)
{
    try
    {
        var ioManager = LayeredIOManager.Instance();
        if (ioManager == null)
        {
            _uiLogger.WarnRaw("清除报警失败: IO管理器未初始化");
            return false;
        }

        if (!ioManager.IsConnected)
        {
            if (!ioManager.Connect())
            {
                _uiLogger.WarnRaw("清除报警失败: IO未连接");
                return false;
            }
        }

        var ctx = ioManager.Ctx;
        if (ctx == null)
        {
            _uiLogger.WarnRaw("清除报警失败: 未获取到IO上下文实例");
            return false;
        }

        ctx.On(x => x.清除报警, now: true);
        await Task.Delay(pulseWidthMs);
        ctx.Off(x => x.清除报警, now: true);

        return true;
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("清除报警异常: {0}", ex.Message);
        return false;
    }
}

/// <summary>
/// 检查安全门是否关闭
/// </summary>
public bool AreSafetyDoorsClosed()
{
    var parameters = SystemParametersManager.Instance.Parameters;
    if (parameters.SafetyDoorAlarmBypass)
    {
        return true;
    }

    try
    {
        var ioManager = LayeredIOManager.Instance();
        if (ioManager == null || ioManager.Ctx == null)
        {
            _uiLogger.WarnRaw("无法检测安全门状态: IO未初始化");
            return false;
        }

        if (!ioManager.IsConnected)
        {
            if (!ioManager.Connect())
            {
                _uiLogger.WarnRaw("无法检测安全门状态: IO未连接");
                return false;
            }
        }

        var ctx = ioManager.Ctx;
        if (ctx.R.前门电磁感应信号 || ctx.R.后门电磁感应信号 || ctx.R.侧门电磁感应信号)
        {
            return false;
        }

        return true;
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("检测安全门状态失败: {0}", ex.Message);
        return false;
    }
}

/// <summary>
/// 读取初始化信号
/// </summary>
public bool ReadInitializeSignal()
{
    try
    {
        var ctx = LayeredIOManager.Instance()?.Ctx;
        if (ctx == null)
        {
            return false;
        }

        return ctx.R.初始化信号;
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("读取初始化信号失败: {0}", ex.Message);
        return false;
    }
}

/// <summary>
/// 确保关闭前暂停恢复
/// </summary>
public void EnsurePauseRecoveryBeforeShutdown()
{
    if (RunState != RunTimeTag.Pause)
    {
        return;
    }

    _uiLogger.InfoRaw("检测到系统处于暂停状态，关闭前执行停止与复原脉冲");
    SendStopPulse();
    Thread.Sleep(DEFAULT_PULSE_WIDTH_MS);
    SendRecoveryPulse();
}

#endregion
```

**修改 PauseInternal 方法**:

```diff
  private void PauseInternal(bool publishSystemControl)
  {
      lock (_controlLock)
      {
-         _sharedState?.SetSystemPaused(true);
+         SendPausePulse();
          _controllerBox?.Pause();

          MessageHub.Current.Post(new StatusIndicatorCommand(SystemStatus.Paused, "暂停中"));
          // ...
      }
  }
```

**修改 HomeInternal 方法**:

```diff
  private void HomeInternal(bool publishSystemControl)
  {
      lock (_controlLock)
      {
          // ...
          StopInternal(publishSystemControl: false);
          MachineParameters.Instance.BeginHome();
          Thread.Sleep(500);

          DisposeMainLogicIfNeeded();
          _logicThread.ClearAction();

-         _sharedState?.ResetAllStates();
          MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.ForceStopAll(nameof(LogicManager)));
          MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.InitializeAll(nameof(LogicManager)));

          _logicThread.AddAction(new HomeLogic());
          _controllerBox.Start();
          // ...
      }
  }
```

**修改 StartInternal 方法**:

```diff
  private bool StartInternal(bool publishSystemControl)
  {
      // ...
      if (_logicThread.Count == 0)
      {
-         _mainLogic = new MainLogic(_sharedState);
+         _mainLogic = new MainLogic();
          _logicThread.AddAction(_mainLogic);
      }
      // ...
  }
```

**修改 StopInternal 方法**:

```diff
  private void StopInternal(bool publishSystemControl)
  {
      lock (_controlLock)
      {
          _controllerBox?.Stop();

-         _sharedState?.SetSystemPaused(false);
-         _sharedState?.ResetAllStates();
          MessageHub.Current.Post(Ewan.Model.Production.BinElevatorCommandMessage.ForceStopAll(nameof(LogicManager)));
          // ...
      }
  }
```

**修改 ResumeInternal 方法**:

```diff
  private void ResumeInternal(bool publishSystemControl)
  {
      lock (_controlLock)
      {
-         _sharedState?.SetSystemPaused(false);
          _controllerBox?.Start();
          // ...
      }
  }
```

**删除字段和属性**:

```diff
- private ProductionLineSharedState _sharedState;
- public ProductionLineSharedState SharedState => _sharedState;
```

**删除 Init 中的初始化**:

```diff
  public override bool Init()
  {
      // ...
-     _sharedState = new ProductionLineSharedState();
      // ...
  }
```

**需要添加的 using**:

```csharp
using System.Threading.Tasks;
using Ewan.Model.IO;
using EwanIO.Core.Context;
```

---

### 2.2 MainLogic.cs

**位置**: `Ewan.Core\Logic\MainLogic.cs`

```diff
  public class MainLogic : LogicBase, IDisposable
  {
      private readonly SystemParametersManager _parametersManager;
-     private readonly ProductionLineSharedState _sharedState;

      private readonly LogicBase _loadingLogic;
      private readonly LogicBase _unloadingLogic;

-     public MainLogic(ProductionLineSharedState sharedState)
+     public MainLogic()
      {
-         _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
          _parametersManager = SystemParametersManager.Instance;

-         var loadingLogic = new MaterialLoadingLogic(_sharedState);
-         var unloadingLogic = new MaterialUnloadingLogic(_sharedState);
+         var loadingLogic = new MaterialLoadingLogic();
+         var unloadingLogic = new MaterialUnloadingLogic();

          _loadingLogic = loadingLogic;
          _unloadingLogic = unloadingLogic;
      }

      internal MainLogic(LogicBase loadingLogic, LogicBase unloadingLogic)
      {
          _parametersManager = SystemParametersManager.Instance;
-         _sharedState = new ProductionLineSharedState();
          _loadingLogic = loadingLogic ?? throw new ArgumentNullException(nameof(loadingLogic));
          _unloadingLogic = unloadingLogic ?? throw new ArgumentNullException(nameof(unloadingLogic));
      }
  }
```

**删除 using**:

```diff
- using Ewan.Core.Module;
```

---

### 2.3 MaterialLoadingLogic.cs

**位置**: `Ewan.Core\Logic\MaterialLoadingLogic.cs`

```diff
  public class MaterialLoadingLogic : LogicBase
  {
      #region 私有字段

-     private readonly ProductionLineSharedState _sharedState;
      private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
      private readonly SystemParametersManager _parametersManager;

      // ...

-     public MaterialLoadingLogic(ProductionLineSharedState sharedState)
+     public MaterialLoadingLogic()
      {
-         _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
          _parametersManager = SystemParametersManager.Instance;
      }

      public override void Handler()
      {
-         // 系统暂停时不处理
-         if (_sharedState.IsSystemPaused())
-         {
-             return;
-         }

          switch (SwitchIndex)
          {
              // ...
              case "等待料片信号":
                  _ioManager.Ctx.On(x => x.触发机械手皮带线允许取料);
-                 _sharedState.MarkLoadingInProgress();
                  SwitchIndex = "取料中";
                  Tw.StartWatch(SwitchIndex);
                  break;
              // ...
          }
      }

      private void HandleLoadingComplete()
      {
          // ...
-         _sharedState.ClearLoadingInProgress();
          // ...
      }

      private void ForceCleanupIO()
      {
          // ...
-         _sharedState.ClearLoadingInProgress();
          // ...
      }
  }
```

**删除 using**:

```diff
- using Ewan.Core.Module;
```

---

### 2.4 MaterialUnloadingLogic.cs

**位置**: `Ewan.Core\Logic\MaterialUnloadingLogic.cs`

```diff
  public class MaterialUnloadingLogic : LogicBase
  {
      #region 私有字段

-     private readonly ProductionLineSharedState _sharedState;
      private readonly LayeredIOManager _ioManager = LayeredIOManager.Instance();
      // ...

-     public MaterialUnloadingLogic(ProductionLineSharedState sharedState)
+     public MaterialUnloadingLogic()
      {
-         _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
          _parametersManager = SystemParametersManager.Instance;
          _modbusRTUManager = ModbusRTUManager.Instance();
          // ...
      }

      public override void Handler()
      {
-         // 系统暂停时不处理
-         if (_sharedState.IsSystemPaused())
-         {
-             return;
-         }

          switch (SwitchIndex)
          {
              // ...
          }
      }
  }
```

**删除 using**:

```diff
- using Ewan.Core.Module;
```

---

### 2.5 MainWindowViewModel.cs - 统一调用 LogicManager

**位置**: `MarkingMachineFeeder\Viewmodel\MainWindowViewModel.cs`

**删除字段**:

```diff
- private readonly SystemControlService _systemControlService;
```

**删除构造函数中的初始化**:

```diff
  public MainWindowViewModel()
  {
      // ...
-     _systemControlService = SystemControlService.Instance();
      // ...
  }
```

**修改 ExecuteSystemInitialize**:

```diff
  private async Task ExecuteSystemInitialize()
  {
      // ...
      _uiLogger.InfoRaw("设置低速运行模式");
-     _systemControlService.SetHighSpeedMode(false);
+     LogicManager.Instance().SetHighSpeedMode(false);
      await Task.Delay(parameters.LowSpeedSetupDelayMs);

-     _systemControlService.SendStopPulse();
+     LogicManager.Instance().SendStopPulse();
      LogicManager.Instance().Home();
      // ...
  }
```

**修改 ExecuteEmergencyStop**:

```diff
  private void ExecuteEmergencyStop()
  {
      // ...
-     _systemControlService.EmergencyStopSystem();
+     LogicManager.Instance().SendStopPulse();
+     LogicManager.Instance().EmergencyStop();
      // ...
  }
```

**修改 ExecuteClearAlarm**:

```diff
  private async void ExecuteClearAlarm()
  {
      // ...
-     bool result = await _systemControlService.ClearAlarm();
+     bool result = await LogicManager.Instance().ClearHardwareAlarm();
      // ...
  }
```

**修改 ExecuteSystemStart**:

```diff
  private void ExecuteSystemStart()
  {
      // ...
-     if (!_systemControlService.AreSafetyDoorsClosed())
+     if (!LogicManager.Instance().AreSafetyDoorsClosed())
      {
          // ...
      }

      if (parameters.EnableHighSpeedMode)
      {
          _uiLogger.InfoRaw("启用高速运行模式");
-         _systemControlService.SetHighSpeedMode(true);
+         LogicManager.Instance().SetHighSpeedMode(true);
      }
      else
      {
          _uiLogger.InfoRaw("保持低速运行模式");
-         _systemControlService.SetHighSpeedMode(false);
+         LogicManager.Instance().SetHighSpeedMode(false);
      }

-     if (_systemControlService.IsPaused())
+     if (LogicManager.Instance().RunState == RunTimeTag.Pause)
      {
          _uiLogger.InfoRaw("检测到系统处于暂停状态，发送复原脉冲");
-         _systemControlService.SendRecoveryPulse();
-         _systemControlService.ResumeSystem();
+         LogicManager.Instance().SendRecoveryPulse();
+         LogicManager.Instance().Resume();
      }
      else
      {
          if (!LogicManager.Instance().Start())
          {
              // ...
          }
      }
      // ...
  }
```

**修改 ExecuteSystemPause**:

```diff
  private void ExecuteSystemPause()
  {
      try
      {
          _uiLogger.Info("处理已完成: {0}", "用户请求暂停系统");

-         _systemControlService.PauseSystem();
+         LogicManager.Instance().Pause();

          // 更新界面状态
          // ...
      }
  }
```

**修改 ExecuteSystemResume**:

```diff
  private void ExecuteSystemResume()
  {
      try
      {
          _uiLogger.Info("处理已完成: {0}", "用户请求恢复系统");

-         _systemControlService.ResumeSystem();
+         LogicManager.Instance().Resume();

          // 更新界面状态
          // ...
      }
  }
```

**修改 ExecuteSystemStop**:

```diff
  private void ExecuteSystemStop()
  {
      // ...
-     _systemControlService.SendStopPulse();
+     LogicManager.Instance().SendStopPulse();
      LogicManager.Instance().Stop();
      // ...
  }
```

**添加 using**:

```csharp
using EwanCore.StateMachine;  // for RunTimeTag
```

**删除 using**:

```diff
- using Ewan.BusinessBonding;
```

---

### 2.6 App.xaml.cs

**位置**: `MarkingMachineFeeder\App.xaml.cs`

```diff
  private void OnExit(object sender, ExitEventArgs e)
  {
      try
      {
-         var controlService = SystemControlService.Instance();
-         controlService.EnsurePauseRecoveryBeforeShutdown();
+         LogicManager.Instance().EnsurePauseRecoveryBeforeShutdown();
      }
      catch (System.Exception ex)
      {
          // ...
      }
  }
```

**修改 using**:

```diff
- using Ewan.BusinessBonding;
+ using Ewan.Core.Manager;
```

---

## 重构后架构

### 单一入口

```
┌─────────────────────────────────────────────────────────────┐
│                       LogicManager                          │
├─────────────────────────────────────────────────────────────┤
│  状态机控制                                                  │
│  ├─ Home()           复位                                   │
│  ├─ Start()          启动                                   │
│  ├─ Stop()           停止                                   │
│  ├─ Pause()          暂停                                   │
│  ├─ Resume()         恢复                                   │
│  └─ EmergencyStop()  紧急停止                               │
├─────────────────────────────────────────────────────────────┤
│  IO 脉冲操作                                                 │
│  ├─ SendStopPulse()      停止脉冲                           │
│  ├─ SendRecoveryPulse()  复位脉冲                           │
│  └─ SendPausePulse()     暂停脉冲 (private)                 │
├─────────────────────────────────────────────────────────────┤
│  硬件控制                                                    │
│  ├─ SetHighSpeedMode()        高速/低速模式                 │
│  ├─ ClearHardwareAlarm()      清除硬件报警                  │
│  ├─ AreSafetyDoorsClosed()    安全门检测                    │
│  ├─ ReadInitializeSignal()    读取初始化信号                │
│  └─ EnsurePauseRecoveryBeforeShutdown() 关闭前处理         │
├─────────────────────────────────────────────────────────────┤
│  状态查询                                                    │
│  ├─ RunState              当前运行状态                      │
│  ├─ HasAlarm              是否有报警                        │
│  ├─ HasNeedResetAlarm     是否有需复位报警                  │
│  └─ CurrentLogicState     当前逻辑状态字符串                │
└─────────────────────────────────────────────────────────────┘
```

### 调用流程

```
┌──────────────────┐          ┌─────────────────┐
│ MainWindowVM     │─────────▶│   LogicManager  │
│ App.xaml.cs      │          │   (唯一入口)     │
└──────────────────┘          └────────┬────────┘
                                       │
                   ┌───────────────────┼───────────────────┐
                   ▼                   ▼                   ▼
           ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
           │ LogicRunner │     │LayeredIO    │     │ AlarmService│
           │ (状态机)     │     │Manager (IO) │     │ (报警)      │
           └─────────────┘     └─────────────┘     └─────────────┘
```

---

## 测试验证

### 功能测试清单

- [ ] Home (复位) 功能正常 + IO 脉冲
- [ ] Start (启动) 功能正常 + 安全门检测
- [ ] Stop (停止) 功能正常 + IO 脉冲
- [ ] Pause (暂停) 功能正常 + IO 脉冲
- [ ] Resume (恢复) 功能正常
- [ ] EmergencyStop (紧急停止) 功能正常
- [ ] 高速/低速模式切换正常
- [ ] 清除硬件报警正常
- [ ] 安全门检测正常
- [ ] 应用关闭前暂停恢复正常

### 状态查询测试

```csharp
// 验证单一状态源
var state = LogicManager.Instance().RunState;
// RunTimeTag.Run / Pause / Stop / Step
```

---

## 删除统计

| 类型 | 数量 |
|------|------|
| 删除文件 | 2 |
| 删除类 | 2 (ProductionLineSharedState, SystemControlService) |
| 删除字段 | 5+ |
| 删除方法 | 10+ |
| 简化调用 | 12+ |

---

## 风险评估

| 风险项 | 影响 | 缓解措施 |
|--------|------|----------|
| 编译错误 | 中 | 按顺序修改，逐步验证 |
| 功能遗漏 | 低 | 所有功能已迁移到 LogicManager |
| 现有订阅 SystemControlMessage | 低 | 保留消息发布，不影响订阅方 |

---

## 总结

本次重构：
1. **删除** `ProductionLineSharedState` 类
2. **删除** `SystemControlService` 类
3. **合并** 所有系统控制功能到 `LogicManager`
4. **统一** 所有调用为直接调用 `LogicManager.Instance()`
5. **建立** 单一入口原则（Single Entry Point）

重构后：
- 代码更简洁（减少 2 个类，10+ 个方法）
- 职责更清晰（LogicManager 统一管理）
- 状态管理更统一（RunTag 为唯一状态源）
- 调用方式一致（全部直接调用）
