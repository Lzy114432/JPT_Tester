# MainLogic 串行轮询重构计划

> **状态**: ✅ 已完成
> **完成日期**: 2025-12-28
> **代码变更**: -747 行 / +134 行

## 完成摘要

| 任务 | 状态 | 说明 |
|------|------|------|
| T1.1 MainLogic 重构 | ✅ | 改为装料流程↔下料流程串行轮询 |
| T2.1 MaterialLoadingLogic 简化 | ✅ | 移除 TryStartLoading/FinishProcess |
| T3.1 MaterialUnloadingLogic 简化 | ✅ | 移除优先级/流程锁调用 |
| T4.1 ProductionLineSharedState 精简 | ✅ | 删除 ActiveProcess/互斥锁方法 |
| T5.1 更新 SharedState 测试 | ✅ | 删除旧互斥锁测试 |
| T5.2 更新 MainLogic 测试 | ✅ | 4个测试覆盖串行轮询逻辑 |

---

## 1. 概述

### 1.1 重构目标

将 `MainLogic` 从**并行检测**模式重构为**串行轮询**模式，简化共享状态管理。

### 1.2 当前架构

```
MainLogic.Handler()
├── if (_loadingEnabled)
│   └── _loadingLogic.Handler()  ← 每次循环都调用
├── if (_unloadingEnabled)
│   └── _unloadingLogic.Handler() ← 每次循环都调用
└── 两者通过 ProductionLineSharedState 互斥
```

**问题**：
- 需要复杂的流程互斥锁 (`TryStartLoading`/`TryStartUnloading`)
- 需要优先级请求机制 (`RequestUnloadingPriority`)
- `ProductionLineSharedState` 承载过多职责

### 1.3 目标架构

```
MainLogic.Handler()
├── case "装料流程"
│   └── _loadingLogic.Handler()
│   └── 完成后 → SwitchIndex = "下料流程"
├── case "下料流程"
│   └── _unloadingLogic.Handler()
│   └── 完成后 → SwitchIndex = "装料流程"
└── 串行切换，天然互斥
```

**优势**：
- 无需流程互斥锁
- 无需优先级请求机制
- `ProductionLineSharedState` 职责精简

---

## 2. 影响范围分析

### 2.1 涉及文件

| 文件 | 修改类型 | 说明 |
|------|----------|------|
| `Ewan.Core\Logic\MainLogic.cs` | 重构 | 改为串行轮询 |
| `Ewan.Core\Logic\MaterialLoadingLogic.cs` | 简化 | 移除流程锁调用 |
| `Ewan.Core\Logic\MaterialUnloadingLogic.cs` | 简化 | 移除优先级/流程锁调用 |
| `Ewan.Core\Module\ProductionLineSharedState.cs` | 精简 | 删除互斥锁相关代码 |
| `Ewan.Core.Tests\*` | 更新 | 调整相关测试 |

### 2.2 ProductionLineSharedState 变更

#### 可删除的功能

| 功能 | 代码位置 | 删除原因 |
|------|----------|----------|
| `TryStartLoading()` | 228-239 | 串行执行无需互斥 |
| `TryStartUnloading()` | 245-256 | 串行执行无需互斥 |
| `FinishProcess()` | 261-267 | 无需释放锁 |
| `GetCurrentProcess()` | 273-279 | 无需跟踪当前流程 |
| `IsLoading()` | 285-291 | 无需查询流程状态 |
| `IsUnloading()` | 297-303 | 无需查询流程状态 |
| `_currentProcess` 字段 | 47 | 无需流程状态 |
| `ActiveProcess` 枚举 | 16-32 | 无需流程类型 |
| `RequestUnloadingPriority()` | 349-355 | MainLogic 直接控制 |
| `ClearUnloadingPriority()` | 360-366 | 同上 |
| `HasUnloadingPriorityRequest()` | 371-377 | 同上 |
| `_unloadingPriorityRequested` 字段 | 57 | 同上 |

#### 仍需保留的功能

| 功能 | 代码位置 | 保留原因 |
|------|----------|----------|
| `SetLoadingCompleted()` | 113-119 | BinElevatorModule 触发完成信号 |
| `GetLoadingCompleted()` | 125-131 | MaterialLoadingLogic 检查完成 |
| `SetUnloadingCompleted()` | 137-143 | BinElevatorModule 触发完成信号 |
| `GetUnloadingCompleted()` | 149-155 | MaterialUnloadingLogic 检查完成 |
| `IsSystemPaused()` | 161-167 | ProductionLineOperator 控制暂停 |
| `SetSystemPaused()` | 173-179 | 同上 |
| `RequireReinit()` | 185-191 | BinElevatorModule 检查重初始化 |
| `SetRequireReinit()` | 197-203 | ProductionLineOperator 触发 |
| `ResetAllStates()` | 208-218 | 系统重置 |
| `MarkLoadingInProgress()` | 312-318 | 装料流程标记 |
| `ClearLoadingInProgress()` | 323-329 | 装料流程清理 |

