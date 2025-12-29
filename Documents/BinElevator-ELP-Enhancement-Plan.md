# 料仓升降模块 - 硬限位（ELP）检测增强方案

**文档版本**: V1.1
**创建日期**: 2025-12-29
**更新日期**: 2025-12-29
**项目**: MarkingMachineFeeder
**涉及模块**: BinElevatorModule, AxisManager

---

## 1. 需求概述

### 1.1 问题描述

当前 `BinElevatorModule` 存在以下两个场景的问题：

#### 场景 A: 初始化流程 (HandleInitializeAsync)

**现有流程**:
1. **Phase 1 (RaisingToSensor)**: 所有料仓同时上升，等待感应器触发
2. **Phase 2 (LoweringToNoSensor)**: 感应器触发后，下降至无感应位置

**存在的问题**:
- 如果料仓是**空的**（无物料），感应器永远不会触发
- 系统会一直等待直到超时（30秒）才报错
- 空料仓会持续上升，直至触发硬限位或超时

#### 场景 B: 单料仓上升检测物料 (HandleRaiseToSensorAsync)

**现有流程**:
- 单个料仓 JogUp → 等待感应器触发 → 10秒超时返回"超时无料"

**存在的问题**:
- 空料仓需要等待 10 秒超时才返回结果
- 即使已经触发 ELP 到顶，仍然继续等待直到超时

### 1.2 需求说明

**目标**: 在所有料仓上升场景中增加硬限位（ELP）判断逻辑

#### 需求 1: 初始化流程

**逻辑**:
- 上升过程中，如果**料仓传感器触发** → 正常流程，进入下降阶段
- 上升过程中，如果**ELP（正向硬限位）触发** → 认为已到顶部，料仓为空，标记初始化完成
- 超时仍无任何信号触发 → 报错

#### 需求 2: 单料仓上升检测物料 (RaiseToSensor)

**逻辑**:
- 上升过程中，如果**料仓传感器触发** → 返回"检测到物料"
- 上升过程中，如果**ELP（正向硬限位）触发** → 立即返回"无物料"（不等超时）
- 超时仍无任何信号触发 → 返回"超时无料"

**预期效果**:
- 空料仓初始化时间从 30 秒缩短至 2-5 秒（取决于上升速度）
- 单料仓检测从 10 秒超时缩短至 2-5 秒（ELP 触发即返回）
- 提供明确的空料仓状态反馈
- 避免不必要的超时等待

---

## 2. 当前实现分析

### 2.1 BinElevatorModule 初始化流程

**文件位置**: `F:\MarkingMachineFeeder\Ewan.Core\Module\BinElevatorModule.cs`

**关键方法**:

```csharp
// 入口方法 (line 197-270)
private async Task<BinElevatorStatusMessage> HandleInitializeAsync(BinElevatorCommandMessage request)
{
    // 创建初始化任务，设置 Phase = InitPhase.RaisingToSensor
    // 启动所有未触发感应器的料仓上升
    // 等待完成或超时（30秒）
}

// Phase 1: 上升检测感应器 (line 371-401)
private void ProcessInitRaisingPhase()
{
    foreach (var bin in _bins)
    {
        if (bin.CurrentState == BinElevatorState.Moving)
        {
            // 仅检查料仓感应器
            if (ReadBinSensor(bin.BinNumber))
            {
                StopBinAxis(bin);
                bin.ReachedSensor = true;
                bin.CurrentState = BinElevatorState.Stopped;
            }
        }
    }

    // 所有料仓都检测到感应器后，进入 Phase 2
    if (_bins.All(b => b.ReachedSensor))
    {
        _initializeTask.Phase = InitPhase.LoweringToNoSensor;
        // 开始下降...
    }
}

// Phase 2: 下降至无感应 (line 403-438)
private void ProcessInitLoweringPhase()
{
    // 下降到感应器无信号位置
    // 所有料仓完成后，返回成功
}
```

**问题点**:
- `ProcessInitRaisingPhase()` 只检测 `ReadBinSensor()`
- 没有检测轴的硬限位状态
- 空料仓会一直运动直到 30 秒超时

### 2.2 AxisManager 当前功能

**文件位置**: `F:\MarkingMachineFeeder\Ewan.Core\Axis\AxisManager.cs`

