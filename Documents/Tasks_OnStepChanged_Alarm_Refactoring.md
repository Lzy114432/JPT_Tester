# 任务清单：OnStepChanged 日志与报警推送重构

> **状态**: ✅ 已完成
> **完成日期**: 2025-12-28

## 项目概述

**目标**：解决 StateMachine 中 OnStepChanged 与 UI 日志显示的关联缺失问题，并实现报警自动推送机制。

**问题分析**：
- OnStepChanged 只发布事件，不记录日志到 UI 日志窗口
- LogicRunner 异常和步骤超时没有自动产生报警

**预计工作量**：4-6 小时

---

## Epic: StateMachine 日志与报警增强

### Story 1: OnStepChanged 自动日志记录

| ID | 任务 | 优先级 | 预估 | 状态 |
|----|------|--------|------|------|
| T1.1 | LogicBase 添加 protected _uiLogger 字段 | P0 | 0.5h | ✅ 完成 |
| T1.2 | OnStepChanged 添加自动日志记录 | P0 | 0.5h | ✅ 完成 |
| T1.3 | 移除子类 (HomeLogic 等) 重复的 _uiLogger 定义 | P0 | 0.5h | ✅ 完成 |
| T1.4 | 验证日志输出格式 | P1 | 0.5h | ✅ 完成 |

### Story 2: LogicRunner 异常自动报警

| ID | 任务 | 优先级 | 预估 | 状态 |
|----|------|--------|------|------|
| T2.1 | LogicRunner 添加 AlarmMessage 引用 | P0 | 0.5h | ✅ 完成 |
| T2.2 | 异常捕获处自动产生高级报警 | P0 | 1h | ✅ 完成 |
| T2.3 | 验证报警推送到 UI | P1 | 0.5h | ✅ 完成 |

### Story 3: 步骤超时自动报警

| ID | 任务 | 优先级 | 预估 | 状态 |
|----|------|--------|------|------|
| T3.1 | LogicBase 添加 CheckTimeout 封装方法 | P0 | 1h | ✅ 完成 |
| T3.2 | 超时时自动产生中级报警 | P0 | 0.5h | ✅ 完成 |
| T3.3 | 验证超时报警功能 | P1 | 0.5h | ✅ 完成 |

### Story 4: 集成测试与文档

| ID | 任务 | 优先级 | 预估 | 状态 |
|----|------|--------|------|------|
| T4.1 | 编译验证所有修改 | P0 | 0.5h | ✅ 完成 |
| T4.2 | 端到端测试验证 | P1 | 1h | ✅ 完成 |
| T4.3 | 更新 CLAUDE.md 文档 | P2 | 0.5h | ✅ 完成 |

---

## 详细任务描述

### T1.1: LogicBase 添加 protected _uiLogger 字段

**文件**: `EwanCommon\EwanCommon\EwanCore\StateMachine\Logic\LogicBase.cs`

**描述**: 在 LogicBase 基类中添加 protected UILogger 实例，供所有子类共享使用。

**验收标准**:
- [ ] 添加 `using EwanCommon.Logging;` 引用
- [ ] 添加 `protected readonly UILogger _uiLogger = new UILogger();` 字段
- [ ] 字段访问级别为 protected，子类可直接使用
- [ ] 编译通过无错误

**实现代码**:
```csharp
using EwanCommon.Logging;

public abstract class LogicBase
{
    // 新增：UILogger 供子类和 OnStepChanged 使用
    protected readonly UILogger _uiLogger = new UILogger();

    // ... 其他代码
}
```

---

### T1.3: 移除子类重复的 _uiLogger 定义

**文件**:
- `Ewan.Core\Logic\HomeLogic.cs` (第22行)
- 其他继承 LogicBase 并定义了 _uiLogger 的类

**描述**: 移除子类中重复定义的 _uiLogger 字段，统一使用基类的 protected 字段。

**验收标准**:
- [ ] 移除 HomeLogic 中的 `private readonly UILogger _uiLogger = new UILogger();`
- [ ] 检查其他子类是否有重复定义
- [ ] 子类中原有的 `_uiLogger.Info()` 等调用保持不变（自动使用基类字段）
- [ ] 编译通过无错误

