# Logic 架构重构任务分解

## 项目概述

**目标**: 将当前复杂的多层 Logic 架构重构为 ScribingV3 的简洁直接模式

---

## 参考程序

**参考项目路径**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\
```

**关键参考文件**:

| 文件 | 路径 | 说明 |
|------|------|------|
| LogicManger.cs | `ScribingV3\Logic\LogicManger.cs` | 逻辑管理器（主要参考） |
| HomeLogic.cs | `ScribingV3\Logic\HomeLogic.cs` | 复位逻辑（参考实现） |
| MainLogic.cs | `ScribingV3\Logic\MainLogic.cs` | 主逻辑（参考实现） |
| ArmLogicMain.cs | `ScribingV3\Logic\ArmLogic\ArmLogicMain.cs` | 子逻辑示例 |
| DualPlatformLogicMain.cs | `ScribingV3\Logic\PlatfromLogic\DualPlatformLogicMain.cs` | 子逻辑示例 |

**参考项目依赖**:
- `Byron.Commond` - 基础命令库
- `Byron.Commond.Logic.LogicThread` - 逻辑线程
- `Byron.Commond.Logic.BaseLogic` - 逻辑基类
- `Byron.Commond.ControllerBox` - 控制器盒子

---

## 当前项目路径

**当前项目路径**:
```
C:\Users\Administrator\Desktop\钧崴\MarkingMachineFeeder\MarkingMachineFeeder\
```

**需要重构的文件**:

| 文件 | 路径 | 操作 |
|------|------|------|
| ProductionLineOperator.cs | `Ewan.Core\Operator\` | 废弃/删除 |
| ProductionLineLogic.cs | `Ewan.Core\Logic\` | 废弃/删除 |
| HomeLogic.cs | `Ewan.Core\Logic\` | 重构 |
| MaterialLoadingLogic.cs | `Ewan.Core\Logic\` | 适配 |
| MaterialUnloadingLogic.cs | `Ewan.Core\Logic\` | 适配 |

**需要新建的文件**:

| 文件 | 路径 | 说明 |
|------|------|------|
| LogicManager.cs | `Ewan.Core\Manager\` | 新建 - 逻辑管理器 |
| MainLogic.cs | `Ewan.Core\Logic\` | 新建 - 主逻辑 |

---

## 问题分析

**当前问题**:
- 4层封装：`ProductionLineOperator` → `MachineOperator` → `LogicRunner` → `Logic`
- 消息驱动启动，流程不直观
- 状态机套状态机，调试困难
- 过度抽象，代码可读性低

**目标架构**:
```
LogicManager (新)
    │
    ├─→ LogicThread        // 逻辑队列（复用 EwanCommon）
    ├─→ ControllerBox      // 控制器（复用 EwanCommon）
    │
    ├─→ HomeLogic          // 复位逻辑（独立）
    └─→ MainLogic          // 主逻辑
            │
            ├─→ MaterialLoadingLogic   // 装料子逻辑
            └─→ MaterialUnloadingLogic // 下料子逻辑
```

---

## 任务依赖图

```
T1.1 ──┬──► T1.2 ──► T1.3
       │
T2.1 ──┴──► T2.2 ──► T2.3 ──► T2.4
                              │
T3.1 ──────────────────────► T3.2 ──► T3.3
                              │
T4.1 ──────────────────────► T4.2 ──► T4.3
                              │
                              ▼
                            T5.1 ──► T5.2 ──► T5.3
                                              │
                                              ▼
                                            T6.1 ──► T6.2