**现有方法**:
- `GetAxisConfig(int axisId)` - 获取轴配置
- `JogUp(AxisConfig)` - 上升点动
- `JogStop(AxisConfig)` - 停止点动
- `Position(AxisConfig)` - 获取位置
- `IsAlarm(AxisConfig)` - 检查报警
- `IsBusy(AxisConfig)` - 检查运动状态

**缺失功能**:
- ❌ 没有 `GetAxisIO(AxisConfig)` 方法获取 IO 状态

### 2.3 轴 IO 状态定义

**文件位置**: `F:\MarkingMachineFeeder\Hardware\EwanAxis\Core\Models\AxisIOState.cs`

```csharp
public class AxisIOState
{
    public bool ALM { get; set; }  // 伺服报警
    public bool ELP { get; set; }  // 正向硬限位（上限位）⭐
    public bool ELN { get; set; }  // 负向硬限位（下限位）
    public bool ORG { get; set; }  // 原点信号
    public bool SLP { get; set; }  // 正向软限位
    public bool SLN { get; set; }  // 负向软限位
    public bool INP { get; set; }  // 到位信号
    public bool Busy { get; set; } // 运动中
    public bool Home { get; set; } // 回原点中

    public bool HasLimitTriggered => ELP || ELN || SLP || SLN;
    public bool IsSafe => !ALM && !HasLimitTriggered;
}
```

**关键信号**:
- `ELP = true` → 触发正向硬限位（料仓已到顶部）

### 2.4 IAxis 接口

**文件位置**: `F:\MarkingMachineFeeder\Hardware\EwanAxis\Core\Interfaces\IAxis.cs`

```csharp
public interface IAxis
{
    // 轴专属IO获取方法
    AxisIOState GetAxisIO();
}
```

**实现类**: `SMC606Axis` (line 471-493)
```csharp
public override AxisIOState GetAxisIO()
{
    uint ret;
    lock (_card.SyncRoot)
    {
        ret = LTSMC.smc_axis_io_status(_card.CardNo, (ushort)_parameter.AxisNum);
    }

    var ioState = new AxisIOState
    {
        ALM = (ret & (1 << 0)) > 0,
        ELP = (ret & (1 << 1)) > 0,  // bit 1: 正限位 ⭐
        ELN = (ret & (1 << 2)) > 0,
        ORG = (ret & (1 << 4)) > 0,
        // ...
    };
    return ioState;
}
```

---

## 3. 修改方案

### 3.1 架构设计

```
BinElevatorModule
    |
    ├─ ProcessInitRaisingPhase()
    |   └─ 检查两种条件:
    |       1. ReadBinSensor() → 料仓有料
    |       2. CheckAxisELP() → 料仓无料（已到顶）
    |
    └─ CheckAxisELP(BinState bin)
        └─ AxisManager.GetAxisIO(axisConfig)
            └─ IAxis.GetAxisIO()
                └─ AxisIOState.ELP
```

### 3.2 修改策略

**方案**: 在现有两阶段流程中增加 ELP 检测

**优点**:
- 最小化代码修改
- 保持现有架构不变
- 仅在 Phase 1 增加一个额外检测条件

**阶段流程调整**:

| 阶段 | 原有逻辑 | 新增逻辑 |
|------|---------|---------|
| Phase 1: RaisingToSensor | 检测料仓传感器 | **+ 检测 ELP 硬限位** |
| Phase 2: LoweringToNoSensor | 下降至无感应 | 无变化 |

**判断逻辑**:

```csharp
if (ReadBinSensor(bin.BinNumber))
{
    // 情况A: 检测到物料 → 正常流程
    bin.ReachedSensor = true;
    bin.HasMaterial = true;
}
else if (CheckAxisELP(bin))
{
    // 情况B: ELP触发 → 空料仓到顶
    bin.ReachedSensor = true;
    bin.HasMaterial = false;
}
```

### 3.3 数据结构调整

**BinState 类增强**:

```csharp
private class BinState
{
    public int BinNumber { get; }
    public int AxisId { get; }
    public BinElevatorState CurrentState { get; set; }
    public bool ReachedSensor { get; set; }

    // ⭐ 新增字段
    public bool HasMaterial { get; set; }  // 初始化时检测到的物料状态

    public BinState(int binNumber, int axisId)
    {
        BinNumber = binNumber;
        AxisId = axisId;
        CurrentState = BinElevatorState.Unknown;
        ReachedSensor = false;
        HasMaterial = false;
    }

    public void Reset()
    {
        CurrentState = BinElevatorState.Unknown;
        ReachedSensor = false;
        HasMaterial = false;  // ⭐ 重置时清除
    }
}
```

---

## 4. 详细实现步骤

### 4.1 步骤 1: AxisManager 添加 GetAxisIO 方法

**文件**: `F:\MarkingMachineFeeder\Ewan.Core\Axis\AxisManager.cs`

**位置**: 在 `#region 轴操作方法` 区域末尾添加（line 364 之前）

**代码**:

```csharp
/// <summary>
/// 获取轴IO状态（包括硬限位、报警等信号）
/// </summary>
/// <param name="axisConfig">轴配置</param>
/// <returns>轴IO状态，获取失败返回null</returns>
public AxisIOState GetAxisIO(AxisConfig axisConfig)
{
    var axis = GetAxis(axisConfig);
    if (axis == null)
    {
        s_logger.WarnFormat("获取轴IO失败: 轴 {0} 不存在", axisConfig?.AxisID);
        return null;
    }

    try
    {
        return axis.GetAxisIO();
    }
    catch (Exception ex)
    {
        s_logger.ErrorFormat("获取轴IO异常: 轴 {0}, 错误: {1}",
            axisConfig.AxisID, ex.Message);
        return null;
    }
}
```

**依赖**:
- 需要引用 `using EwanAxis.Core.Interfaces;` (已存在)

### 4.2 步骤 2: BinElevatorModule 增加 ELP 检测方法

**文件**: `F:\MarkingMachineFeeder\Ewan.Core\Module\BinElevatorModule.cs`

**位置**: 在 `#region 辅助方法` 区域，`ReadBinSensor()` 方法之后（line 560 之后）

**代码**:

```csharp
/// <summary>
/// 检查料仓轴是否触发正向硬限位（ELP）
/// </summary>
/// <param name="bin">料仓状态</param>
/// <returns>true=已触发ELP，false=未触发</returns>
private bool CheckAxisELP(BinState bin)
{
    try
    {
        var axisConfig = _axisManager?.GetAxisConfig(bin.AxisId);
        if (axisConfig == null)
        {
            return false;
        }

        var ioState = _axisManager.GetAxisIO(axisConfig);
        if (ioState == null)
        {
            return false;
        }

        // ELP = true 表示触发正向硬限位（上限位）
        return ioState.ELP;
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("检测料仓{0}硬限位失败: {1}", bin.BinNumber, ex.Message);
        return false;
    }
}
```

### 4.3 步骤 3: 修改 BinState 数据结构

**文件**: `F:\MarkingMachineFeeder\Ewan.Core\Module\BinElevatorModule.cs`

**位置**: `BinState` 类定义（文件末尾，line 612-634 附近）

**修改内容**:

```csharp
private class BinState
{
    public int BinNumber { get; }
    public int AxisId { get; }
    public BinElevatorState CurrentState { get; set; }
    public bool ReachedSensor { get; set; }

    // ⭐ 新增: 初始化时检测到的物料状态
    public bool HasMaterial { get; set; }

    public BinState(int binNumber, int axisId)
    {
        BinNumber = binNumber;
        AxisId = axisId;
        CurrentState = BinElevatorState.Unknown;
        ReachedSensor = false;
        HasMaterial = false;  // ⭐ 新增
    }

    public void Reset()
    {
        CurrentState = BinElevatorState.Unknown;
        ReachedSensor = false;
        HasMaterial = false;  // ⭐ 新增
    }
}
```

### 4.4 步骤 4: 修改 ProcessInitRaisingPhase 逻辑

**文件**: `F:\MarkingMachineFeeder\Ewan.Core\Module\BinElevatorModule.cs`

**位置**: `ProcessInitRaisingPhase()` 方法（line 371-401）

**原代码**:

```csharp
private void ProcessInitRaisingPhase()
{
    foreach (var bin in _bins)
    {
        if (bin.ReachedSensor)
        {
            continue;
        }

        if (bin.CurrentState == BinElevatorState.Moving)
        {
            if (ReadBinSensor(bin.BinNumber))
            {
                StopBinAxis(bin);
                bin.ReachedSensor = true;
                bin.CurrentState = BinElevatorState.Stopped;
            }
        }
    }

    if (_bins.All(b => b.ReachedSensor))
    {
        _initializeTask.Phase = InitPhase.LoweringToNoSensor;
        foreach (var bin in _bins)
        {
            bin.ReachedSensor = false;
            StartBinJogDown(bin);
            bin.CurrentState = BinElevatorState.Moving;
        }
    }
}
```

**修改为**:

```csharp
private void ProcessInitRaisingPhase()
{
    foreach (var bin in _bins)
    {
        if (bin.ReachedSensor)
        {
            continue;
        }

        if (bin.CurrentState == BinElevatorState.Moving)
        {
            // ⭐ 情况1: 检测到料仓传感器 → 有物料
            if (ReadBinSensor(bin.BinNumber))
            {
                StopBinAxis(bin);
                bin.ReachedSensor = true;
                bin.HasMaterial = true;
                bin.CurrentState = BinElevatorState.Stopped;
                _uiLogger.InfoRaw("料仓{0}检测到物料", bin.BinNumber);
            }
            // ⭐ 情况2: 触发ELP硬限位 → 无物料（已到顶部）
            else if (CheckAxisELP(bin))
            {
                StopBinAxis(bin);
                bin.ReachedSensor = true;
                bin.HasMaterial = false;
                bin.CurrentState = BinElevatorState.Stopped;
                _uiLogger.InfoRaw("料仓{0}已到顶部（ELP触发），无物料", bin.BinNumber);
            }
        }
    }

    // ⭐ 所有料仓都完成检测后的处理
    if (_bins.All(b => b.ReachedSensor))
    {
        // ⭐ 筛选有物料的料仓进入下降阶段
        var binsWithMaterial = _bins.Where(b => b.HasMaterial).ToArray();

        if (binsWithMaterial.Length > 0)
        {
            // ⭐ 有物料的料仓进入下降阶段
            _initializeTask.Phase = InitPhase.LoweringToNoSensor;

            foreach (var bin in _bins)
            {
                bin.ReachedSensor = false;

                if (bin.HasMaterial)
                {
                    // 有物料：下降到无感应位置
                    StartBinJogDown(bin);
                    bin.CurrentState = BinElevatorState.Moving;
                }
                else
                {
                    // 无物料（ELP触发）：标记为已完成，无需下降
                    bin.ReachedSensor = true;
                    bin.CurrentState = BinElevatorState.Stopped;
                }
            }

            _uiLogger.InfoRaw("初始化进入下降阶段: {0}个料仓有物料", binsWithMaterial.Length);
        }
        else
        {
            // ⭐ 所有料仓都无物料（都触发了ELP）
            var tcs = _initializeTask.Completion;
            _initializeTask = null;

            foreach (var bin in _bins)
            {
                bin.Reset();
            }

            tcs?.TrySetResult(BinElevatorStatusMessage.InitializeResult(
                BinOperationResult.Success, "初始化完成（所有料仓无物料）"));

            _uiLogger.InfoRaw("所有料仓初始化完成（均无物料）");
        }
    }
}
```

**关键变更**:
1. 增加 `CheckAxisELP(bin)` 检测
2. 设置 `bin.HasMaterial` 标志
3. 根据物料状态决定是否进入下降阶段
4. 无物料的料仓直接标记完成，不参与下降

### 4.5 步骤 5: 修改 ProcessUnloadingTask 逻辑（RaiseToSensor 场景）

**文件**: `F:\MarkingMachineFeeder\Ewan.Core\Module\BinElevatorModule.cs`

**位置**: `ProcessUnloadingTask()` 方法（line 486-506）

**原代码**:

```csharp
private void ProcessUnloadingTask(BinState bin, BinTask task)
{
    switch (bin.CurrentState)
    {
        case BinElevatorState.Unknown:
            StartBinJogUp(bin);
            bin.CurrentState = BinElevatorState.Moving;
            break;

        case BinElevatorState.Moving:
            if (ReadBinSensor(bin.BinNumber))
            {
                StopBinAxis(bin);
                var result = BinElevatorStatusMessage.MaterialCheckResult(
                    bin.BinNumber, BinOperationResult.HasMaterial, "检测到物料");
                CompleteTask(bin.BinNumber, result);
                _uiLogger.InfoRaw("料仓{0}上升完成，检测到物料", bin.BinNumber);
            }
            break;
    }
}
```

**修改为**:

```csharp
private void ProcessUnloadingTask(BinState bin, BinTask task)
{
    switch (bin.CurrentState)
    {
        case BinElevatorState.Unknown:
            StartBinJogUp(bin);
            bin.CurrentState = BinElevatorState.Moving;
            break;

        case BinElevatorState.Moving:
            // ⭐ 情况1: 检测到料仓传感器 → 有物料
            if (ReadBinSensor(bin.BinNumber))
            {
                StopBinAxis(bin);
                var result = BinElevatorStatusMessage.MaterialCheckResult(
                    bin.BinNumber, BinOperationResult.HasMaterial, "检测到物料");
                CompleteTask(bin.BinNumber, result);
                _uiLogger.InfoRaw("料仓{0}上升完成，检测到物料", bin.BinNumber);
            }
            // ⭐ 情况2: 触发ELP硬限位 → 无物料（已到顶部），立即返回
            else if (CheckAxisELP(bin))
            {
                StopBinAxis(bin);
                var result = BinElevatorStatusMessage.MaterialCheckResult(
                    bin.BinNumber, BinOperationResult.NoMaterial, "已到顶部（ELP触发），无物料");
                CompleteTask(bin.BinNumber, result);
                _uiLogger.InfoRaw("料仓{0}已到顶部（ELP触发），无物料", bin.BinNumber);
            }
            break;
    }
}
```

**关键变更**:
1. 增加 `CheckAxisELP(bin)` 检测
2. ELP 触发时立即返回 `BinOperationResult.NoMaterial`
3. 不再需要等待超时

**注意**: 需要在 `BinOperationResult` 枚举中确认是否有 `NoMaterial` 值，如果没有可以使用现有的合适值或新增。

---

### 4.6 步骤 6: 调整 ProcessInitLoweringPhase（可选优化）

**文件**: `F:\MarkingMachineFeeder\Ewan.Core\Module\BinElevatorModule.cs`

**位置**: `ProcessInitLoweringPhase()` 方法（line 403-438）

**说明**: 由于步骤 4 已经筛选了有物料的料仓，此方法无需修改，但可以添加安全检查：

**原代码保持不变**，仅添加日志优化（可选）：

```csharp
private void ProcessInitLoweringPhase()
{
    foreach (var bin in _bins)
    {
        if (bin.ReachedSensor)
        {
            continue;
        }

        if (bin.CurrentState == BinElevatorState.Moving)
        {
            if (!ReadBinSensor(bin.BinNumber))
            {
                StopBinAxis(bin);
                bin.ReachedSensor = true;
                bin.CurrentState = BinElevatorState.Stopped;
                // ⭐ 可选：更详细的日志
                _uiLogger.InfoRaw("料仓{0}下降到无感应位置", bin.BinNumber);
            }
        }
    }

    if (_bins.All(b => b.ReachedSensor))
    {
        var tcs = _initializeTask.Completion;
        _initializeTask = null;

        foreach (var bin in _bins)
        {
            bin.Reset();
        }

        tcs?.TrySetResult(BinElevatorStatusMessage.InitializeResult(
            BinOperationResult.Success, "初始化完成"));

        _uiLogger.InfoRaw("所有料仓初始化完成");
    }
}
```

---

## 5. 代码修改清单

### 5.1 文件修改概览

| 文件路径 | 修改类型 | 修改内容 |
|---------|---------|---------|
| `Ewan.Core\Axis\AxisManager.cs` | 新增方法 | 添加 `GetAxisIO(AxisConfig)` |
| `Ewan.Core\Module\BinElevatorModule.cs` | 新增方法 | 添加 `CheckAxisELP(BinState)` |
| `Ewan.Core\Module\BinElevatorModule.cs` | 修改类 | BinState 增加 `HasMaterial` 字段 |
| `Ewan.Core\Module\BinElevatorModule.cs` | 修改方法 | `ProcessInitRaisingPhase()` 增加 ELP 检测 |
| `Ewan.Core\Module\BinElevatorModule.cs` | 修改方法 | `ProcessUnloadingTask()` 增加 ELP 检测 |
| `Ewan.Core\Module\BinElevatorModule.cs` | 可选优化 | `ProcessInitLoweringPhase()` 日志增强 |

