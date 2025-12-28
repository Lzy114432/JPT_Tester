# 冗余代码移除重构 - 任务分解

## 任务概览

| 任务ID | 任务名称 | 预估时间 | 优先级 | 依赖 |
|--------|----------|----------|--------|------|
| T01 | 删除 ProductionLineSharedState.cs | 5 min | P0 | - |
| T02 | 修改 LogicManager - 新增 IO 脉冲方法 | 30 min | P0 | - |
| T03 | 修改 LogicManager - 新增硬件控制方法 | 45 min | P0 | - |
| T04 | 修改 LogicManager - 移除 SharedState 相关代码 | 15 min | P0 | T02, T03 |
| T05 | 修改 MainLogic.cs | 10 min | P1 | T04 |
| T06 | 修改 MaterialLoadingLogic.cs | 15 min | P1 | T04 |
| T07 | 修改 MaterialUnloadingLogic.cs | 10 min | P1 | T04 |
| T08 | 修改 MainWindowViewModel.cs | 30 min | P1 | T02, T03 |
| T09 | 修改 App.xaml.cs | 5 min | P1 | T03 |
| T10 | 删除 SystemControlService.cs | 5 min | P2 | T08, T09 |
| T11 | 编译验证 | 15 min | P0 | T01-T10 |
| T12 | 功能测试 | 30 min | P0 | T11 |

---

## T01: 删除 ProductionLineSharedState.cs

### 描述
删除完全冗余的 `ProductionLineSharedState` 类文件。

### 文件位置
`Ewan.Core\Module\ProductionLineSharedState.cs`

### 验收标准
- [ ] 文件已删除
- [ ] 项目文件(.csproj)中已移除引用（如有）

### 预估时间
5 分钟

### 优先级
P0 - 最高

### 依赖
无

---

## T02: 修改 LogicManager - 新增 IO 脉冲方法

### 描述
将 `SystemControlService` 中的 IO 脉冲方法迁移到 `LogicManager`。

### 文件位置
`Ewan.Core\Manager\LogicManager.cs`

### 实现内容

**1. 添加常量和属性**

```csharp
private const int DEFAULT_PULSE_WIDTH_MS = 200;

private IoContext<MarkingMachineFeederIOModel> Ctx => LayeredIOManager.Instance()?.Ctx;
```

**2. 添加 IO 脉冲方法**

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

**3. 添加 using**

```csharp
using Ewan.Model.IO;
using EwanIO.Core.Context;
```

### 验收标准
- [ ] 常量 `DEFAULT_PULSE_WIDTH_MS` 已添加
- [ ] 属性 `Ctx` 已添加
- [ ] 方法 `SendStopPulse()` 已实现
- [ ] 方法 `SendRecoveryPulse()` 已实现
- [ ] 方法 `SendPausePulse()` 已实现（private）
- [ ] using 语句已添加

### 预估时间
30 分钟

### 优先级
P0

### 依赖
无

---

## T03: 修改 LogicManager - 新增硬件控制方法

### 描述
将 `SystemControlService` 中的硬件控制方法迁移到 `LogicManager`。

### 文件位置
`Ewan.Core\Manager\LogicManager.cs`

### 实现内容

**1. 添加硬件控制方法**

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

**2. 添加 using**

```csharp
using System.Threading.Tasks;
```

### 验收标准
- [ ] 方法 `SetHighSpeedMode(bool)` 已实现
- [ ] 方法 `ClearHardwareAlarm()` 已实现
- [ ] 方法 `AreSafetyDoorsClosed()` 已实现
- [ ] 方法 `ReadInitializeSignal()` 已实现
- [ ] 方法 `EnsurePauseRecoveryBeforeShutdown()` 已实现
- [ ] using 语句已添加

### 预估时间
45 分钟

### 优先级
P0

### 依赖
无

---

## T04: 修改 LogicManager - 移除 SharedState 相关代码

### 描述
移除 `LogicManager` 中与 `ProductionLineSharedState` 相关的所有代码。

### 文件位置
`Ewan.Core\Manager\LogicManager.cs`

### 实现内容

**1. 删除字段和属性**

```diff
- private ProductionLineSharedState _sharedState;
- public ProductionLineSharedState SharedState => _sharedState;
```

**2. 修改 Init() 方法**