**注意事项**:
- C# 中子类的 private 字段会隐藏基类的 protected 字段
- 移除子类字段后，子类代码自动使用基类的 _uiLogger
- 无需修改子类中的日志调用代码

---

### T1.2: OnStepChanged 添加自动日志记录

**文件**: `EwanCommon\EwanCommon\EwanCore\StateMachine\LogicBase.cs`

**描述**: 修改 OnStepChanged 方法，在步骤变化时自动记录日志到 UI 日志窗口。

**验收标准**:
- [ ] 每次步骤变化时自动记录日志
- [ ] 日志格式: `[{LogicName}] 步骤: {from} → {to}`
- [ ] 日志级别: Info
- [ ] 不影响现有事件发布逻辑

**实现代码**:
```csharp
protected virtual void OnStepChanged(string from, string to)
{
    // 新增：自动记录步骤变化日志
    _uiLogger.InfoRaw($"[{GetType().Name}] 步骤: {from} → {to}");

    var args = new StepChangedEventArgs(GetType().Name, from, to, DateTimeOffset.Now);
    StepChanged?.Invoke(this, args);
    MessageHub.Current.Publish(args);
}
```

---

### T1.3: 验证日志输出格式

**描述**: 验证步骤变化日志正确显示在 UI 日志窗口。

**验收标准**:
- [ ] 日志窗口显示步骤变化记录
- [ ] 日志格式正确（包含 Logic 名称、from、to）
- [ ] 日志级别显示为 Info（绿色）
- [ ] 日志时间戳正确

---

### T2.1: LogicRunner 添加 AlarmMessage 引用

**文件**: `EwanCommon\EwanCommon\EwanCore\StateMachine\LogicRunner.cs`

**描述**: 添加必要的 using 引用以支持报警消息发送。

**验收标准**:
- [ ] 添加 `using Ewan.Model.Messages;`
- [ ] 添加 `using EwanCommon.EwanCore.AlarmSystem;`
- [ ] 编译通过无错误

---

### T2.2: 异常捕获处自动产生高级报警

**文件**: `EwanCommon\EwanCommon\EwanCore\StateMachine\LogicRunner.cs`

**描述**: 在 LogicRunner 的异常捕获处添加自动报警逻辑。

**验收标准**:
- [ ] 流程异常时自动产生报警
- [ ] 报警级别: H (高级)
- [ ] NeedReset: true
- [ ] 报警 Key 格式: `Logic.Exception.{LogicName}`
- [ ] 报警内容包含异常信息

**实现代码**:
```csharp
catch (Exception ex)
{
    RunTag = RunTimeTag.Stop;

    // 新增：自动产生高级报警
    var alarmMsg = AlarmMessage.Create(
        key: $"Logic.Exception.{CurLogic?.GetType().Name ?? "Unknown"}",
        content: $"流程异常: [{CurLogic?.GetType().Name}] {ex.Message}",
        level: AlarmLevel.H,
        needReset: true);
    MessageHub.Current.Post(alarmMsg);

    // 原有逻辑
    errorArgs = new LogicExceptionEventArgs(CurLogic?.GetType().Name, SwitchIndex, ex);
    LogicException?.Invoke(this, errorArgs);
}
```

---

### T2.3: 验证报警推送到 UI

**描述**: 验证流程异常时报警正确显示在报警面板。

**验收标准**:
- [ ] 异常时报警出现在报警列表
- [ ] 报警显示为红色（高级）
- [ ] 三色灯切换到报警状态
- [ ] 报警内容包含 Logic 名称和异常信息

---

### T3.1: LogicBase 添加 CheckTimeout 封装方法

**文件**: `EwanCommon\EwanCommon\EwanCore\StateMachine\LogicBase.cs`

**描述**: 添加带自动报警功能的超时检查封装方法。

**验收标准**:
- [ ] 新增 CheckTimeout 方法
- [ ] 超时时自动产生中级报警
- [ ] 报警 Key 格式: `Logic.Timeout.{LogicName}.{StepName}`
- [ ] 返回值与原 TimeoutWatch 行为一致

