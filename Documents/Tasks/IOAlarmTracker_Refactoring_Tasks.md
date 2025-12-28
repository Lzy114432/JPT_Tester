# IOAlarmTracker 重构任务清单

> **项目目标**: 将 PlcAlarmTracker 的标签驱动报警模式应用到 IO 系统，使 SafetyModule 只需在 MarkingMachineFeederIOModel 中定义标签即可自动监控报警，消除手动轮询逻辑。

## 架构约束（重要）

```
依赖关系（不可违反）：
┌─────────────┐
│   EwanIO    │  ← 最底层，不依赖 EwanCommon
└──────┬──────┘
       ↓
┌─────────────┐
│ Ewan.Model  │  ← 依赖 EwanIO + EwanCommon
└──────┬──────┘
       ↓
┌─────────────┐
│  Ewan.Core  │  ← 依赖所有（可访问 IAlarmService）
└─────────────┘

分层实现策略：
- EwanIO: 定义 IOAttribute 报警属性 + 本地枚举（IOAlarmLevel, IOAlarmAction）
- Ewan.Core: 实现 IOAlarmTracker（可访问 IAlarmService + 做枚举转换）
```

## 任务概览

| 阶段 | 任务数 | 预估工时 | 优先级 |
|------|--------|----------|--------|
| Phase 1: EwanIO 层扩展 | 3 | 3h | P0 |
| Phase 2: Ewan.Core 层组件 | 4 | 8h | P0 |
| Phase 3: Model 标记 | 2 | 2h | P1 |
| Phase 4: SafetyModule 重构 | 3 | 4h | P1 |
| Phase 5: 测试与验证 | 3 | 4h | P2 |
| **总计** | **15** | **21h** | - |

---

## Phase 1: EwanIO 层扩展 (P0)

> **注意**: 此阶段所有代码放在 EwanIO 项目中，**不能依赖 EwanCommon**

### Task 1.1: 定义 IO 报警本地枚举
- **位置**: `Hardware\EwanIO\Core\Attributes\IOAlarmEnums.cs`
- **描述**: 在 EwanIO 中定义独立的报警枚举，避免依赖 EwanCommon
- **依赖**: 无
- **预估**: 0.5h

**验收标准**:
- [ ] 定义 `IOAlarmLevel` 枚举：`L`, `M`, `H`（与 EwanCore.AlarmLevel 对应）
- [ ] 定义 `IOAlarmAction` 枚举：`Warning`, `Pause`, `EmergencyStop`
- [ ] 添加 XML 文档注释
- [ ] **不引用 EwanCommon**
- [ ] 编译通过

**代码示例**:
```csharp
namespace EwanIO.Core.Attributes
{
    /// <summary>
    /// IO 报警级别（本地定义，避免跨项目依赖）
    /// </summary>
    public enum IOAlarmLevel
    {
        /// <summary>低级别：仅记录</summary>
        L = 0,
        /// <summary>中级别：暂停</summary>
        M = 1,
        /// <summary>高级别：紧急停机</summary>
        H = 2
    }

    /// <summary>
    /// IO 报警动作类型
    /// </summary>
    public enum IOAlarmAction
    {
        /// <summary>仅警告，不影响运行</summary>
        Warning = 0,
        /// <summary>暂停系统</summary>
        Pause = 1,
        /// <summary>紧急停机</summary>
        EmergencyStop = 2
    }
}
```

---

### Task 1.2: 扩展 IOAttribute 添加报警属性
- **位置**: `Hardware\EwanIO\Core\Attributes\IOAttribute.cs`
- **描述**: 在现有 IOAttribute 中添加报警相关属性（使用本地枚举）
- **依赖**: Task 1.1
- **预估**: 1h

**验收标准**:
- [ ] 添加 `IsAlarm` 属性 (bool, 默认 false)
- [ ] 添加 `AlarmDesc` 属性 (string?, 报警描述)
- [ ] 添加 `AlarmLevel` 属性 (**IOAlarmLevel**, 默认 M)
- [ ] 添加 `AlarmAction` 属性 (**IOAlarmAction**, 默认 Warning)
- [ ] 添加 `NeedReset` 属性 (bool?, 是否需要复位)
- [ ] 现有功能不受影响
- [ ] **不引用 EwanCommon**
- [ ] 编译通过

