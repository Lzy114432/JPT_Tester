# MaterialUnloadingLogic 边沿检测重构任务

## 概述

将 `MaterialUnloadingLogic` 中的边沿检测从本地标志 `_requestProcessed` 重构为使用消息字段 `RingLineDataMessage.RisingEdge`。

### 重构目标

| 目标 | 说明 |
|:-----|:-----|
| 单一职责 | 边沿检测由数据生产者（RingLineModule）负责 |
| 代码简化 | 移除冗余状态管理，减少维护点 |
| 一致性 | 所有消费者使用相同的边沿信号 |

### 影响范围

- `Ewan.Core\Logic\MaterialUnloadingLogic.cs`

---

## 任务清单

### Task 1: 添加边沿信号字段

**状态**: `待完成`
**预估时间**: 15 分钟
**优先级**: P0

#### 描述

在 `MaterialUnloadingLogic` 中添加用于存储消息边沿信号的字段。

#### 验收标准

- [ ] 添加 `_ringLineRisingEdge` 字段
- [ ] 字段初始值为 `false`
- [ ] 编译通过

#### 实现细节

```csharp
// 在 #region 私有字段 中添加
private bool _ringLineRisingEdge = false;
```

---

### Task 2: 修改消息订阅回调

**状态**: `待完成`
**预估时间**: 15 分钟
**优先级**: P0
**依赖**: Task 1

#### 描述

修改 `OnRingLineData` 方法，使用消息中的 `RisingEdge` 字段。

#### 验收标准

- [ ] 在回调中更新 `_ringLineRisingEdge`
- [ ] 保留其他字段更新（`_emptyCount`, `_cuttingBridgeCarCount`）
- [ ] 可选：移除不再需要的 `_ringLineSignal` 更新

#### 实现细节

```csharp
private void OnRingLineData(RingLineDataMessage msg)
{
    _ringLineRisingEdge = msg.RisingEdge;  // 新增
    _emptyCount = msg.EmptyCarCount;
    _cuttingBridgeCarCount = msg.CuttingBridgeCarCount;
}
```

---

### Task 3: 重构初始状态逻辑

**状态**: `待完成`
**预估时间**: 20 分钟
**优先级**: P0
**依赖**: Task 2

#### 描述

简化 `case "初始状态"` 的判断逻辑，使用 `_ringLineRisingEdge` 替代复杂的条件判断。

#### 验收标准

- [ ] 移除 `_ringLineSignal && !_requestProcessed` 条件
- [ ] 使用 `_ringLineRisingEdge` 判断
- [ ] 移除下降沿清理逻辑（`!_ringLineSignal && _requestProcessed`）
- [ ] 保持 `IsFinish = true` 分支

#### 实现细节

**修改前：**
```csharp
case "初始状态":
    if (_ringLineSignal && !_requestProcessed)
    {
        SwitchIndex = "检查环线信号";
    }
    else
    {
        if (!_ringLineSignal && _requestProcessed)
        {
            _requestProcessed = false;
        }
        IsFinish = true;
    }
    break;
```

**修改后：**
```csharp
case "初始状态":
    if (_ringLineRisingEdge)
    {
        SwitchIndex = "检查环线信号";
    }
    else
    {
        IsFinish = true;
    }
    break;
```

---

### Task 4: 简化检查环线信号步骤

**状态**: `待完成`
**预估时间**: 10 分钟
**优先级**: P0
**依赖**: Task 3

#### 描述

移除 `case "检查环线信号"` 中的 `_requestProcessed = true` 设置。

#### 验收标准

- [ ] 移除 `_requestProcessed = true;`
- [ ] 保留 `SwitchIndex = "检查料仓有料";`

#### 实现细节

**修改前：**
```csharp
case "检查环线信号":
    _requestProcessed = true;
    SwitchIndex = "检查料仓有料";
    break;
```

**修改后：**
```csharp
case "检查环线信号":
    SwitchIndex = "检查料仓有料";
    break;
```

---

### Task 5: 清理 Rset 方法

**状态**: `待完成`
**预估时间**: 10 分钟
**优先级**: P1
**依赖**: Task 4

#### 描述

从 `Rset()` 方法中移除 `_requestProcessed` 相关代码（如果还存在）。

