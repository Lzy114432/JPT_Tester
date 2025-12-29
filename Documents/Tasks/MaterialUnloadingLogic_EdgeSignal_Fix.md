# MaterialUnloadingLogic 边沿信号覆盖问题修复

## 问题背景

在代码审计中发现，`RisingEdge` 是一个脉冲信号（只在边沿发生的那一刻为 `true`），但当前实现存在信号被覆盖的风险。

### 问题时序

```
T1: RingLineModule 检测到上升沿, Post(RisingEdge=true)
T2: OnRingLineData 设置 _ringLineRisingEdge = true
T3: RingLineModule 下一周期, Post(RisingEdge=false)  ← 200ms后
T4: OnRingLineData 设置 _ringLineRisingEdge = false  ← 覆盖！
T5: Handler() 执行，看到 _ringLineRisingEdge = false  ← 错过边沿
```

### 影响范围

- `Ewan.Core\Logic\MaterialUnloadingLogic.cs`

---

## 任务清单

### Task 1: 修改消息订阅回调

**状态**: `待完成`
**预估时间**: 10 分钟
**优先级**: P0

#### 描述

修改 `OnRingLineData` 方法，使用"或"逻辑防止边沿信号被覆盖。

#### 验收标准

- [ ] 边沿信号只能被设置为 `true`，不会被后续消息覆盖为 `false`
- [ ] 其他字段（`_emptyCount`, `_cuttingBridgeCarCount`）正常更新
- [ ] 编译通过

#### 实现细节

**修改前：**
```csharp
private void OnRingLineData(RingLineDataMessage msg)
{
    _ringLineRisingEdge = msg.RisingEdge;  // 可能被覆盖
    _emptyCount = msg.EmptyCarCount;
    _cuttingBridgeCarCount = msg.CuttingBridgeCarCount;
}
```

**修改后：**
```csharp
private void OnRingLineData(RingLineDataMessage msg)
{
    // 边沿信号使用"或"逻辑，防止被后续消息覆盖
    if (msg.RisingEdge)
    {
        _ringLineRisingEdge = true;
    }
    _emptyCount = msg.EmptyCarCount;
    _cuttingBridgeCarCount = msg.CuttingBridgeCarCount;
}
```

---

### Task 2: 修改初始状态逻辑

**状态**: `待完成`
**预估时间**: 10 分钟
**优先级**: P0
**依赖**: Task 1

#### 描述

在初始状态消费边沿信号后，立即清除标志，确保下次能正确检测新的边沿。

#### 验收标准

- [ ] 检测到边沿后立即清除 `_ringLineRisingEdge`
- [ ] 不影响后续状态流转
- [ ] 编译通过

#### 实现细节

**修改前：**
```csharp
case "初始状态":
    // 检测环线信号，有信号才切换步骤
    if (_ringLineRisingEdge)
    {
        SwitchIndex = "检查环线信号";
    }
    else
    {
        // 无信号时直接标记完成，不切换步骤，避免日志刷屏
        IsFinish = true;
    }
    break;
```

**修改后：**
```csharp
case "初始状态":
    // 检测环线上升沿，有边沿才切换步骤
    if (_ringLineRisingEdge)
    {
        _ringLineRisingEdge = false;  // 消费后清除，准备下一次检测
        SwitchIndex = "检查环线信号";
    }
    else
    {
        // 无上升沿时直接标记完成，等待下一个触发周期
        IsFinish = true;
    }
    break;
```

---

### Task 3: 更新注释

**状态**: `待完成`
**预估时间**: 5 分钟
**优先级**: P1
**依赖**: Task 2

#### 描述

更新相关注释，确保与新逻辑一致。

#### 验收标准

- [ ] 注释准确描述边沿检测逻辑
- [ ] 移除过时的注释内容

#### 实现细节

更新 `OnRingLineData` 方法的注释：
```csharp
/// <summary>
/// 处理环线数据消息
/// </summary>
/// <remarks>
/// 边沿信号使用"或"逻辑锁存，防止被后续消息覆盖。
/// 边沿信号在 Handler() 的初始状态中消费后清除。
/// </remarks>
private void OnRingLineData(RingLineDataMessage msg)
```

---

### Task 4: 编译验证

**状态**: `待完成`
**预估时间**: 5 分钟
**优先级**: P0
**依赖**: Task 3

#### 描述

编译项目，确保无错误和警告。

#### 验收标准

- [ ] 编译成功
- [ ] 无编译错误
- [ ] 无相关警告

#### 执行命令

```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" "F:\MarkingMachineFeeder\MarkingMachineFeeder.sln" -t:Build -p:Configuration=Debug -p:Platform=x64 -verbosity:minimal
```

---

### Task 5: 功能测试

**状态**: `待完成`
**预估时间**: 20 分钟
**优先级**: P0
**依赖**: Task 4

#### 描述

测试修复后的边沿检测功能，验证信号不会被覆盖。

#### 验收标准

- [ ] 环线信号 0→1 时触发流程
- [ ] 快速连续的消息不会覆盖边沿信号
- [ ] 流程完成后能正常响应新的边沿
- [ ] ForceCleanup 后能正常响应新边沿

#### 测试场景

| 场景 | 预期结果 |
|:-----|:---------|
| 正常边沿 0→1 | 触发一次流程 |
| 边沿后快速收到 false | 不影响，仍触发流程 |
| 流程完成后新边沿 | 触发新流程 |
| ForceCleanup 后边沿 | 触发新流程 |

---

## 任务依赖图

```
Task 1 (修改回调)
    │
    ▼
Task 2 (修改初始状态)
    │
    ▼
Task 3 (更新注释)
    │
    ▼
Task 4 (编译验证)
    │
    ▼
Task 5 (功能测试)
```

---

## 总时间预估

| 任务类型 | 时间 |
|:---------|:-----|
| 代码修改 | ~25 分钟 |
| 编译测试 | ~25 分钟 |
| **总计** | **~50 分钟** |

---

## 技术说明

### 为什么需要"或"逻辑锁存？

```
消息发布频率: RingLineModule 每 200ms 发布一次
状态机执行频率: LogicRunner 每 50ms 执行一次

时序风险:
┌────────────────────────────────────────────────────────────┐
│ 0ms    : RingLineModule 检测到边沿, RisingEdge=true        │
│ 10ms   : OnRingLineData 收到消息, _ringLineRisingEdge=true │
│ 50ms   : Handler 还未执行到（可能在处理其他逻辑）            │
│ 200ms  : RingLineModule 下一周期, RisingEdge=false         │
│ 210ms  : OnRingLineData 收到消息, _ringLineRisingEdge=false│ ← 覆盖！
│ 250ms  : Handler 执行初始状态，看到 false                   │ ← 错过！
└────────────────────────────────────────────────────────────┘

修复后:
┌────────────────────────────────────────────────────────────┐
│ 0ms    : RingLineModule 检测到边沿, RisingEdge=true        │
│ 10ms   : OnRingLineData 收到消息, _ringLineRisingEdge=true │
│ 200ms  : RingLineModule 下一周期, RisingEdge=false         │
│ 210ms  : OnRingLineData 收到消息, if(false) 不执行         │ ← 保持 true
│ 250ms  : Handler 执行初始状态，看到 true，清除后切换步骤    │ ← 正确触发
└────────────────────────────────────────────────────────────┘
```

### 模式名称

这种模式称为**边沿锁存（Edge Latching）**，常用于工业自动化中处理脉冲信号。

---

## 回滚方案

如果修复后出现问题，可通过 Git 回滚：

```bash
git checkout -- Ewan.Core/Logic/MaterialUnloadingLogic.cs
```