**代码示例**:
```csharp
// 新增属性（使用本地枚举）
public bool IsAlarm { get; set; } = false;
public string? AlarmDesc { get; set; }
public IOAlarmLevel AlarmLevel { get; set; } = IOAlarmLevel.M;
public IOAlarmAction AlarmAction { get; set; } = IOAlarmAction.Warning;
public bool? NeedReset { get; set; }
```

---

### Task 1.3: 创建 IOAlarmPropertyInfo 数据类
- **位置**: `Hardware\EwanIO\Core\Attributes\IOAlarmPropertyInfo.cs`
- **描述**: 定义报警属性信息的数据结构（纯数据类，无外部依赖）
- **依赖**: Task 1.1
- **预估**: 0.5h

**验收标准**:
- [ ] 创建 `IOAlarmPropertyInfo` 类
- [ ] 包含属性：`PropertyName`, `Index`, `AlarmDesc`, `AlarmLevel`, `AlarmAction`, `NeedReset`
- [ ] 使用 **IOAlarmLevel** 和 **IOAlarmAction** 本地枚举
- [ ] 添加 XML 文档注释
- [ ] **不引用 EwanCommon**
- [ ] 编译通过

---

## Phase 2: Ewan.Core 层组件开发 (P0)

> **注意**: 此阶段所有代码放在 Ewan.Core 项目中，**可以访问 IAlarmService**

### Task 2.1: 创建 AlarmTriggeredEventArgs 事件参数类
- **位置**: `Ewan.Core\Alarm\AlarmTriggeredEventArgs.cs`
- **描述**: 定义报警触发事件的参数类
- **依赖**: 无
- **预估**: 0.5h

**验收标准**:
- [ ] 继承 `EventArgs`
- [ ] 包含属性：`Key`, `Description`, `Level` (AlarmLevel), `Action` (IOAlarmAction), `Timestamp`
- [ ] 添加 XML 文档注释
- [ ] 编译通过

---

### Task 2.2: 创建枚举转换工具类
- **位置**: `Ewan.Core\Alarm\AlarmLevelConverter.cs`
- **描述**: 提供 IOAlarmLevel ↔ AlarmLevel 的转换方法
- **依赖**: Task 1.1
- **预估**: 0.5h

**验收标准**:
- [ ] `ToAlarmLevel(IOAlarmLevel)` 方法
- [ ] `ToIOAlarmLevel(AlarmLevel)` 方法
- [ ] 编译通过

**代码示例**:
```csharp
public static class AlarmLevelConverter
{
    public static AlarmLevel ToAlarmLevel(IOAlarmLevel ioLevel)
    {
        return ioLevel switch
        {
            IOAlarmLevel.L => AlarmLevel.L,
            IOAlarmLevel.M => AlarmLevel.M,
            IOAlarmLevel.H => AlarmLevel.H,
            _ => AlarmLevel.M
        };
    }
}
```

---

### Task 2.3: 实现 IOAlarmTracker 核心逻辑
- **位置**: `Ewan.Core\Alarm\IOAlarmTracker.cs`
- **描述**: 参考 PlcAlarmTracker 实现 IO 版本的报警追踪器
- **依赖**: Task 1.1 ~ 1.3, Task 2.1, 2.2
- **预估**: 4h

**验收标准**:
- [ ] 泛型类 `IOAlarmTracker<TLayout>`
- [ ] 构造函数接受 `IAlarmService` 参数
- [ ] `ScanAlarmProperties()` 方法扫描标记了 `IsAlarm=true` 的 InputSignal 属性
- [ ] `Process()` 方法处理 IO 快照并同步报警
- [ ] 内部使用 `AlarmLevelConverter` 做枚举转换
- [ ] 支持上升沿检测（报警触发）和下降沿检测（报警清除）
- [ ] `AlarmTriggered` 事件在报警触发时触发
- [ ] `AlarmCleared` 事件在报警清除时触发
- [ ] 线程安全
- [ ] 添加 XML 文档注释
- [ ] 编译通过

