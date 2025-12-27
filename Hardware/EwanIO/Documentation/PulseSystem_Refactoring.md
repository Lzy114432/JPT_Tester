# EwanIO Pulse 脉冲系统重构文档

> **提交**: `3c69bb1b67d8a00fdba9bd3d4878472f11428e03`
> **日期**: 2025-12-27
> **作者**: hanHHHyU

---

## 概述

本次重构将 EwanIO 的脉冲（Pulse）系统从基于 Tick 周期的计数方式改为基于 `Stopwatch` 的绝对时间计时方式。这一改进解决了原有设计中脉冲时长受 Tick 周期波动影响的问题，提供了更精确、更可预测的脉冲控制能力。

### 核心改进

| 特性 | 旧版本 | 新版本 |
|------|--------|--------|
| **计时方式** | Tick 周期计数 | Stopwatch 绝对时间 |
| **时间单位** | Tick 数 | 毫秒 / TimeSpan |
| **精度** | 受 Tick 周期影响 | 高精度（毫秒级） |
| **完成通知** | 无 | 支持回调 |
| **异步支持** | 无 | `PulseAsync` |
| **同步等待** | 无 | `PulseAndWait` |
| **状态查询** | 无 | `IsPulseActive` / `GetPulseRemainingMs` |
| **取消能力** | 无 | `CancelPulse` |

---

## 架构变更

### 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Hardware/EwanIO/Core/Data/PulseOperation.cs` | 新增 | 脉冲操作核心类 |
| `Hardware/EwanIO/Core/Context/IoContext.cs` | 修改 | 替换脉冲字段为 `PulseManager` |
| `Hardware/EwanIO/Core/Context/IoContext.Output.cs` | 修改 | 重构 Pulse API |
| `Hardware/EwanIO/Core/Context/IoContext.Tick.cs` | 修改 | 简化脉冲更新逻辑 |

### 类图