```

---

## Epic 1: 基础设施准备

### Story 1.1: 确认 EwanCommon 组件可用性

---

#### T1.1 验证 LogicThread 和 ControllerBox 存在 ✅

**描述**: 确认 EwanCommon 中是否已有 `LogicThread` 和 `ControllerBox` 类，或需要从 Byron.Commond 移植

**验收标准**:
- [x] 定位 LogicThread 类的位置
- [x] 定位 ControllerBox 类的位置
- [x] 如果不存在，记录需要创建的类
- [x] 文档记录 API 接口

**状态**: ✅ 完成
**实现位置**: `EwanCommon\EwanCommon\EwanCore\StateMachine\Engine\`

**依赖**: 无

**技术细节**:
- 组件: EwanCommon
- 预估工时: 1 小时
- 优先级: P0

**实现备注**:
- 检查 `EwanCommon\EwanCore\Runner\` 目录
- 检查 `EwanCommon\EwanCore\StateMachine\` 目录

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 32-33 行：`LogicThread` 的使用方式
- 查看第 44 行：`ControllerBox` 的绑定方式

---

#### T1.2 创建/验证 LogicThread 类 ✅

**描述**: 确保 `LogicThread` 类可用，提供逻辑队列管理功能

**验收标准**:
- [x] `AddAction(BaseLogic)` 方法可用
- [x] `ClearAction()` 方法可用
- [x] `ExistAction(Type)` 方法可用
- [x] `Count` 属性可用
- [x] `RunTag` 属性可用
- [x] `CurLogicStateStr` 属性可用

**状态**: ✅ 完成
**实现位置**: `EwanCommon\EwanCommon\EwanCore\StateMachine\Engine\LogicThread.cs`

**依赖**: T1.1

**技术细节**:
- 组件: EwanCommon
- 预估工时: 2-4 小时（如需创建）
- 优先级: P0

**实现备注**:
- 可能已存在为 `LogicRunner`，需要确认 API 兼容性

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 87 行：`logicManger.AddAction(new HomeLogic())`
- 查看第 117-119 行：`logicManger.Count`, `AddAction(new MainLogic())`
- 查看第 272-273 行：`logicManger.Count`, `RunTag`

---

#### T1.3 创建/验证 ControllerBox 类 ✅

**描述**: 确保 `ControllerBox` 类可用，提供统一的启停控制

**验收标准**:
- [x] `AddLogicManger(LogicThread)` 方法可用
- [x] `Start()` 方法可用
- [x] `Stop()` 方法可用
- [x] `Step()` 方法可用
- [x] 支持管理多个 LogicThread

**状态**: ✅ 完成
**实现位置**: `EwanCommon\EwanCommon\EwanCore\StateMachine\Engine\ControllerBox.cs`

**依赖**: T1.1

**技术细节**:
- 组件: EwanCommon
- 预估工时: 2-4 小时（如需创建）
- 优先级: P0

**实现备注**:
- 可能需要从头创建
- 功能类似于当前的 `MachineOperator` 但更简洁

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 44-46 行：`controllerBox.AddLogicManger(logicManger)`
- 查看第 88 行：`controllerBox.Start()`
- 查看第 158 行：`controllerBox.Start()`
- 查看第 170 行：`controllerBox.Stop()`
- 查看第 209 行：`controllerBox.Step()`

---

## Epic 2: LogicManager 实现

### Story 2.1: 创建新的 LogicManager

---

#### T2.1 创建 LogicManager 基础框架 ✅

**描述**: 创建新的 `LogicManager` 类，作为逻辑管理的统一入口

**验收标准**:
- [x] 创建 `Ewan.Core\Manager\LogicManager.cs`
- [x] 实现单例模式（继承 BaseManager）
- [x] 持有 LogicThread 实例
- [x] 持有 ControllerBox 实例
- [x] 构建成功无错误

**状态**: ✅ 完成
**实现位置**: `Ewan.Core\Manager\LogicManager.cs`

**依赖**: T1.2, T1.3

**技术细节**:
- 组件: Ewan.Core
- 文件: `Ewan.Core\Manager\LogicManager.cs`
- 预估工时: 2 小时
- 优先级: P0

**实现备注**:
```csharp
[Manager(Priority = 5)]
public class LogicManager : BaseManager<LogicManager>
{
    private LogicThread _logicThread;
    private ControllerBox _controllerBox;

    public override bool Init()
    {
        _logicThread = new LogicThread();
        _controllerBox = new ControllerBox();
        _controllerBox.AddLogicManger(_logicThread);
        return base.Init();
    }
}
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 30-47 行：整体类结构和构造函数

---

#### T2.2 实现 Home 方法 ✅

**描述**: 实现复位功能，直接添加 HomeLogic 到队列

**验收标准**:
- [x] `Home()` 方法实现
- [x] 调用 Stop() 停止当前逻辑
- [x] 清空逻辑队列
- [x] 添加 HomeLogic 到队列
- [x] 启动 ControllerBox
- [x] 防止重复复位