**核心方法签名**:
```csharp
namespace Ewan.Core.Alarm
{
    public sealed class IOAlarmTracker<TLayout> where TLayout : class
    {
        public event EventHandler<AlarmTriggeredEventArgs>? AlarmTriggered;
        public event EventHandler<AlarmTriggeredEventArgs>? AlarmCleared;

        public IOAlarmTracker(IAlarmService alarmService);
        public void Process(TLayout model, Func<int, bool> getRisingEdge, Func<int, bool> getFallingEdge);
        public IReadOnlyList<IOAlarmPropertyInfo> AlarmProperties { get; }
    }
}
```

---

### Task 2.4: 添加 IOAlarmTracker 防抖机制
- **位置**: `Ewan.Core\Alarm\IOAlarmTracker.cs`
- **描述**: 添加报警防抖机制，避免短时间内重复触发
- **依赖**: Task 2.3
- **预估**: 2h

**验收标准**:
- [ ] 可配置防抖时间（默认 500ms）
- [ ] 同一报警在防抖时间内不重复触发
- [ ] 添加 `DebounceTime` 属性
- [ ] 单元测试验证防抖逻辑

---

## Phase 3: Model 标记 (P1)

### Task 3.1: 标记紧急停机报警信号
- **位置**: `Ewan.Model\IO\MarkingMachineFeederIOModel.cs`
- **描述**: 为紧急停机级别的 IO 信号添加报警标记
- **依赖**: Task 1.2
- **预估**: 1h

**验收标准**:
- [ ] X0 急停按钮：`IsAlarm=true, AlarmLevel=H, AlarmAction=EmergencyStop, NeedReset=true`
- [ ] X15 机械手报警信号：`IsAlarm=true, AlarmLevel=H, AlarmAction=EmergencyStop`
- [ ] X17 下相机报警信号：`IsAlarm=true, AlarmLevel=H, AlarmAction=EmergencyStop`
- [ ] X19 机械臂气缸报警信号：`IsAlarm=true, AlarmLevel=H, AlarmAction=EmergencyStop`
- [ ] 编译通过

**代码示例**:
```csharp
// 注意：使用 IOAlarmLevel 和 IOAlarmAction（EwanIO 本地枚举）
[IO(0, DisplayName = "急停(常闭)",
    IsAlarm = true, AlarmDesc = "急停按钮被按下",
    AlarmLevel = IOAlarmLevel.H, AlarmAction = IOAlarmAction.EmergencyStop, NeedReset = true)]
public InputSignal 急停按钮 { get; set; }
```

---

### Task 3.2: 标记暂停级报警信号
- **位置**: `Ewan.Model\IO\MarkingMachineFeederIOModel.cs`
- **描述**: 为暂停级别的 IO 信号添加报警标记
- **依赖**: Task 1.2
- **预估**: 1h

**验收标准**:
- [ ] X12 料仓1下限位置信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] X13 料仓2下限位置信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] X14 料仓3下限位置信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] X24 前门电磁感应信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] X25 后门电磁感应信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] X26 侧门电磁感应信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] X31 机械臂门电磁感应信号：`IsAlarm=true, AlarmLevel=M, AlarmAction=Pause`
- [ ] 编译通过

---

## Phase 4: SafetyModule 重构 (P1)

### Task 4.1: 集成 IOAlarmTracker 到 SafetyModule
- **位置**: `Ewan.Core\Module\SafetyModule.cs`
- **描述**: 在 SafetyModule 中集成 IOAlarmTracker，使用事件驱动替代轮询
- **依赖**: Phase 2, Phase 3
- **预估**: 2h