### 5.2 代码行数统计

| 修改项 | 新增行数 | 修改行数 | 删除行数 |
|-------|---------|---------|---------|
| AxisManager.GetAxisIO() | ~20 行 | 0 | 0 |
| BinElevatorModule.CheckAxisELP() | ~25 行 | 0 | 0 |
| BinState 类修改 | 3 行 | 2 行 | 0 |
| ProcessInitRaisingPhase() | ~35 行 | 10 行 | 5 行 |
| ProcessUnloadingTask() | ~10 行 | 0 | 0 |
| **总计** | **~93 行** | **~12 行** | **~5 行** |

### 5.3 依赖项检查

**新增引用**:
- ✅ `using EwanAxis.Core.Interfaces;` - AxisManager.cs 已存在
- ✅ `using System.Linq;` - BinElevatorModule.cs 已存在（line 7）

**无需新增 NuGet 包或外部依赖**

---

## 6. 测试要点

### 6.1 单元测试场景

#### 测试场景 1: 料仓有物料（正常流程）

**前置条件**:
- 料仓内有物料
- 物料位置在传感器检测范围内

**执行步骤**:
1. 发送初始化命令
2. 等待 Phase 1 完成
3. 等待 Phase 2 完成

**预期结果**:
- Phase 1: 料仓上升，传感器触发，停止上升
- Phase 2: 料仓下降，传感器无信号，停止下降
- 初始化成功，返回 `BinOperationResult.Success`
- 日志包含: "料仓X检测到物料"

**验证点**:
- ✅ `bin.HasMaterial == true`
- ✅ 未触发 ELP 检测分支
- ✅ 总耗时 < 10 秒

---

#### 测试场景 2: 料仓无物料（ELP 触发）

**前置条件**:
- 料仓完全空载
- 上升会触发 ELP 硬限位

**执行步骤**:
1. 发送初始化命令
2. 等待料仓上升至 ELP 触发

**预期结果**:
- Phase 1: 料仓上升至顶部，ELP 触发，立即停止
- 跳过 Phase 2（无需下降）
- 初始化成功，返回 `BinOperationResult.Success`
- 日志包含: "料仓X已到顶部（ELP触发），无物料"

**验证点**:
- ✅ `bin.HasMaterial == false`
- ✅ ELP 检测分支生效
- ✅ 总耗时 < 5 秒（远小于 30 秒超时）
- ✅ 未进入 Phase 2 下降流程

---

#### 测试场景 3: 混合场景（部分料仓有料，部分无料）

**前置条件**:
- 料仓 1: 有物料
- 料仓 2: 无物料
- 料仓 3: 有物料

**执行步骤**:
1. 发送初始化命令
2. 观察三个料仓的独立行为

**预期结果**:
- Phase 1:
  - 料仓 1: 传感器触发，停止
  - 料仓 2: ELP 触发，停止
  - 料仓 3: 传感器触发，停止
- Phase 2:
  - 料仓 1: 下降至无感应
  - 料仓 2: 保持停止（已完成）
  - 料仓 3: 下降至无感应
- 初始化成功

**验证点**:
- ✅ 料仓 1: `HasMaterial == true`，执行下降
- ✅ 料仓 2: `HasMaterial == false`，跳过下降
- ✅ 料仓 3: `HasMaterial == true`，执行下降
- ✅ 总耗时合理（< 15 秒）

---

#### 测试场景 4: RaiseToSensor 有物料

**前置条件**:
- 单料仓内有物料
- 物料位置在传感器检测范围内

**执行步骤**:
1. 发送 RaiseToSensor 命令
2. 等待感应器触发

**预期结果**:
- 料仓上升，传感器触发，立即返回
- 返回 `BinOperationResult.HasMaterial`

**验证点**:
- ✅ 返回"检测到物料"
- ✅ 响应时间 < 5 秒

---