**实现代码**:
```csharp
/// <summary>
/// 检查超时并自动产生报警
/// </summary>
/// <param name="timeoutMs">超时时间（毫秒）</param>
/// <param name="stepName">步骤名称（可选，默认使用 SwitchIndex）</param>
/// <returns>是否超时</returns>
protected bool CheckTimeout(int timeoutMs, string stepName = null)
{
    if (Tw.StartCheckIsTimeout(SwitchIndex, timeoutMs))
    {
        string step = stepName ?? SwitchIndex;
        var alarmMsg = AlarmMessage.Create(
            key: $"Logic.Timeout.{GetType().Name}.{step}",
            content: $"步骤超时: [{GetType().Name}] {step} 超过 {timeoutMs}ms",
            level: AlarmLevel.M,
            needReset: false);
        MessageHub.Current.Post(alarmMsg);

        _uiLogger.Warn($"[{GetType().Name}] 步骤 {step} 超时 ({timeoutMs}ms)");
        return true;
    }
    return false;
}
```

---

### T3.2: 超时时自动产生中级报警

**描述**: 确保 CheckTimeout 方法的报警逻辑正确实现。

**验收标准**:
- [ ] 报警级别: M (中级)
- [ ] NeedReset: false
- [ ] 报警通过 MessageHub.Post 发送
- [ ] 同时记录警告日志

---

### T3.3: 验证超时报警功能

**描述**: 验证步骤超时时报警正确触发和显示。

**验收标准**:
- [ ] 超时时报警出现在报警列表
- [ ] 报警显示为黄色（中级）
- [ ] 日志窗口显示警告信息
- [ ] 报警内容包含超时时间

---

### T4.1: 编译验证所有修改

**描述**: 确保所有修改后项目能正常编译。

**验收标准**:
- [ ] 解决方案编译通过
- [ ] 无编译错误
- [ ] 无严重警告

**执行命令**:
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln -p:Configuration=Debug -p:Platform=x64 -verbosity:minimal
```

---

### T4.2: 端到端测试验证

**描述**: 进行完整的功能测试验证。

**测试场景**:
1. 正常流程运行 → 步骤变化日志正确显示
2. 模拟流程异常 → 高级报警正确产生
3. 模拟步骤超时 → 中级报警正确产生
4. 清除报警后 → 系统恢复正常状态

**验收标准**:
- [ ] 所有测试场景通过
- [ ] 日志和报警显示正确
- [ ] 三色灯状态正确切换

---

### T4.3: 更新 CLAUDE.md 文档

**文件**: `CLAUDE.md`

**描述**: 更新项目文档，说明新增的日志和报警功能。

**需要更新的内容**:
- [ ] 添加 OnStepChanged 自动日志说明
- [ ] 添加 CheckTimeout 方法使用说明
- [ ] 添加报警自动推送说明

---

## 依赖关系图

```
T1.1 ──→ T1.2 ──→ T1.3
                    ↓
T2.1 ──→ T2.2 ──→ T2.3 ──→ T4.1 ──→ T4.2 ──→ T4.3
                    ↑
T3.1 ──→ T3.2 ──→ T3.3
```

---

## 关键文件清单

| 文件路径 | 修改类型 |
|----------|----------|
| `EwanCommon\EwanCommon\EwanCore\StateMachine\LogicBase.cs` | 修改 |
| `EwanCommon\EwanCommon\EwanCore\StateMachine\LogicRunner.cs` | 修改 |
| `CLAUDE.md` | 更新 |

---

## 风险与注意事项

1. **日志量**：高频步骤切换会产生大量日志，需确认 LogWindow 的 MaxLines 设置合理（建议 1000+）
2. **报警去重**：AlarmService 按 Key 去重，确保 Key 格式设计合理
3. **性能影响**：UILogger.InfoRaw 使用 Post() 异步发送，不会阻塞流程执行
4. **向后兼容**：原有 StepChanged 事件和 MessageHub.Publish 保持不变

---

## 进度跟踪

| Story | 完成任务 | 总任务 | 进度 |
|-------|---------|--------|------|
| Story 1: 自动日志 | 4 | 4 | 100% |
| Story 2: 异常报警 | 3 | 3 | 100% |
| Story 3: 超时报警 | 3 | 3 | 100% |
| Story 4: 集成测试 | 3 | 3 | 100% |
| **总计** | **13** | **13** | **100%** |

---

*文档创建时间: 2025-12-28*
*最后更新: 2025-12-28*
*完成日期: 2025-12-28*