---

## 3. 任务分解

### Epic 1: MainLogic 重构

#### T1.1 重构 MainLogic.Handler()

**描述**: 将并行调用改为串行轮询的 case 分支

**当前代码** (`MainLogic.cs:46-83`):
```csharp
public override void Handler()
{
    switch (SwitchIndex)
    {
        case "初始状态":
            RefreshModuleConfiguration();
            _loadingLogic.Rset();
            _unloadingLogic.Rset();
            SwitchIndex = "主动作";
            break;

        case "主动作":
            RefreshModuleConfiguration();

            if (_loadingEnabled)
            {
                _loadingLogic.Handler();
                if (_loadingLogic.IsFinish)
                {
                    _loadingLogic.Rset();
                }
            }

            if (_unloadingEnabled)
            {
                _unloadingLogic.Handler();
                if (_unloadingLogic.IsFinish)
                {
                    _unloadingLogic.Rset();
                }
            }
            break;

        case "结束状态":
            Complete();
            break;
    }
}
```

**目标代码**:
```csharp
public override void Handler()
{
    switch (SwitchIndex)
    {
        case "初始状态":
            RefreshModuleConfiguration();
            _loadingLogic.Rset();
            _unloadingLogic.Rset();
            SwitchIndex = "装料流程";
            break;

        case "装料流程":
            RefreshModuleConfiguration();

            if (_loadingEnabled)
            {
                _loadingLogic.Handler();
                if (_loadingLogic.IsFinish)
                {
                    _loadingLogic.Rset();
                    SwitchIndex = "下料流程";
                }
            }
            else
            {
                SwitchIndex = "下料流程";
            }
            break;

        case "下料流程":
            RefreshModuleConfiguration();

            if (_unloadingEnabled)
            {
                _unloadingLogic.Handler();
                if (_unloadingLogic.IsFinish)
                {
                    _unloadingLogic.Rset();
                    SwitchIndex = "装料流程";
                }
            }
            else
            {
                SwitchIndex = "装料流程";
            }
            break;

        case "结束状态":
            Complete();
            break;
    }
}
```

**验收标准**:
- [ ] 编译通过
- [ ] 装料完成后自动切换到下料检查
- [ ] 下料完成后自动回到装料检查

---

### Epic 2: MaterialLoadingLogic 简化

#### T2.1 移除流程锁调用

**描述**: 删除 `TryStartLoading()` 和 `FinishProcess()` 调用

**修改位置**:

1. `ProcessCheckPreconditions()` (第153-172行):
```csharp
// 删除以下代码:
if (_sharedState.HasUnloadingPriorityRequest())
{
    _uiLogger.DebugRaw("检测到下料优先级请求，等待下料完成: {0}", "MaterialLoadingLogic");
    return;
}

if (!_sharedState.TryStartLoading())
{
    _uiLogger.DebugRaw("无法获取流程锁，等待中: {0}", "MaterialLoadingLogic");
    return;
}

// 简化为:
SwitchIndex = "等待料片信号";
Tw.StartWatch(SwitchIndex);
```

2. `ProcessCleanup()` (第360行):
```csharp
// 删除:
_sharedState.FinishProcess();
```

3. `ForceCleanup()` (第406行):
```csharp
// 删除:
_sharedState.FinishProcess();
```

**验收标准**:
- [ ] 编译通过
- [ ] 装料流程正常运行

---

### Epic 3: MaterialUnloadingLogic 简化

#### T3.1 移除优先级和流程锁调用

**描述**: 删除优先级请求和流程锁相关调用

**修改位置**:

1. `ProcessCheckEmptyCartCount()` (第272-283行):
```csharp
// 删除以下代码:
_sharedState.RequestUnloadingPriority();
_uiLogger.InfoRaw("处理已开始: {0}", "环线请求下料，设置优先级标志");

if (_ioManager?.Ctx?.R.触发机械手皮带线允许取料 == true)
{
    _requestProcessed = false;
    return;
}

SwitchIndex = "获取流程锁";

// 替换为:
SwitchIndex = "检查料仓有料";
```

2. 删除 `ProcessAcquireLock()` 整个方法 (第289-303行)