```
┌─────────────────────────────────────────────────────────────┐
│                        IoContext<TLayout>                   │
├─────────────────────────────────────────────────────────────┤
│ - _pulseManager: PulseManager                               │
├─────────────────────────────────────────────────────────────┤
│ + Pulse(expr, durationMs, now, value, onCompleted)          │
│ + Pulse(index, durationMs, now, value, onCompleted)         │
│ + Pulse(expr, duration: TimeSpan, ...)                      │
│ + PulsePhysical(...)                                        │
│ + PulseAsync(...)                                           │
│ + PulseAndWait(...)                                         │
│ + IsPulseActive(index) : bool                               │
│ + GetPulseRemainingMs(index) : long                         │
│ + CancelPulse(index)                                        │
│ + PulseState : PulseManager [只读]                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ 包含
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        PulseManager                         │
├─────────────────────────────────────────────────────────────┤
│ - _operations: PulseOperation[]                             │
│ - _activeCount: int                                         │
├─────────────────────────────────────────────────────────────┤
│ + OutputCount : int                                         │
│ + ActiveCount : int                                         │
│ + HasActivePulse : bool                                     │
│ + this[index] : PulseOperation                              │
├─────────────────────────────────────────────────────────────┤
│ + Start(index, durationMs, endValue, onCompleted) : bool    │
│ + ForceStart(...)                                           │
│ + IsPulseActive(index) : bool                               │
│ + GetRemainingMs(index) : long                              │
│ + Cancel(index)                                             │
│ + CancelAll()                                               │
│ + Update(setOutput: Action<int,bool>)                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ 管理
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                       PulseOperation                        │
├─────────────────────────────────────────────────────────────┤
│ - _stopwatch: Stopwatch                                     │
│ - _durationMs: long                                         │
│ - _endValue: bool                                           │
│ - _onCompleted: Action<int>?                                │
│ - _isActive: int (原子操作)                                 │
├─────────────────────────────────────────────────────────────┤
│ + OutputIndex : int                                         │
│ + DurationMs : long                                         │
│ + EndValue : bool                                           │
│ + IsActive : bool                                           │
│ + ElapsedMs : long                                          │
│ + RemainingMs : long                                        │
│ + IsExpired : bool                                          │
├─────────────────────────────────────────────────────────────┤
│ + Start(durationMs, endValue, onCompleted) : bool           │
│ + ForceStart(...)                                           │
│ + TryComplete(out endValue) : bool                          │
│ + Cancel()                                                  │
│ + Reset()                                                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 新增类详解

### PulseOperation 类

**位置**: `Hardware/EwanIO/Core/Data/PulseOperation.cs`

`PulseOperation` 类表示单个输出通道的脉冲操作，使用 `System.Diagnostics.Stopwatch` 实现精确计时。

#### 核心特性

1. **精确计时**: 使用 `Stopwatch` 实现毫秒级精度，不受 Tick 周期影响
2. **线程安全**: 使用 `Interlocked` 和 `Volatile` 确保多线程安全
3. **回调支持**: 脉冲完成时可触发回调通知
4. **互斥启动**: 同一输出通道同时只能有一个脉冲在执行

#### 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `OutputIndex` | `int` | 输出通道索引 |
| `DurationMs` | `long` | 脉冲持续时间（毫秒） |
| `EndValue` | `bool` | 脉冲结束后的输出值 |
| `IsActive` | `bool` | 是否正在执行脉冲 |
| `ElapsedMs` | `long` | 已经过的时间（毫秒） |
| `RemainingMs` | `long` | 剩余时间（毫秒） |
| `IsExpired` | `bool` | 脉冲是否已到期 |

#### 方法说明

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `Start(durationMs, endValue, onCompleted)` | `bool` | 启动脉冲，已有脉冲执行时返回 `false` |
| `ForceStart(...)` | `void` | 强制启动脉冲，覆盖正在执行的脉冲 |
| `TryComplete(out endValue)` | `bool` | 检查并完成到期的脉冲，触发回调 |
| `Cancel()` | `void` | 取消脉冲（不触发回调，不改变输出） |
| `Reset()` | `void` | 重置脉冲状态 |

### PulseManager 类

**位置**: `Hardware/EwanIO/Core/Data/PulseOperation.cs`

`PulseManager` 类管理多个输出通道的脉冲操作，提供统一的脉冲控制接口。

#### 核心特性

1. **批量管理**: 管理所有输出通道的脉冲操作
2. **活跃计数**: 跟踪当前活跃脉冲数量，支持快路径优化
3. **统一更新**: 在 Tick 循环中统一更新所有脉冲状态

#### 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| `OutputCount` | `int` | 输出通道数量 |
| `ActiveCount` | `int` | 活跃脉冲数量 |
| `HasActivePulse` | `bool` | 是否有活跃脉冲 |
| `this[index]` | `PulseOperation` | 获取指定通道的脉冲操作 |

#### 方法说明

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `Start(index, durationMs, endValue, onCompleted)` | `bool` | 启动指定通道的脉冲 |
| `ForceStart(...)` | `void` | 强制启动脉冲 |
| `IsPulseActive(index)` | `bool` | 检查指定通道是否有脉冲执行 |
| `GetRemainingMs(index)` | `long` | 获取指定通道脉冲剩余时间 |
| `Cancel(index)` | `void` | 取消指定通道的脉冲 |
| `CancelAll()` | `void` | 取消所有脉冲 |
| `Update(setOutput)` | `void` | 更新所有脉冲状态（Tick 调用） |

---

## API 变更

### Pulse 方法签名变更

#### 旧版本（已废弃）

```csharp
// 基于 Tick 计数
void Pulse(Expression<Func<TLayout, OutputSignal>> expr, int durationTicks, bool now = false, bool value = true)
void Pulse(int index, int durationTicks, bool now = false, bool value = true)
```

#### 新版本

```csharp
// 基于毫秒
void Pulse(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, Action<int>? onCompleted = null)
void Pulse(int index, int durationMs, bool now = false, bool value = true, Action<int>? onCompleted = null)