**状态**: ✅ 完成
**实现位置**: `LogicManager.HomeInternal()`

**依赖**: T2.1

**技术细节**:
- 组件: Ewan.Core
- 预估工时: 2 小时
- 优先级: P0

**实现备注**:
```csharp
public void Home()
{
    if (_logicThread.ExistAction(typeof(HomeLogic)))
    {
        _uiLogger.Warn("复位中，请勿重复点击");
        return;
    }

    Stop();
    Thread.Sleep(500);
    _logicThread.ClearAction();
    _logicThread.AddAction(new HomeLogic());
    _controllerBox.Start();
}
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 53-95 行：`Home()` 方法完整实现
- 注意第 54-68 行：防止重复复位的逻辑
- 注意第 71-74 行：Stop → Sleep → ClearAction 顺序

---

#### T2.3 实现 Start/Stop 方法 ✅

**描述**: 实现启动和停止功能

**验收标准**:
- [x] `Start()` 方法实现
- [x] 检查报警状态
- [x] 如队列为空则添加 MainLogic
- [x] `Stop()` 方法实现
- [x] 正确停止 ControllerBox

**状态**: ✅ 完成
**实现位置**: `LogicManager.StartInternal()` / `LogicManager.StopInternal()`

**依赖**: T2.2

**技术细节**:
- 组件: Ewan.Core
- 预估工时: 2 小时
- 优先级: P0

**实现备注**:
```csharp
public bool Start()
{
    if (AlarmService.Instance().HasAlarm)
    {
        _uiLogger.Warn("存在报警，无法启动");
        return false;
    }

    if (_logicThread.Count == 0)
    {
        _logicThread.AddAction(new MainLogic());
    }
    _controllerBox.Start();
    return true;
}

public void Stop()
{
    _controllerBox.Stop();
}
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 101-162 行：`Start()` 方法完整实现
- 注意第 104-109 行：NeedHome 检查
- 注意第 110-114 行：报警检查
- 注意第 117-121 行：队列为空时添加 MainLogic
- 查看第 167-201 行：`Stop()` 方法完整实现

---

#### T2.4 实现辅助方法 ✅

**描述**: 实现状态查询和调试支持方法

**验收标准**:
- [x] `GetCurLogicState()` 返回当前状态字符串
- [x] `IsRunning()` 返回运行状态
- [x] `SetStep()` 支持单步调试
- [x] `AddDebugLogic()` 支持测试逻辑注入

**状态**: ✅ 完成
**实现位置**: `LogicManager` 类中的公共方法

**依赖**: T2.3

**技术细节**:
- 组件: Ewan.Core
- 预估工时: 1 小时
- 优先级: P1

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\LogicManger.cs
```
- 查看第 204-212 行：`SetStep()` 方法
- 查看第 217-233 行：`AddDebugLogic()` 方法
- 查看第 304-307 行：`GetCurLogicState()` 方法
- 查看第 309-312 行：`IsRun()` 方法

---

## Epic 3: HomeLogic 重构

### Story 3.1: 简化 HomeLogic

---

#### T3.1 分析当前 HomeLogic 状态 ✅

**描述**: 分析当前 HomeLogic 的状态流程和依赖

**验收标准**:
- [x] 记录当前状态流程图
- [x] 识别所有外部依赖
- [x] 确定可简化的步骤
- [x] 文档记录分析结果

**状态**: ✅ 完成

**依赖**: 无

**技术细节**:
- 组件: 文档
- 预估工时: 1 小时
- 优先级: P0

**当前状态流程**:
```
初始状态 → 停止ON → 停止ON等待 → 停止OFF → 停止OFF等待
→ 开始ON → 开始ON等待 → 开始OFF → 清除允许取料
→ 料仓初始化 → 等待料仓完成 → 结束状态
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\HomeLogic.cs
```
- 查看整体状态流程设计
- 注意状态命名方式（中文）

---

#### T3.2 重构 HomeLogic ✅

**描述**: 按照 ScribingV3 模式重构 HomeLogic

**验收标准**:
- [x] 移除对 ProductionLineSharedState 的依赖（如可行）
- [x] 简化构造函数参数
- [x] 添加 `AbortHome()` 错误处理方法
- [x] 添加完成标志管理（IsHomeing）
- [x] 保持核心复位功能不变

**状态**: ✅ 完成
**实现位置**: `Ewan.Core\Logic\HomeLogic.cs`

**依赖**: T3.1

**技术细节**:
- 组件: Ewan.Core
- 文件: `Ewan.Core\Logic\HomeLogic.cs`
- 预估工时: 3 小时
- 优先级: P0

**实现备注**:
```csharp
public class HomeLogic : BaseLogic
{
    private void AbortHome(string alarmMessage, Exception ex = null)
    {
        if (!string.IsNullOrWhiteSpace(alarmMessage))
            AlarmService.Instance().AddAlarm(alarmMessage);

        MachineParameters.Instance.NeedHome = true;
        MachineParameters.Instance.IsHomeing = false;

        Complete();
    }