3. `ProcessCheckRingLineSignal()` - 修改跳转目标:
```csharp
// 原: SwitchIndex = "检查空车数量";
// 改为直接检查条件后跳转到 "检查料仓有料"
```

4. `ProcessReleaseEmptyCart()` (第549行):
```csharp
// 删除:
_sharedState.ClearUnloadingPriority();
_sharedState.FinishProcess();
```

5. `ProcessCleanup()` (第572-576行):
```csharp
// 删除:
_sharedState.ClearUnloadingPriority();
_sharedState.FinishProcess();
```

6. `ForceCleanup()` (第636-637行):
```csharp
// 删除:
_sharedState.ClearUnloadingPriority();
_sharedState.FinishProcess();
```

**验收标准**:
- [ ] 编译通过
- [ ] 下料流程正常运行

---

### Epic 4: ProductionLineSharedState 精简

#### T4.1 删除互斥锁相关代码

**描述**: 移除不再需要的流程互斥和优先级功能

**删除内容**:

```csharp
// 删除枚举 (第16-32行)
public enum ActiveProcess { ... }

// 删除字段 (第47, 57行)
private ActiveProcess _currentProcess = ActiveProcess.None;
private bool _unloadingPriorityRequested = false;

// 删除流程互斥锁方法 (第222-303行整个 region)
#region 流程互斥锁方法
public bool TryStartLoading() { ... }
public bool TryStartUnloading() { ... }
public void FinishProcess() { ... }
public ActiveProcess GetCurrentProcess() { ... }
public bool IsLoading() { ... }
public bool IsUnloading() { ... }
#endregion

// 删除下料优先级管理 (第344-377行整个 region)
#region 下料优先级管理
public void RequestUnloadingPriority() { ... }
public void ClearUnloadingPriority() { ... }
public bool HasUnloadingPriorityRequest() { ... }
#endregion

// 修改 ResetAllStates() - 移除 _currentProcess 重置
public void ResetAllStates()
{
    lock (_stateLock)
    {
        _loadingCompleted = false;
        _unloadingCompleted = false;
        _systemPaused = false;
        _requireReinit = false;
        // 删除: _currentProcess = ActiveProcess.None;
    }
}
```

**验收标准**:
- [ ] 编译通过
- [ ] BinElevatorModule 仍能正常触发完成信号

---

### Epic 5: 测试更新

#### T5.1 更新 ProductionLineSharedStateTests

**描述**: 删除已移除功能的测试用例

**删除测试**:
- `TryStartLoading_*` 相关测试
- `TryStartUnloading_*` 相关测试
- `FinishProcess_*` 相关测试
- `*UnloadingPriority*` 相关测试

#### T5.2 更新 MainLogic 测试

**描述**: 验证串行轮询逻辑

**新增测试**:
- 装料完成后切换到下料
- 下料完成后回到装料
- 装料禁用时直接进入下料
- 下料禁用时直接回到装料

---

## 4. 执行顺序

```
T1.1 MainLogic 重构
    ↓
T2.1 MaterialLoadingLogic 简化
    ↓
T3.1 MaterialUnloadingLogic 简化
    ↓
T4.1 ProductionLineSharedState 精简
    ↓
编译验证
    ↓
T5.1 更新 SharedState 测试
    ↓
T5.2 更新 MainLogic 测试
```

---

## 5. 风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| BinElevatorModule 依赖 `IsUnloading()` | 高 | 检查 BinElevatorModule 代码，确认使用方式 |
| 串行轮询响应延迟 | 中 | 装料等待状态时仍可快速切换到下料 |
| 单元测试大量失效 | 低 | 按任务顺序逐步更新 |

---

## 6. 回滚方案

如重构出现问题，可通过 git 回滚:

```bash
git checkout HEAD -- Ewan.Core/Logic/MainLogic.cs
git checkout HEAD -- Ewan.Core/Logic/MaterialLoadingLogic.cs
git checkout HEAD -- Ewan.Core/Logic/MaterialUnloadingLogic.cs
git checkout HEAD -- Ewan.Core/Module/ProductionLineSharedState.cs
```

---

## 7. 预计工作量

| 任务 | 预计时间 |
|------|----------|
| T1.1 MainLogic 重构 | 30 分钟 |
| T2.1 MaterialLoadingLogic 简化 | 20 分钟 |
| T3.1 MaterialUnloadingLogic 简化 | 30 分钟 |
| T4.1 ProductionLineSharedState 精简 | 20 分钟 |
| 编译调试 | 30 分钟 |
| T5.x 测试更新 | 1 小时 |
| **合计** | **约 3 小时** |