#### 验收标准

- [ ] 确认 `_requestProcessed` 相关代码已移除
- [ ] `Rset()` 不包含边沿检测相关重置

#### 实现细节

```csharp
public override void Rset()
{
    _beltStopRequested = false;
    // 移除: _requestProcessed = false;
    _selectedBin = 1;
    _lastScannedQrCode = string.Empty;
    _scanRetryCount = 0;
    _hasMaterial = true;
    _materialCheckTask = null;
    base.Rset();
}
```

---

### Task 6: 清理 ForceCleanup 方法

**状态**: `待完成`
**预估时间**: 15 分钟
**优先级**: P1
**依赖**: Task 5

#### 描述

简化 `ForceCleanup()` 方法，移除不必要的边沿状态重置。

#### 验收标准

- [ ] 移除 `_ringLineSignal = false;`
- [ ] 移除 `_requestProcessed = false;`
- [ ] 保留其他清理逻辑（IO 清理、皮带控制等）

#### 实现细节

**修改前：**
```csharp
public void ForceCleanup(string reason)
{
    // ... IO 清理 ...

    _ringLineSignal = false;     // 移除
    _requestProcessed = false;   // 移除

    // ... 皮带控制 ...
    Rset();
}
```

**修改后：**
```csharp
public void ForceCleanup(string reason)
{
    // ... IO 清理 ...

    // 边沿状态由消息更新，无需手动清理

    // ... 皮带控制 ...
    Rset();
}
```

---

### Task 7: 移除废弃字段

**状态**: `待完成`
**预估时间**: 10 分钟
**优先级**: P1
**依赖**: Task 6

#### 描述

移除不再使用的私有字段。

#### 验收标准

- [ ] 移除 `_requestProcessed` 字段声明
- [ ] 可选：评估是否仍需要 `_ringLineSignal` 字段
- [ ] 编译通过，无警告

#### 实现细节

```csharp
#region 私有字段

// 移除: private bool _ringLineSignal = false;
// 移除: private bool _requestProcessed = false;
private bool _ringLineRisingEdge = false;  // 新增

// ... 其他字段 ...

#endregion
```

---

### Task 8: 编译验证

**状态**: `待完成`
**预估时间**: 10 分钟
**优先级**: P0
**依赖**: Task 7

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

### Task 9: 功能测试

**状态**: `待完成`
**预估时间**: 30 分钟
**优先级**: P0
**依赖**: Task 8

#### 描述

测试重构后的边沿检测功能。

#### 验收标准

- [ ] 环线信号 0→1 时触发流程
- [ ] 环线信号持续为 1 时不重复触发
- [ ] 流程完成后，信号保持为 1 不再次触发
- [ ] 信号变为 0 后再变为 1 时正常触发
- [ ] ForceCleanup 后能正常响应新信号

#### 测试场景

| 场景 | 预期结果 |
|:-----|:---------|
| 信号 0→1 | 触发一次流程 |
| 信号持续 1 | 不重复触发 |
| 信号 1→0→1 | 触发新流程 |
| ForceCleanup 后 0→1 | 触发新流程 |

---

## 任务依赖图

```
Task 1 (添加字段)
    │
    ▼
Task 2 (修改回调)
    │
    ▼
Task 3 (重构初始状态)
    │
    ▼
Task 4 (简化检查环线)
    │
    ▼
Task 5 (清理 Rset)
    │
    ▼
Task 6 (清理 ForceCleanup)
    │
    ▼
Task 7 (移除废弃字段)
    │
    ▼
Task 8 (编译验证)
    │
    ▼
Task 9 (功能测试)
```

---

## 总时间预估

| 任务类型 | 时间 |
|:---------|:-----|
| 代码修改 | ~1.5 小时 |
| 编译测试 | ~40 分钟 |
| **总计** | **~2 小时** |

---

## 回滚方案

如果重构后出现问题，可通过 Git 回滚：

```bash
git checkout -- Ewan.Core/Logic/MaterialUnloadingLogic.cs
```

---

## 参考资料

- `RingLineModule.cs` - 边沿检测实现
- `RingLineDataMessage.cs` - 消息定义
- `CLAUDE.md` - 项目开发指南