```diff
  public override bool Init()
  {
      // ...
-     _sharedState = new ProductionLineSharedState();
      // ...
  }
```

**3. 修改 HomeInternal() 方法**

```diff
- _sharedState?.ResetAllStates();
```

**4. 修改 StartInternal() 方法**

```diff
- _mainLogic = new MainLogic(_sharedState);
+ _mainLogic = new MainLogic();
```

**5. 修改 StopInternal() 方法**

```diff
- _sharedState?.SetSystemPaused(false);
- _sharedState?.ResetAllStates();
```

**6. 修改 PauseInternal() 方法**

```diff
- _sharedState?.SetSystemPaused(true);
+ SendPausePulse();
```

**7. 修改 ResumeInternal() 方法**

```diff
- _sharedState?.SetSystemPaused(false);
```

**8. 删除 using**

```diff
- using Ewan.Core.Module;
```

### 验收标准
- [ ] 字段 `_sharedState` 已删除
- [ ] 属性 `SharedState` 已删除
- [ ] Init() 中初始化代码已删除
- [ ] HomeInternal() 中 ResetAllStates 调用已删除
- [ ] StartInternal() 中构造函数参数已移除
- [ ] StopInternal() 中 SharedState 调用已删除
- [ ] PauseInternal() 中改为调用 SendPausePulse()
- [ ] ResumeInternal() 中 SharedState 调用已删除
- [ ] using 语句已清理

### 预估时间
15 分钟

### 优先级
P0

### 依赖
T02, T03

---

## T05: 修改 MainLogic.cs

### 描述
移除 `MainLogic` 中对 `ProductionLineSharedState` 的依赖。

### 文件位置
`Ewan.Core\Logic\MainLogic.cs`

### 实现内容

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

**删除 using**

```diff
- using Ewan.Core.Module;
```

### 验收标准
- [ ] 字段 `_sharedState` 已删除
- [ ] 主构造函数参数已移除
- [ ] 内部构造函数中 SharedState 初始化已删除
- [ ] MaterialLoadingLogic 构造调用已更新
- [ ] MaterialUnloadingLogic 构造调用已更新
- [ ] using 语句已清理

### 预估时间
10 分钟

### 优先级
P1

### 依赖
T04

---

## T06: 修改 MaterialLoadingLogic.cs

### 描述
移除 `MaterialLoadingLogic` 中对 `ProductionLineSharedState` 的依赖。

### 文件位置
`Ewan.Core\Logic\MaterialLoadingLogic.cs`

### 实现内容

**1. 删除字段**

```diff
- private readonly ProductionLineSharedState _sharedState;
```

**2. 修改构造函数**

```diff
- public MaterialLoadingLogic(ProductionLineSharedState sharedState)
+ public MaterialLoadingLogic()
  {
-     _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
      _parametersManager = SystemParametersManager.Instance;
  }
```

**3. 修改 Handler() 方法**

```diff
  public override void Handler()
  {
-     // 系统暂停时不处理
-     if (_sharedState.IsSystemPaused())
-     {
-         return;
-     }

      switch (SwitchIndex)
      {
          // ...
          case "等待料片信号":
              _ioManager.Ctx.On(x => x.触发机械手皮带线允许取料);
-             _sharedState.MarkLoadingInProgress();
              SwitchIndex = "取料中";
              // ...
      }
  }
```

**4. 修改 HandleLoadingComplete() 方法**

```diff
- _sharedState.ClearLoadingInProgress();
```

**5. 修改 ForceCleanupIO() 方法**

```diff
- _sharedState.ClearLoadingInProgress();
```

**6. 删除 using**

```diff
- using Ewan.Core.Module;
```

### 验收标准
- [ ] 字段 `_sharedState` 已删除
- [ ] 构造函数参数已移除
- [ ] Handler() 中 IsSystemPaused 检查已删除
- [ ] Handler() 中 MarkLoadingInProgress 调用已删除
- [ ] HandleLoadingComplete() 中 ClearLoadingInProgress 已删除
- [ ] ForceCleanupIO() 中 ClearLoadingInProgress 已删除
- [ ] using 语句已清理

### 预估时间
15 分钟

### 优先级
P1

### 依赖
T04

---

## T07: 修改 MaterialUnloadingLogic.cs