    public override void Handler()
    {
        try
        {
            switch (SwitchIndex)
            {
                case "初始状态":
                    MachineParameters.Instance.IsHomeing = true;
                    SwitchIndex = "IO复位";
                    break;
                // ...
                case "复位完成":
                    MachineParameters.Instance.NeedHome = false;
                    MachineParameters.Instance.IsHomeing = false;
                    Complete();
                    break;
            }
        }
        catch (Exception ex)
        {
            AbortHome("复位异常: " + ex.Message, ex);
        }
    }
}
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\HomeLogic.cs
```
- 查看第 15-38 行：`AbortHome()` 错误处理方法
- 查看第 40-226 行：`Handler()` 方法完整实现
- 查看第 47-78 行：初始状态处理
- 查看第 189-219 行：复位完成处理
- 注意整体的 try-catch 包裹模式

---

#### T3.3 HomeLogic 单元测试 ✅

**描述**: 为重构后的 HomeLogic 编写单元测试

**验收标准**:
- [x] 测试正常复位流程
- [x] 测试超时处理
- [x] 测试异常处理
- [x] 测试 AbortHome 功能
- [x] 所有测试通过

**状态**: ✅ 完成
**实现位置**: `Ewan.Core.Tests\Logic\HomeLogicTests.cs`

**依赖**: T3.2

**技术细节**:
- 组件: Ewan.Core.Tests
- 文件: `Ewan.Core.Tests\Logic\HomeLogicTests.cs`
- 预估工时: 2 小时
- 优先级: P1

---

## Epic 4: MainLogic 重构

### Story 4.1: 创建新的 MainLogic

---

#### T4.1 设计 MainLogic 结构 ✅

**描述**: 设计新的 MainLogic 类结构，参考 ScribingV3 的 MainLogic

**验收标准**:
- [x] 确定状态流程
- [x] 确定子逻辑调用方式
- [x] 设计清料/停止逻辑
- [x] 文档记录设计决策

**状态**: ✅ 完成

**依赖**: 无

**技术细节**:
- 组件: 文档
- 预估工时: 1 小时
- 优先级: P0

**设计参考**:
```
状态流程:
初始状态 → 主动作 → 结束状态

主动作中:
- 调用 _loadingLogic.Handler()
- 调用 _unloadingLogic.Handler()
- 检查清料完成条件
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\MainLogic.cs
```
- 查看整体类结构和状态流程
- 注意第 38-87 行："初始状态" 和 "主动作" 的处理

---

#### T4.2 实现 MainLogic ✅

**描述**: 实现新的 MainLogic 类

**验收标准**:
- [x] 创建 `Ewan.Core\Logic\MainLogic.cs`
- [x] 内嵌 MaterialLoadingLogic 和 MaterialUnloadingLogic
- [x] 实现 "初始状态" → "主动作" → "结束状态" 流程
- [x] 在 "主动作" 中循环调用子逻辑
- [x] 支持模块启用/禁用配置

**状态**: ✅ 完成
**实现位置**: `Ewan.Core\Logic\MainLogic.cs`

**依赖**: T4.1

**技术细节**:
- 组件: Ewan.Core
- 文件: `Ewan.Core\Logic\MainLogic.cs`
- 预估工时: 4 小时
- 优先级: P0

**实现备注**:
```csharp
public class MainLogic : BaseLogic
{
    private MaterialLoadingLogic _loadingLogic;
    private MaterialUnloadingLogic _unloadingLogic;
    private bool _loadingEnabled = true;
    private bool _unloadingEnabled = true;