#### 测试场景 5: RaiseToSensor 无物料（ELP 触发）

**前置条件**:
- 单料仓完全空载
- 上升会触发 ELP 硬限位

**执行步骤**:
1. 发送 RaiseToSensor 命令
2. 等待料仓上升至 ELP 触发

**预期结果**:
- 料仓上升至顶部，ELP 触发，立即停止
- 返回 `BinOperationResult.NoMaterial`
- 日志包含: "料仓X已到顶部（ELP触发），无物料"

**验证点**:
- ✅ 返回"无物料"（不是"超时无料"）
- ✅ 总耗时 < 5 秒（远小于 10 秒超时）
- ✅ ELP 检测分支生效

---

### 6.2 集成测试要点

#### 测试点 1: AxisManager.GetAxisIO() 方法

**测试代码**:
```csharp
var axisManager = AxisManager.Instance();
var axisConfig = axisManager.GetAxisConfig(0); // 料仓1轴
var ioState = axisManager.GetAxisIO(axisConfig);

Assert.NotNull(ioState);
Assert.False(ioState.ALM);  // 无报警
// 根据实际硬件状态验证 ELP
```

**验证点**:
- ✅ 返回非空 `AxisIOState`
- ✅ 各字段值与实际硬件状态一致
- ✅ 异常情况返回 null（轴不存在）

---

#### 测试点 2: ELP 信号实时性

**测试步骤**:
1. 手动点动料仓轴上升
2. 实时读取 ELP 状态
3. 触发硬限位后立即读取

**验证点**:
- ✅ ELP 触发前: `ioState.ELP == false`
- ✅ ELP 触发后: `ioState.ELP == true`
- ✅ 响应延迟 < 100ms

---

#### 测试点 3: 超时保护

**测试场景**: 料仓卡住，既不触发传感器也不触发 ELP

**前置条件**:
- 人为模拟料仓卡住（断开轴使能）

**预期结果**:
- 30 秒后返回超时错误
- 日志包含: "初始化超时"
- 所有轴停止运动

---

### 6.3 边界条件测试

| 测试项 | 条件 | 预期结果 |
|-------|------|---------|
| 空指针保护 | `_axisManager == null` | `CheckAxisELP()` 返回 false |
| 轴配置不存在 | 无效的 `AxisId` | 返回 false，记录警告日志 |
| ELP 抖动 | ELP 信号短暂触发后恢复 | 单次触发即停止（符合安全要求） |
| 同时触发 | 传感器和 ELP 同时触发 | 传感器优先（`if-else` 顺序） |
| 初始化中断 | 收到 ForceStopAll 消息 | 立即取消任务，清理状态 |

---

### 6.4 性能测试

| 指标 | 目标值 | 测量方法 |
|------|--------|---------|
| 空料仓初始化时间 | < 5 秒 | 记录开始到完成的时间戳 |
| 有料仓初始化时间 | < 10 秒 | 完整两阶段流程耗时 |
| ELP 检测频率 | 每 1ms | OnRun() 循环周期 |
| CPU 占用增加 | < 2% | 对比修改前后 CPU 使用率 |

---

### 6.5 安全性测试

#### 测试点 1: 硬限位保护

**场景**: ELP 触发后继续发送上升命令

**预期**:
- 轴卡硬件保护生效，拒绝运动
- 系统检测到 ELP 并停止命令发送

---

#### 测试点 2: 报警状态处理

**场景**: 初始化过程中触发伺服报警（ALM）

**预期**:
- 检测到 `ioState.ALM == true`
- 停止所有运动
- 返回错误状态

**建议增强**（可选）:
```csharp
if (CheckAxisELP(bin))
{
    var ioState = _axisManager.GetAxisIO(_axisManager.GetAxisConfig(bin.AxisId));

    // 额外检查报警状态
    if (ioState?.ALM == true)
    {
        _uiLogger.ErrorRaw("料仓{0}轴报警", bin.BinNumber);
        // 触发异常流程...
    }
}
```

---

## 7. 回归测试清单

### 7.1 现有功能验证

**必须通过的测试**:

- ✅ LoadingCompleted 流程（料仓下降）
- ✅ RaiseToSensor 流程（单料仓上升检测物料）
- ✅ ForceStopAll 强制停止
- ✅ 并发任务处理（多料仓同时操作）
- ✅ 消息驱动响应（MessageHub 订阅）