**验收标准**:
- [ ] 添加 `IOAlarmTracker<MarkingMachineFeederIOModel>` 字段
- [ ] 在 `OnInit()` 中初始化 IOAlarmTracker
- [ ] 订阅 `AlarmTriggered` 和 `AlarmCleared` 事件
- [ ] 在 `OnRun()` 中调用 `_alarmTracker.Process()`
- [ ] 编译通过

---

### Task 4.2: 实现报警事件响应逻辑
- **位置**: `Ewan.Core\Module\SafetyModule.cs`
- **描述**: 实现报警触发时的系统响应逻辑
- **依赖**: Task 4.1
- **预估**: 1h

**验收标准**:
- [ ] `OnAlarmTriggered` 方法根据 `AlarmAction` 执行相应动作
- [ ] `EmergencyStop` 动作：调用 `TriggerSystemStopPulse()` + 发送 `SystemControlMessage.EmergencyStop`
- [ ] `Pause` 动作：调用 `TriggerRobotPausePulse()` + 发送 `SystemControlMessage.Pause`
- [ ] `Warning` 动作：仅记录日志
- [ ] 保留安全门报警旁路检查逻辑

---

### Task 4.3: 移除 SafetyModule 硬编码报警逻辑
- **位置**: `Ewan.Core\Module\SafetyModule.cs`
- **描述**: 删除不再需要的硬编码报警检测代码
- **依赖**: Task 4.1, 4.2
- **预估**: 1h

**验收标准**:
- [ ] 移除 `_lastXxxTime` 防抖字段（约 11 个）
- [ ] 移除 `CheckAlarmInputs()` 方法
- [ ] 移除 `CheckPauseAlarms()` 方法
- [ ] 移除 `CheckEmergencyAlarms()` 方法
- [ ] 移除 `CanTriggerAlarm()` 方法
- [ ] 移除 `CheckSafetyDoorAlarm()` 方法
- [ ] 保留脉冲控制方法（`TriggerRobotPausePulse`, `TriggerSystemStopPulse` 等）
- [ ] 保留 `OnSystemControlMessage` 消息处理
- [ ] 代码行数从 ~650 行减少到 ~200 行
- [ ] 编译通过

---

## Phase 5: 测试与验证 (P2)

### Task 5.1: 编写 IOAlarmTracker 单元测试
- **位置**: `Hardware\EwanIO.Tests\Alarm\IOAlarmTrackerTests.cs`
- **描述**: 编写 IOAlarmTracker 的单元测试
- **依赖**: Phase 2
- **预估**: 2h

**验收标准**:
- [ ] 测试属性扫描正确性
- [ ] 测试上升沿触发报警
- [ ] 测试下降沿清除报警
- [ ] 测试防抖机制
- [ ] 测试事件触发
- [ ] 所有测试通过

---

### Task 5.2: 集成测试 - 报警触发验证
- **位置**: 手动测试 / 集成测试项目
- **描述**: 验证重构后的报警系统功能正常
- **依赖**: Phase 4
- **预估**: 1h

**验收标准**:
- [ ] 急停按钮触发紧急停机
- [ ] 机械手报警信号触发紧急停机
- [ ] 料仓下限信号触发暂停
- [ ] 安全门信号触发暂停（旁路关闭时）
- [ ] 安全门旁路开启时不触发报警
- [ ] 报警自动清除功能正常

---

### Task 5.3: 性能验证
- **位置**: 手动测试
- **描述**: 验证重构后的性能不低于原有实现
- **依赖**: Phase 4
- **预估**: 1h

**验收标准**:
- [ ] IO 同步周期保持 10ms
- [ ] 报警检测延迟 < 20ms
- [ ] CPU 占用无明显增加
- [ ] 内存占用无明显增加

---

## 依赖关系图（修正版）

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Phase 1: EwanIO 层 (不依赖 EwanCommon)                                 │
│                                                                         │
│    1.1 IOAlarmEnums ────────────────┐                                   │
│    (IOAlarmLevel, IOAlarmAction)    │                                   │
│              │                      │                                   │
│              ▼                      ▼                                   │
│    1.2 扩展 IOAttribute      1.3 IOAlarmPropertyInfo                    │
│              │                      │                                   │
└──────────────┼──────────────────────┼───────────────────────────────────┘
               │                      │
               ▼                      ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  Phase 2: Ewan.Core 层 (可访问 IAlarmService)                            │