    public MainLogic()
    {
        _loadingLogic = new MaterialLoadingLogic();
        _unloadingLogic = new MaterialUnloadingLogic();
    }

    public override void Handler()
    {
        switch (SwitchIndex)
        {
            case "初始状态":
                _loadingLogic.Rset();
                _unloadingLogic.Rset();
                SwitchIndex = "主动作";
                break;

            case "主动作":
                if (_loadingEnabled)
                    _loadingLogic.Handler();

                if (_unloadingEnabled)
                    _unloadingLogic.Handler();
                break;

            case "结束状态":
                Complete();
                break;
        }
    }
}
```

**📁 参考文件**:
```
C:\Users\Administrator\Desktop\钧崴\V1.42-20251221-MES待测试\V1.42-20251224\V1.42-20251224\ScribingV3\Logic\MainLogic.cs
```
- 查看第 14-29 行：类结构和子逻辑声明
- 查看第 31-50 行：初始状态处理和子逻辑复位
- 查看第 51-77 行：主动作中调用子逻辑 Handler()
- 查看第 56-77 行：报警检查和条件执行
- 查看第 318-321 行：结束状态处理

---

#### T4.3 MainLogic 单元测试 ✅

**描述**: 为 MainLogic 编写单元测试

**验收标准**:
- [x] 测试状态流程正确
- [x] 测试子逻辑调用
- [x] 测试模块启用/禁用
- [x] 所有测试通过

**状态**: ✅ 完成
**实现位置**: `Ewan.Core.Tests\Logic\MainLogicTests.cs`

**依赖**: T4.2

**技术细节**:
- 组件: Ewan.Core.Tests
- 文件: `Ewan.Core.Tests\Logic\MainLogicTests.cs`
- 预估工时: 2 小时
- 优先级: P1

---

## Epic 5: 子逻辑适配

### Story 5.1: 适配现有子逻辑

---

#### T5.1 简化 MaterialLoadingLogic 依赖

**描述**: 减少 MaterialLoadingLogic 对 ProductionLineSharedState 的依赖

**验收标准**:
- [ ] 分析当前依赖项
- [ ] 简化或移除 SharedState 依赖
- [ ] 使用简单标志替代复杂状态管理
- [ ] 保持核心功能不变
- [ ] 构建成功

**依赖**: T4.2

**技术细节**:
- 组件: Ewan.Core
- 文件: `Ewan.Core\Logic\MaterialLoadingLogic.cs`
- 预估工时: 3 小时
- 优先级: P1

**实现备注**:
- 可能需要保留部分 SharedState 用于装料/下料互斥
- 考虑使用简单的静态标志替代

---

#### T5.2 简化 MaterialUnloadingLogic 依赖

**描述**: 减少 MaterialUnloadingLogic 对 ProductionLineSharedState 的依赖

**验收标准**:
- [ ] 分析当前依赖项
- [ ] 简化或移除 SharedState 依赖
- [ ] 保持环线信号处理功能
- [ ] 保持核心功能不变
- [ ] 构建成功

**依赖**: T5.1

**技术细节**:
- 组件: Ewan.Core
- 文件: `Ewan.Core\Logic\MaterialUnloadingLogic.cs`
- 预估工时: 3 小时
- 优先级: P1

---

#### T5.3 子逻辑集成测试

**描述**: 验证子逻辑在新架构下正常工作

**验收标准**:
- [ ] 装料流程完整运行
- [ ] 下料流程完整运行
- [ ] 装料/下料优先级正确
- [ ] 无回归问题

**依赖**: T5.2

**技术细节**:
- 组件: Ewan.Core.Tests
- 预估工时: 2 小时
- 优先级: P1

---

## Epic 6: 集成与清理

### Story 6.1: UI 集成

---

#### T6.1 更新 ViewModel 调用 ✅

**描述**: 更新 UI ViewModel 使用新的 LogicManager

**验收标准**:
- [x] 更新启动按钮调用 `LogicManager.Instance().Start()`
- [x] 更新停止按钮调用 `LogicManager.Instance().Stop()`
- [x] 更新复位按钮调用 `LogicManager.Instance().Home()`
- [x] 更新状态显示绑定
- [x] UI 功能正常

**状态**: ✅ 完成
**实现位置**: `MarkingMachineFeeder\Viewmodel\MainWindowViewModel.cs`

**依赖**: T5.3

**技术细节**:
- 组件: MarkingMachineFeeder (UI)
- 文件: `MainWindowViewModel.cs` 等
- 预估工时: 2 小时
- 优先级: P0

**实现备注**:
```csharp
// 旧代码
_productionLineOperator.Start();