### 7.2 UI 交互测试

- ✅ 初始化按钮触发正常
- ✅ 日志窗口显示 ELP 相关信息
- ✅ 状态显示准确（料仓有料/无料）

---

## 8. 潜在风险与缓解措施

### 8.1 风险识别

| 风险项 | 影响 | 概率 | 缓解措施 |
|-------|------|------|---------|
| ELP 信号误触发 | 空料仓误判为满料仓 | 低 | 增加日志记录，人工复核 |
| 传感器失效 | 有料料仓触发 ELP | 中 | 维护检查传感器状态 |
| 线程竞争 | IO 状态读取不一致 | 低 | SMC606Axis 内部已有锁保护 |
| 硬件响应延迟 | ELP 触发后轴仍运动一小段 | 低 | 硬件级别保护，正常现象 |

### 8.2 回滚方案

**如果出现严重问题，可快速回滚**:

1. 注释掉 `CheckAxisELP()` 调用
2. 删除 `bin.HasMaterial` 相关逻辑
3. 恢复原 `ProcessInitRaisingPhase()` 代码

**回滚耗时**: < 5 分钟（仅需注释几行代码）

---

## 9. 后续优化建议

### 9.1 短期优化（本次实现后）

1. **日志增强**: 记录 ELP 触发次数和时机，用于数据分析
2. **状态上报**: 通过消息系统上报料仓物料状态到 UI
3. **配置化**: 将 ELP 检测功能设为可配置项（默认启用）

### 9.2 中期优化（1-2 个月）

1. **传感器健康检查**: 定期验证传感器功能是否正常
2. **ELP 标定**: 记录每个料仓触发 ELP 的位置，用于磨损分析
3. **统计报表**: 生成料仓物料使用统计

### 9.3 长期优化（3-6 个月）

1. **预测性维护**: 根据 ELP 触发频率预测料仓需要补料时间
2. **自适应速度**: 根据料仓状态调整上升速度
3. **AI 检测**: 结合传感器数据和 ELP 信号，智能判断物料状态

---

## 10. 附录

### 10.1 相关文件路径

```
F:\MarkingMachineFeeder\
├─ Ewan.Core\
│  ├─ Axis\
│  │  └─ AxisManager.cs                    # 需修改
│  └─ Module\
│     └─ BinElevatorModule.cs              # 需修改
├─ Hardware\
│  └─ EwanAxis\
│     ├─ Core\
│     │  ├─ Interfaces\
│     │  │  └─ IAxis.cs                    # 参考
│     │  └─ Models\
│     │     └─ AxisIOState.cs              # 参考
│     └─ Hardware\
│        └─ SMC606\
│           └─ SMC606Axis.cs               # 参考实现
└─ Documents\
   └─ BinElevator-ELP-Enhancement-Plan.md  # 本文档
```

### 10.2 参考资料

- **CLAUDE.md**: 项目开发指南
- **AxisIOState 定义**: `F:\MarkingMachineFeeder\Hardware\EwanAxis\Core\Models\AxisIOState.cs`
- **MessageHub 文档**: 消息驱动架构设计

### 10.3 术语表

| 术语 | 全称 | 说明 |
|------|------|------|
| ELP | End Limit Positive | 正向硬限位（上限位） |
| ELN | End Limit Negative | 负向硬限位（下限位） |
| ALM | Alarm | 伺服报警信号 |
| ORG | Origin | 原点信号 |
| INP | In Position | 到位信号 |
| Jog | Jog Motion | 点动（连续运动） |

---

## 11. 审批与变更记录

### 11.1 文档审批

| 角色 | 姓名 | 审批意见 | 日期 |
|------|------|---------|------|
| 需求方 | - | 待审批 | - |
| 开发 | - | 待审批 | - |
| 测试 | - | 待审批 | - |

### 11.2 变更记录

| 版本 | 日期 | 修改人 | 修改内容 |
|------|------|-------|---------|
| V1.0 | 2025-12-29 | Claude | 初始版本 |
| V1.1 | 2025-12-29 | Claude | 增加 RaiseToSensor 场景的 ELP 检测需求 |

---

**文档结束**