// 基于 TimeSpan
void Pulse(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, Action<int>? onCompleted = null)
void Pulse(int index, TimeSpan duration, bool now = false, bool value = true, Action<int>? onCompleted = null)
```

### 新增 API

#### 异步等待方法

```csharp
// 异步等待脉冲完成
Task<bool> PulseAsync(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, CancellationToken cancellationToken = default)
Task<bool> PulseAsync(int index, int durationMs, bool now = false, bool value = true, CancellationToken cancellationToken = default)
Task<bool> PulseAsync(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, CancellationToken cancellationToken = default)
```

#### 同步等待方法

```csharp
// 同步等待脉冲完成（阻塞当前线程）
bool PulseAndWait(Expression<Func<TLayout, OutputSignal>> expr, int durationMs, bool now = false, bool value = true, int timeoutMs = -1)
bool PulseAndWait(int index, int durationMs, bool now = false, bool value = true, int timeoutMs = -1)
bool PulseAndWait(Expression<Func<TLayout, OutputSignal>> expr, TimeSpan duration, bool now = false, bool value = true, int timeoutMs = -1)
```

#### 状态查询方法

```csharp
// 检查脉冲是否活跃
bool IsPulseActive(int index)
bool IsPulseActive(Expression<Func<TLayout, OutputSignal>> expr)

// 获取脉冲剩余时间（毫秒）
long GetPulseRemainingMs(int index)

// 取消脉冲（不改变输出值）
void CancelPulse(int index)
void CancelPulse(Expression<Func<TLayout, OutputSignal>> expr)
```

#### 状态访问属性

```csharp
// 获取脉冲管理器（用于高级状态查询）
PulseManager PulseState { get; }
```

---

## 使用示例

### 基本脉冲输出

```csharp
// 输出 500ms 脉冲
ioContext.Pulse(io => io.Cylinder1, 500);

// 使用 TimeSpan
ioContext.Pulse(io => io.Cylinder1, TimeSpan.FromMilliseconds(500));

// 立即下发（不等待 Tick）
ioContext.Pulse(io => io.Cylinder1, 500, now: true);

// 输出 OFF->ON 脉冲（默认是 ON->OFF）
ioContext.Pulse(io => io.Cylinder1, 500, value: false);
```

### 带回调的脉冲

```csharp
// 脉冲完成时触发回调
ioContext.Pulse(io => io.Cylinder1, 500, onCompleted: index =>
{
    Console.WriteLine($"输出 {index} 脉冲完成");
});
```

### 异步等待脉冲完成

```csharp
// 在 async 方法中等待脉冲完成
public async Task ExecuteSequenceAsync()
{
    // 等待气缸伸出脉冲完成
    bool success = await ioContext.PulseAsync(io => io.Cylinder1, 500);

    if (success)
    {
        // 继续下一步
        await ioContext.PulseAsync(io => io.Cylinder2, 300);
    }
}

// 支持取消
public async Task ExecuteWithCancellationAsync(CancellationToken ct)
{
    bool success = await ioContext.PulseAsync(io => io.Cylinder1, 500, cancellationToken: ct);
    // 如果取消，success 为 false
}
```

### 同步等待脉冲完成

```csharp
// 阻塞等待脉冲完成
bool success = ioContext.PulseAndWait(io => io.Cylinder1, 500);

// 带超时的等待
bool success = ioContext.PulseAndWait(io => io.Cylinder1, 500, timeoutMs: 1000);
```

### 脉冲状态查询

```csharp
// 检查是否有脉冲正在执行
if (ioContext.IsPulseActive(io => io.Cylinder1))
{
    // 获取剩余时间
    long remaining = ioContext.GetPulseRemainingMs(0);
    Console.WriteLine($"脉冲剩余时间: {remaining}ms");
}

// 取消正在执行的脉冲
ioContext.CancelPulse(io => io.Cylinder1);
```

### 物理值脉冲（绕过 NO/NC 映射）

```csharp
// 直接控制物理输出，忽略 NO/NC 配置
ioContext.PulsePhysical(io => io.Valve1, 500);

// 同样支持 TimeSpan 和回调
ioContext.PulsePhysical(io => io.Valve1, TimeSpan.FromSeconds(1), onCompleted: _ =>
{
    Console.WriteLine("物理脉冲完成");
});
```

---

## 迁移指南

### 从 Tick 计数迁移到毫秒

#### 计算公式

```
durationMs = durationTicks × tickIntervalMs
```

其中 `tickIntervalMs` 是 Tick 周期（毫秒），通常为 10ms。

#### 迁移示例

```csharp
// 旧代码：10 个 Tick（假设 Tick 周期为 10ms = 100ms）
ioContext.Pulse(io => io.Cylinder1, 10);

