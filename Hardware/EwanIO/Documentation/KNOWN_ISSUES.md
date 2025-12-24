# EwanIO 已知问题与改进建议

## 概述

本文档记录了 EwanIO V2 库的已知问题、潜在风险和改进建议。

---

## 1. 性能相关

### 1.1 ~~Swap 操作仍有锁~~ (已修复)

**状态:** ✅ 已在最新版本中修复

**修复方案:**
使用 `Interlocked.Exchange` 实现无锁切换：
```csharp
internal void Swap()
{
    var oldFront = Interlocked.Exchange(ref _front, _back);
    _back = oldFront;
}
```

---

### 1.2 类型转换的装箱开销

**位置:** `IoContext.cs:32`

**问题描述:**
```csharp
private readonly object _command; // Command or CommandOptimized

// 每次使用需要类型转换
if (_useBulkWrite)
{
    ((CommandOptimized)_command).SetOutput(meta.Index, value);
}
```

**影响:** 低

**改进建议:**
使用接口或策略模式：
```csharp
private readonly ICommand _command;

interface ICommand
{
    void SetOutput(int index, bool value);
    bool GetOutput(int index);
    bool HasDirty { get; }
}
```

---

## 2. 线程安全

### 2.1 ~~边缘检测的竞态风险~~ (已修复)

**状态:** ✅ 已在最新版本中修复

**修复方案:**
- 将 `bool[]` 改为 `int[]`（0 = false, 1 = true）
- 使用 `Interlocked.Exchange` 实现原子读取并清除
- 使用 `Volatile.Write/Read` 确保内存可见性

```csharp
private readonly int[] _risingEdges;   // 使用 int 以支持 Interlocked
private readonly int[] _fallingEdges;  // 0 = false, 1 = true

public bool ReadAndClearRising(int logicalIndex)
{
    // 原子操作：读取当前值并设置为 0
    return Interlocked.Exchange(ref _risingEdges[logicalIndex], 0) != 0;
}
```

现在可以安全地在 Tick 线程更新边沿，在流程线程读取并清除。

---

## 3. 功能限制

### 3.1 缺少连接重连机制

**位置:** `IoContext.cs`

**问题描述:**
连接断开后没有自动重连逻辑，需要外部处理。

**影响:** 中等

**改进建议:**
添加自动重连机制：
```csharp
public class IoContextOptions
{
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectIntervalMs { get; set; } = 5000;
    public int MaxReconnectAttempts { get; set; } = 10;
}
```

---

### 3.2 缺少批量操作 API

**位置:** `IoContext.cs`

**问题描述:**
```csharp
// 目前只能逐点设置
ctx.On(0);
ctx.On(1);
ctx.On(2);
```

对外仅暴露 `On/Off/Pulse`，缺少一次性写入多个点的原子接口。

**影响:** 低

**改进建议:**
添加批量 API：
```csharp
// 批量设置多个点
void SetOutputs(int[] indices, bool value);
void SetOutputs(int[] indices, bool[] values);

// 范围设置
void SetOutputRange(int startIndex, int count, uint bitmask);
```

---

### 3.3 Wait/Confirm 响应延迟

**位置:** `IoContext.cs:485-507`

**问题描述:**
Wait/Confirm 依赖 Tick 轮询检查条件，响应延迟最大等于 Tick 周期。

**影响:** 低

**改进建议:**
- 对于时间敏感的操作，提供独立的快速检查线程
- 或使用事件驱动机制

---

### 3.4 索引越界静默返回默认值

**位置:** `Snapshot.cs`, `Command.cs`, `CommandOptimized.cs`, `MetaManager.cs`, `EdgeManager.cs`

**问题描述:**
`GetInput/GetOutput` 等索引 API 在越界时直接返回 `false`（或忽略写入），不抛异常也不记录日志，导致映射/布局配置错误被掩盖，排查困难。

**影响:** 中等

**状态:** ✅ 已修复

**修复方案:**
- 新增 `IndexOutOfRangeBehavior`（默认记录错误并触发 `HealthChanged`）
- 可选 `Throw` 直接抛异常，或 `Ignore` 保持旧行为

---

## 4. 设计假设

### 4.1 端口大小硬编码

**位置:** `CommandOptimized.cs:16`

**问题描述:**
```csharp
private const int BITS_PER_PORT = 32;  // 固定 32 位
```

有些 PLC 是 16 位端口，有些是 8 位，目前不支持配置。

**影响:** 低

**改进建议:**
通过硬件接口获取端口大小：
```csharp
interface IHardwareIOExtended
{
    int BitsPerPort { get; }  // 8, 16, 32
}
```

---

### 4.2 ~~跨端口映射不支持优化~~ (已修复)

**状态:** ✅ 已在最新版本中修复

**修复方案:**
- 新增 `_outputPhysicalToLogical` 反向映射数组
- 通用路径支持跨端口映射，正确计算物理端口值
- 快路径（1:1 映射）保持高性能

---

## 5. 资源管理

### 5.1 双缓冲内存开销

**位置:** `Snapshot.cs`

**问题描述:**
```csharp
_front = new Snapshot(inputCount, outputCount);
_back = new Snapshot(inputCount, outputCount);
// 内存 = 2 × (inputCount + outputCount) bytes
```

**影响:** 低（除非点数非常多）

**改进建议:**
对于超大规模 IO（>1000 点），考虑使用位压缩存储。

---

## 问题严重程度汇总

| 问题 | 严重程度 | 优先级 | 状态 |
|------|----------|--------|------|
| ~~边缘检测竞态风险~~ | 中 | P2 | ✅ 已修复 |
| 缺少自动重连 | 中 | P2 | 待实现 |
| ~~Swap 有锁~~ | 低 | P3 | ✅ 已修复 |
| 类型转换装箱 | 低 | P3 | 待优化 |
| 端口大小硬编码 | 低 | P3 | 待改进 |
| 缺少批量 API | 低 | P3 | 待实现 |
| Wait 响应延迟 | 低 | P3 | 可接受 |
| 索引越界静默返回默认值 | 中 | P2 | ✅ 已修复 |
| ~~跨端口映射~~ | 低 | P4 | ✅ 已修复 |
| 双缓冲内存 | 低 | P4 | 可接受 |

---

## 更新记录

| 日期 | 版本 | 更新内容 |
|------|------|----------|
| 2025-12-16 | V2.0 | 初始文档 |
| 2025-12-16 | V2.1 | 修复 Swap 无锁优化、跨端口映射支持、线程安全增强 |
| 2025-12-16 | V2.2 | 移除 Expression 解析开销（实际影响可忽略） |
| 2025-12-16 | V2.3 | 修复边缘检测竞态风险（使用 Interlocked.Exchange） |