│                                                                          │
│    2.1 AlarmTriggeredEventArgs                                           │
│              │                                                           │
│    2.2 AlarmLevelConverter ◄─────── 枚举转换                             │
│    (IOAlarmLevel → AlarmLevel)      │                                    │
│              │                      │                                    │
│              ▼                      ▼                                    │
│    2.3 IOAlarmTracker ◄───────── 使用 IAlarmService                      │
│              │                                                           │
│              ▼                                                           │
│    2.4 防抖机制                                                          │
│                                                                          │
└──────────────┬───────────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  Phase 3: Model 标记 (并行执行)                                          │
│                                                                          │
│    3.1 紧急停机信号标记 ◄────── 依赖 Task 1.2                            │
│    3.2 暂停级信号标记   ◄────── 依赖 Task 1.2                            │
│                                                                          │
└──────────────┬───────────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  Phase 4: SafetyModule 重构 (串行执行)                                   │
│                                                                          │
│    4.1 集成 IOAlarmTracker ◄────── 依赖 Phase 2 + Phase 3                │
│              │                                                           │
│              ▼                                                           │
│    4.2 实现事件响应逻辑                                                  │
│              │                                                           │
│              ▼                                                           │
│    4.3 移除硬编码报警逻辑                                                │
│                                                                          │
└──────────────┬───────────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│  Phase 5: 测试验证 (并行执行)                                            │
│                                                                          │
│    5.1 单元测试 ◄────────────── 依赖 Phase 2                             │
│    5.2 集成测试 ◄────────────── 依赖 Phase 4                             │
│    5.3 性能验证 ◄────────────── 依赖 Phase 4                             │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### 关键依赖说明

| 任务 | 依赖 | 说明 |
|------|------|------|
| 1.2 | 1.1 | IOAttribute 需要使用 IOAlarmLevel/IOAlarmAction 枚举 |
| 1.3 | 1.1 | IOAlarmPropertyInfo 需要使用本地枚举 |
| 2.2 | 1.1 | 转换器需要知道 IOAlarmLevel 定义 |
| 2.3 | 1.1~1.3, 2.1~2.2 | IOAlarmTracker 是核心组件，依赖所有基础设施 |
| 3.x | 1.2 | Model 标记需要扩展后的 IOAttribute |
| 4.1 | Phase 2 + 3 | 集成需要 IOAlarmTracker + 标记好的 Model |
| 5.1 | Phase 2 | 单元测试可提前进行，不依赖 Model 标记 |

---

## 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 边沿检测遗漏 | 报警未触发 | 保留原有检测间隔（20ms），单元测试覆盖 |
| 防抖时间不当 | 报警重复或遗漏 | 可配置防抖时间，默认保持 500ms |
| **枚举类型重复定义** | 维护成本 | IOAlarmLevel/IOAlarmAction 值与 AlarmLevel 保持一致，通过 AlarmLevelConverter 转换 |
| 安全门旁路逻辑丢失 | 功能缺失 | 在 IOAlarmTracker 或事件处理中保留旁路检查 |
| **循环依赖** | 编译失败 | 严格遵守分层：EwanIO 不依赖 EwanCommon，IOAlarmTracker 放在 Ewan.Core |

---

## 回滚方案

如果重构过程中发现严重问题：
1. 保留原有 SafetyModule 代码备份
2. IOAlarmTracker 作为可选组件，不强制启用
3. 可通过配置开关切换新旧实现

---

## 完成定义 (Definition of Done)

- [ ] 所有任务验收标准通过
- [ ] 代码通过 Code Review
- [ ] 单元测试覆盖率 > 80%
- [ ] 集成测试全部通过
- [ ] 性能指标满足要求
- [ ] 文档更新完成