// 新代码：直接使用毫秒
ioContext.Pulse(io => io.Cylinder1, 100);

// 或使用 TimeSpan 更清晰
ioContext.Pulse(io => io.Cylinder1, TimeSpan.FromMilliseconds(100));
```

### 迁移检查清单

- [ ] 将所有 `Pulse` 调用的 `durationTicks` 参数转换为毫秒
- [ ] 将所有 `PulsePhysical` 调用的参数转换为毫秒
- [ ] 考虑使用 `TimeSpan` 重载提高代码可读性
- [ ] 评估是否需要使用新增的回调功能
- [ ] 评估是否需要使用异步等待功能

---

## 内部实现细节

### Tick 循环中的脉冲更新

```csharp
// IoContext.Tick.cs
protected virtual void OnTick()
{
    // ... 其他 Tick 逻辑 ...

    // 5. 下发 dirty 输出
    FlushOutputsIfDirty();

    // 6. 更新脉冲状态（使用绝对时间计时）
    _pulseManager.Update(SetOutputInternal);

    // 7. 递增 tick 计数器
    Interlocked.Increment(ref _tickCounter);
}
```

### PulseManager.Update 快路径优化

```csharp
public void Update(Action<int, bool> setOutput)
{
    // 快路径：无活跃脉冲时直接返回
    if (Volatile.Read(ref _activeCount) == 0)
        return;

    for (int i = 0; i < OutputCount; i++)
    {
        if (_operations[i].TryComplete(out bool endValue))
        {
            setOutput(i, endValue);
            Interlocked.Decrement(ref _activeCount);
        }
    }
}
```

### 线程安全保证

1. **原子状态检查**: 使用 `Interlocked.CompareExchange` 确保脉冲启动的原子性
2. **Volatile 读写**: 使用 `Volatile.Read/Write` 确保跨线程可见性
3. **回调异常隔离**: 回调异常被捕获并忽略，防止影响 Tick 循环

---

## 性能考量

### 优势

1. **精确计时**: 不再受 Tick 周期波动影响
2. **快路径优化**: 无活跃脉冲时几乎零开销
3. **减少状态同步**: 不需要每个 Tick 都更新计数器

### 注意事项

1. **回调执行**: 回调在 Tick 线程中同步执行，应保持简短
2. **异步等待**: `PulseAsync` 使用 `TaskCompletionSource`，有少量 GC 压力
3. **Stopwatch 精度**: 在极短脉冲（<1ms）场景下，Stopwatch 精度可能不足

---

## 向后兼容性

### 破坏性变更

1. **参数语义变更**: `Pulse` 方法的第二个参数从 `durationTicks` 变为 `durationMs`
2. **移除内部字段**: `_pulseRemainingTicks` 和 `_pulseEndValues` 已被移除
3. **新增公开属性**: `PulseState` 属性暴露了 `PulseManager`

### 迁移风险

- **低风险**: API 签名兼容（参数类型相同），但语义不同
- **建议**: 全局搜索 `Pulse(` 和 `PulsePhysical(` 调用，逐一验证时间参数

---

## 测试建议

### 单元测试场景

1. 基本脉冲功能测试
2. 并发脉冲互斥测试
3. 脉冲取消测试
4. 回调触发测试
5. 异步等待测试
6. 超时处理测试

### 集成测试场景

1. Tick 循环中的脉冲更新
2. 多输出通道并发脉冲
3. 物理值脉冲与 NO/NC 映射

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0.0 | 2025-12-27 | 初始版本：Stopwatch 精确计时、回调支持、异步等待 |

---

## 相关文件

- `Hardware/EwanIO/Core/Data/PulseOperation.cs` - 脉冲操作核心类
- `Hardware/EwanIO/Core/Context/IoContext.cs` - IoContext 主文件
- `Hardware/EwanIO/Core/Context/IoContext.Output.cs` - 输出控制方法
- `Hardware/EwanIO/Core/Context/IoContext.Tick.cs` - Tick 循环逻辑