### 描述
移除 `MaterialUnloadingLogic` 中对 `ProductionLineSharedState` 的依赖。

### 文件位置
`Ewan.Core\Logic\MaterialUnloadingLogic.cs`

### 实现内容

**1. 删除字段**

```diff
- private readonly ProductionLineSharedState _sharedState;
```

**2. 修改构造函数**

```diff
- public MaterialUnloadingLogic(ProductionLineSharedState sharedState)
+ public MaterialUnloadingLogic()
  {
-     _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
      _parametersManager = SystemParametersManager.Instance;
      _modbusRTUManager = ModbusRTUManager.Instance();
      // ...
  }
```

**3. 修改 Handler() 方法**

```diff
  public override void Handler()
  {
-     // 系统暂停时不处理
-     if (_sharedState.IsSystemPaused())
-     {
-         return;
-     }

      switch (SwitchIndex)
      {
          // ...
      }
  }
```

**4. 删除 using**

```diff
- using Ewan.Core.Module;
```

### 验收标准
- [ ] 字段 `_sharedState` 已删除
- [ ] 构造函数参数已移除
- [ ] Handler() 中 IsSystemPaused 检查已删除
- [ ] using 语句已清理

### 预估时间
10 分钟

### 优先级
P1

### 依赖
T04

---

## T08: 修改 MainWindowViewModel.cs

### 描述
将所有 `SystemControlService` 调用替换为 `LogicManager` 调用。

### 文件位置
`MarkingMachineFeeder\Viewmodel\MainWindowViewModel.cs`

### 实现内容

**1. 删除字段**

```diff
- private readonly SystemControlService _systemControlService;
```

**2. 删除构造函数中的初始化**

```diff
- _systemControlService = SystemControlService.Instance();
```

**3. 修改 ExecuteSystemInitialize()**

```diff
- _systemControlService.SetHighSpeedMode(false);
+ LogicManager.Instance().SetHighSpeedMode(false);

- _systemControlService.SendStopPulse();
+ LogicManager.Instance().SendStopPulse();
```

**4. 修改 ExecuteEmergencyStop()**

```diff
- _systemControlService.EmergencyStopSystem();
+ LogicManager.Instance().SendStopPulse();
+ LogicManager.Instance().EmergencyStop();
```

**5. 修改 ExecuteClearAlarm()**

```diff
- bool result = await _systemControlService.ClearAlarm();
+ bool result = await LogicManager.Instance().ClearHardwareAlarm();
```

**6. 修改 ExecuteSystemStart()**

```diff
- if (!_systemControlService.AreSafetyDoorsClosed())
+ if (!LogicManager.Instance().AreSafetyDoorsClosed())

- _systemControlService.SetHighSpeedMode(true);
+ LogicManager.Instance().SetHighSpeedMode(true);

- _systemControlService.SetHighSpeedMode(false);
+ LogicManager.Instance().SetHighSpeedMode(false);

- if (_systemControlService.IsPaused())
+ if (LogicManager.Instance().RunState == RunTimeTag.Pause)

- _systemControlService.SendRecoveryPulse();
- _systemControlService.ResumeSystem();
+ LogicManager.Instance().SendRecoveryPulse();
+ LogicManager.Instance().Resume();
```

**7. 修改 ExecuteSystemPause()**

```diff
- _systemControlService.PauseSystem();
+ LogicManager.Instance().Pause();
```

**8. 修改 ExecuteSystemResume()**

```diff
- _systemControlService.ResumeSystem();
+ LogicManager.Instance().Resume();
```

**9. 修改 ExecuteSystemStop()**

```diff
- _systemControlService.SendStopPulse();
+ LogicManager.Instance().SendStopPulse();
```

**10. 修改 using**

```diff
- using Ewan.BusinessBonding;
+ using EwanCore.StateMachine;  // for RunTimeTag
```

### 验收标准
- [ ] 字段 `_systemControlService` 已删除
- [ ] 构造函数中初始化已删除
- [ ] ExecuteSystemInitialize() 已更新
- [ ] ExecuteEmergencyStop() 已更新
- [ ] ExecuteClearAlarm() 已更新
- [ ] ExecuteSystemStart() 已更新（包括 IsPaused → RunState 检查）
- [ ] ExecuteSystemPause() 已更新
- [ ] ExecuteSystemResume() 已更新
- [ ] ExecuteSystemStop() 已更新
- [ ] using 语句已更新