// 新代码
LogicManager.Instance().Start();
```

---

#### T6.2 清理废弃代码

**描述**: 移除不再使用的类和代码

**验收标准**:
- [ ] 标记 ProductionLineOperator 为废弃（或删除）
- [ ] 标记 ProductionLineLogic 为废弃（或删除）
- [ ] 清理未使用的消息类型
- [ ] 更新相关文档
- [ ] 构建成功无警告

**依赖**: T6.1

**技术细节**:
- 组件: Ewan.Core
- 预估工时: 2 小时
- 优先级: P2

**需要处理的文件**:
- `Ewan.Core\Operator\ProductionLineOperator.cs`
- `Ewan.Core\Logic\ProductionLineLogic.cs`
- `Ewan.Model\Messages\SystemControlMessage.cs`（可能）

---

## 任务优先级汇总

> **更新时间**: 2025-12-28
> **完成进度**: 14/17 (82%)

### P0 - 必须完成（阻塞其他任务）
| 任务ID | 任务名称 | 预估工时 | 状态 | 实现位置 |
|--------|----------|----------|------|----------|
| T1.1 | 验证 LogicThread 和 ControllerBox 存在 | 1h | ✅ 完成 | `EwanCommon\EwanCore\StateMachine\Engine\` |
| T1.2 | 创建/验证 LogicThread 类 | 2-4h | ✅ 完成 | `LogicThread.cs` |
| T1.3 | 创建/验证 ControllerBox 类 | 2-4h | ✅ 完成 | `ControllerBox.cs` |
| T2.1 | 创建 LogicManager 基础框架 | 2h | ✅ 完成 | `Ewan.Core\Manager\LogicManager.cs` |
| T2.2 | 实现 Home 方法 | 2h | ✅ 完成 | `LogicManager.HomeInternal()` |
| T2.3 | 实现 Start/Stop 方法 | 2h | ✅ 完成 | `LogicManager.StartInternal()/StopInternal()` |
| T3.1 | 分析当前 HomeLogic 状态 | 1h | ✅ 完成 | 见本文档状态流程图 |
| T3.2 | 重构 HomeLogic | 3h | ✅ 完成 | `Ewan.Core\Logic\HomeLogic.cs` |
| T4.1 | 设计 MainLogic 结构 | 1h | ✅ 完成 | 见本文档设计参考 |
| T4.2 | 实现 MainLogic | 4h | ✅ 完成 | `Ewan.Core\Logic\MainLogic.cs` |
| T6.1 | 更新 ViewModel 调用 | 2h | ✅ 完成 | `MainWindowViewModel.cs` |

### P1 - 重要（功能完整性）
| 任务ID | 任务名称 | 预估工时 | 状态 | 备注 |
|--------|----------|----------|------|------|
| T2.4 | 实现辅助方法 | 1h | ✅ 完成 | `GetCurLogicState/IsRunning/SetStep/AddDebugLogic` |
| T3.3 | HomeLogic 单元测试 | 2h | ✅ 完成 | `Ewan.Core.Tests\Logic\HomeLogicTests.cs` |
| T4.3 | MainLogic 单元测试 | 2h | ✅ 完成 | `Ewan.Core.Tests\Logic\MainLogicTests.cs` |
| T5.1 | 简化 MaterialLoadingLogic 依赖 | 3h | ⏳ 待定 | 仍依赖 SharedState，暂保留 |
| T5.2 | 简化 MaterialUnloadingLogic 依赖 | 3h | ⏳ 待定 | 仍依赖 SharedState，暂保留 |
| T5.3 | 子逻辑集成测试 | 2h | ⏳ 待定 | 依赖 T5.1/T5.2 |

### P2 - 可选（代码质量）
| 任务ID | 任务名称 | 预估工时 | 状态 | 备注 |
|--------|----------|----------|------|------|
| T6.2 | 清理废弃代码 | 2h | ⏳ 待定 | `ProductionLineOperator.cs`/`ProductionLineLogic.cs` 仍存在 |

---

## 总工时估算

| 类别 | 工时范围 | 已完成 | 剩余 |
|------|----------|--------|------|
| P0 任务 | 22-26 小时 | ✅ 全部完成 | 0h |
| P1 任务 | 13 小时 | 5h | 8h (T5.1/T5.2/T5.3) |
| P2 任务 | 2 小时 | 0h | 2h |
| **总计** | **37-41 小时** | **~27-31h** | **~10h** |

---

## 风险与注意事项

### 高风险
1. ~~**EwanCommon 组件可用性** - LogicThread/ControllerBox 可能不存在~~
   - ✅ 已解决: 组件已存在于 `EwanCommon\EwanCore\StateMachine\Engine\`

2. **SharedState 解耦复杂度** - 装料/下料互斥逻辑可能深度依赖
   - ⚠️ 当前状态: 仍保留 SharedState 依赖，作为可选优化项

### 中风险
1. ~~**UI 集成回归** - 按钮行为可能有细微差异~~
   - ✅ 已解决: ViewModel 已更新并验证

2. ~~**子逻辑状态管理** - 复位后状态可能不一致~~
   - ✅ 已解决: `LogicManager.HomeInternal()` 中调用 `_sharedState.ResetAllStates()`

### 低风险
1. ~~**构建问题** - 引用关系变化~~
   - ✅ 已解决: 构建通过

---

## 里程碑

### M1: 基础设施就绪 ✅
- 完成: T1.1, T1.2, T1.3
- 标志: LogicThread 和 ControllerBox 可用
- **状态**: ✅ 已完成

### M2: LogicManager 可用 ✅
- 完成: T2.1, T2.2, T2.3, T2.4
- 标志: LogicManager 可以启动/停止/复位
- **状态**: ✅ 已完成

### M3: Logic 重构完成 ✅
- 完成: T3.1, T3.2, T4.1, T4.2
- 标志: HomeLogic 和 MainLogic 重构完成
- **状态**: ✅ 已完成

### M4: 集成测试通过 ⏳
- 完成: T5.1, T5.2, T5.3, T6.1
- 标志: 完整流程可运行
- **状态**: ⏳ 部分完成 (T6.1 ✅, T5.x 待定)

### M5: 清理完成 ⏳
- 完成: T3.3, T4.3, T6.2
- 标志: 代码质量达标，废弃代码清理
- **状态**: ⏳ 部分完成 (T3.3/T4.3 ✅, T6.2 待定)

---

## 执行建议

### 已完成的串行执行路径
```
T1.1 ✅ → T1.2 ✅ → T1.3 ✅ → T2.1 ✅ → T2.2 ✅ → T2.3 ✅ → T3.2 ✅ → T4.2 ✅ → T6.1 ✅
```

### 剩余任务 (可选优化)
```
T5.1 → T5.2 → T5.3 → T6.2
```

**建议**：
- T5.1/T5.2 (SharedState 解耦) 可作为后续优化项，当前架构已满足功能需求
- T6.2 (废弃代码清理) 可在确认稳定后执行

---

## 当前架构总结

```
┌─────────────────────────────────────────────────────────────┐
│                    已实现的目标架构                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  LogicManager (Ewan.Core\Manager\)                          │
│      │                                                      │
│      ├─→ LogicThread (EwanCommon) ✅                        │
│      ├─→ ControllerBox (EwanCommon) ✅                      │
│      │                                                      │
│      ├─→ HomeLogic ✅                                       │
│      │       └─→ 状态机: 初始状态→停止→开始→料仓初始化→完成  │
│      │                                                      │
│      └─→ MainLogic ✅                                       │
│              │                                              │
│              ├─→ MaterialLoadingLogic (⚠️ 仍依赖 SharedState)│
│              └─→ MaterialUnloadingLogic (⚠️ 仍依赖 SharedState)│
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**关键改进**：
- ✅ 移除 4 层封装，简化为 LogicManager → Logic 直接调用
- ✅ 支持消息驱动和直接调用两种模式
- ✅ 非阻塞状态机模式
- ⚠️ SharedState 依赖保留，用于装料/下料互斥