### 预估时间
30 分钟

### 优先级
P1

### 依赖
T02, T03

---

## T09: 修改 App.xaml.cs

### 描述
将 `SystemControlService` 调用替换为 `LogicManager` 调用。

### 文件位置
`MarkingMachineFeeder\App.xaml.cs`

### 实现内容

**1. 修改 OnExit() 方法**

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

**2. 修改 using**

```diff
- using Ewan.BusinessBonding;
+ using Ewan.Core.Manager;
```

### 验收标准
- [ ] OnExit() 方法已更新
- [ ] using 语句已更新

### 预估时间
5 分钟

### 优先级
P1

### 依赖
T03

---

## T10: 删除 SystemControlService.cs

### 描述
删除已废弃的 `SystemControlService` 类文件。

### 文件位置
`Ewan.BusinessBonding\SystemControlService.cs`

### 验收标准
- [ ] 文件已删除
- [ ] 项目文件(.csproj)中已移除引用（如有）

### 预估时间
5 分钟

### 优先级
P2

### 依赖
T08, T09

---

## T11: 编译验证

### 描述
确保所有修改后项目能够成功编译。

### 命令

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln -p:Configuration=Debug -p:Platform=x64 -verbosity:minimal
```

### 验收标准
- [ ] 编译无错误
- [ ] 编译警告已检查和处理
- [ ] 所有项目成功生成

### 预估时间
15 分钟

### 优先级
P0

### 依赖
T01-T10

---

## T12: 功能测试

### 描述
验证所有系统控制功能正常工作。

### 测试清单

| 功能 | 测试步骤 | 预期结果 |
|------|----------|----------|
| 复位 | 点击复位按钮 | 发送停止脉冲，进入复位流程 |
| 启动 | 点击启动按钮 | 安全门检测通过，状态机启动 |
| 停止 | 点击停止按钮 | 发送停止脉冲，状态机停止 |
| 暂停 | 点击暂停按钮 | 发送暂停脉冲，状态机暂停 |
| 恢复 | 点击恢复按钮 | 状态机恢复运行 |
| 紧急停止 | 点击紧急停止按钮 | 发送停止脉冲，触发紧急停止 |
| 高速模式 | 切换高速模式 | IO 信号正确切换 |
| 清除报警 | 点击清除报警 | 发送清除报警脉冲 |
| 关闭应用 | 暂停状态下关闭 | 发送停止+复位脉冲 |

### 验收标准
- [ ] 复位功能正常
- [ ] 启动功能正常
- [ ] 停止功能正常
- [ ] 暂停功能正常
- [ ] 恢复功能正常
- [ ] 紧急停止功能正常
- [ ] 高速/低速模式切换正常
- [ ] 清除报警功能正常
- [ ] 应用关闭前处理正常

### 预估时间
30 分钟

### 优先级
P0

### 依赖
T11

---

## 执行顺序

```
阶段 1: 准备 (可并行)
├── T01: 删除 ProductionLineSharedState.cs
├── T02: LogicManager 新增 IO 脉冲方法
└── T03: LogicManager 新增硬件控制方法

阶段 2: LogicManager 清理
└── T04: LogicManager 移除 SharedState 相关代码

阶段 3: Logic 类更新 (可并行)
├── T05: 修改 MainLogic.cs
├── T06: 修改 MaterialLoadingLogic.cs
└── T07: 修改 MaterialUnloadingLogic.cs

阶段 4: UI 层更新 (可并行)
├── T08: 修改 MainWindowViewModel.cs
└── T09: 修改 App.xaml.cs

阶段 5: 清理
└── T10: 删除 SystemControlService.cs

阶段 6: 验证
├── T11: 编译验证
└── T12: 功能测试
```

---

## 总时间估算

| 阶段 | 任务 | 时间 |
|------|------|------|
| 阶段 1 | T01, T02, T03 | 1h 20min |
| 阶段 2 | T04 | 15min |
| 阶段 3 | T05, T06, T07 | 35min |
| 阶段 4 | T08, T09 | 35min |
| 阶段 5 | T10 | 5min |
| 阶段 6 | T11, T12 | 45min |
| **总计** | | **约 3h** |
