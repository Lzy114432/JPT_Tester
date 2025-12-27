# 生产线架构重构 - 任务分解

## 概述

本文档基于 `ProductionLine_Architecture_Refactoring.md` 中的设计，将重构任务分解为具体的、可执行的实施步骤。

---

## 任务依赖关系图

```
阶段1: 基础准备
├── T1.1 更新 csproj 添加 Logic 文件引用
├── T1.2 创建 Operator 目录
└── T1.3 补充 Logic 公共方法

阶段2: 核心组件
├── T2.1 创建 HomeLogic.cs (依赖: T1.3)
├── T2.2 创建 ProductionLineOperator.cs (依赖: T1.2, T2.1)
└── T2.3 创建 BinElevatorWrapperModule.cs (可选)

阶段3: 集成改造
├── T3.1 改造 StreamController.cs (依赖: T2.2)
└── T3.2 更新 MainWindowViewModel (依赖: T3.1)

阶段4: 测试验证
├── T4.1 ProductionLineOperator 单元测试
├── T4.2 HomeLogic 单元测试
├── T4.3 集成测试
└── T4.4 编译验证

阶段5: 清理收尾
├── T5.1 移除 ProductionLineModule 引用
└── T5.2 更新文档
```

---

## 阶段 1: 基础准备

### T1.1 更新 Ewan.Core.csproj 添加 Logic 文件引用

**状态**: 待执行
**优先级**: P0 (阻塞后续任务)
**依赖**: 无

**验收标准**:
- [ ] csproj 包含 `Logic\MaterialLoadingLogic.cs`
- [ ] csproj 包含 `Logic\MaterialUnloadingLogic.cs`
- [ ] csproj 包含 `Logic\ProductionLineLogic.cs`
- [ ] 编译成功无错误

**实施步骤**:
1. 编辑 `Ewan.Core\Ewan.Core.csproj`
2. 在 `<ItemGroup>` 中添加以下 Compile 条目:
```xml
<Compile Include="Logic\MaterialLoadingLogic.cs" />
<Compile Include="Logic\MaterialUnloadingLogic.cs" />
<Compile Include="Logic\ProductionLineLogic.cs" />
```
3. 运行编译验证

---

### T1.2 创建 Operator 目录

**状态**: 待执行
**优先级**: P0
**依赖**: 无

**验收标准**:
- [ ] `Ewan.Core\Operator` 目录存在

**实施步骤**:
```bash
mkdir Ewan.Core\Operator
```

---

### T1.3 补充 Logic 公共方法

**状态**: 待执行
**优先级**: P1
**依赖**: T1.1

**文件**:
- `MaterialLoadingLogic.cs`
- `MaterialUnloadingLogic.cs`
- `ProductionLineLogic.cs`

**验收标准**:
- [ ] MaterialLoadingLogic 添加 `ForceCleanup()` 公共方法
- [ ] MaterialUnloadingLogic 添加 `ForceCleanup()` 公共方法
- [ ] ProductionLineLogic 添加 `SetBinElevatorModule()` 公共方法 (已存在，验证)

**实施步骤**:

1. **MaterialLoadingLogic.cs** - 将 `ForceCleanup` 改为 public:
```csharp
// 当前: private void ForceCleanup(string reason)
// 改为: public void ForceCleanup(string reason)
```

2. **MaterialUnloadingLogic.cs** - 将 `ForceCleanup` 改为 public:
```csharp
// 当前: private void ForceCleanup(string reason)
// 改为: public void ForceCleanup(string reason)
```

3. **ProductionLineLogic.cs** - 验证 `SetBinElevatorModule` 存在:
```csharp
// 已存在: public void SetBinElevatorModule(BinElevatorModule binElevator)
```

---

## 阶段 2: 核心组件

### T2.1 创建 HomeLogic.cs

**状态**: 待执行
**优先级**: P0
**依赖**: T1.3

**文件**: `Ewan.Core\Logic\HomeLogic.cs`

**验收标准**:
- [ ] 文件创建成功
- [ ] 继承 `LogicBase`
- [ ] 实现上料机初始化序列
- [ ] 实现料仓初始化调用
- [ ] 编译通过

**代码模板**: 见 `ProductionLine_Architecture_Refactoring.md` 2.2节

**实施步骤**:
1. 创建 `Ewan.Core\Logic\HomeLogic.cs`
2. 实现 Handler() 方法，包含:
   - 初始状态
   - 上料机初始化 (PerformFeederInit)
   - 料仓初始化
   - 等待完成
3. 添加到 csproj

---

### T2.2 创建 ProductionLineOperator.cs

**状态**: 待执行
**优先级**: P0
**依赖**: T1.2, T2.1

**文件**: `Ewan.Core\Operator\ProductionLineOperator.cs`

**验收标准**:
- [ ] 文件创建成功
- [ ] 封装 AlarmService
- [ ] 封装 LogicRunner
- [ ] 封装 MachineOperator
- [ ] 实现 Start/Stop/Pause/Resume/Home/ClearAlarm
- [ ] 实现 LogicException 事件处理转报警
- [ ] 编译通过

**代码模板**: 见 `ProductionLine_Architecture_Refactoring.md` 2.1节

**实施步骤**:
1. 创建 `Ewan.Core\Operator\ProductionLineOperator.cs`
2. 实现构造函数:
   - 创建 AlarmService
   - 创建 ProductionLineSharedState
   - 创建 BinElevatorModule
   - 创建 LogicRunner
   - 订阅 LogicException 事件
   - 创建 MachineOperator
3. 实现公共控制方法
4. 实现 Logic 工厂方法
5. 实现事件处理方法
6. 添加到 csproj

---

### T2.3 创建 BinElevatorWrapperModule.cs (可选)

**状态**: 待执行
**优先级**: P2 (可选)
**依赖**: T2.2

**文件**: `Ewan.Core\Module\BinElevatorWrapperModule.cs`

**说明**: 如果需要 BinElevator 独立于 Operator 运行，可创建此包装模块

**验收标准**:
- [ ] 实现 IModule 接口
- [ ] 调用 Operator.RunBinElevator()

**实施步骤**:
1. 创建包装模块，在 Run() 中调用 ProductionLineOperator.RunBinElevator()
2. 或者直接在 StreamController 中管理 BinElevator

---

## 阶段 3: 集成改造

### T3.1 改造 StreamController.cs

**状态**: 待执行
**优先级**: P0
**依赖**: T2.2

**文件**: `Ewan.BusinessBonding\StreamController.cs`

**验收标准**:
- [ ] 移除 ProductionLineModule 引用
- [ ] 添加 ProductionLineOperator 成员
- [ ] Init() 中创建 ProductionLineOperator
- [ ] StartRun() 调用 _productionOperator.Start()
- [ ] StopRun() 调用 _productionOperator.Stop()
- [ ] 添加 PauseProduction/ResumeProduction/EmergencyStop/Home/ClearAlarm 公共方法
- [ ] 编译通过

**改动点详述**:

1. **添加命名空间**:
```csharp
using Ewan.Core.Operator;
```

2. **替换成员变量**:
```csharp
// 移除
// private StreamRunner _mainRunner;
// private List<IModule> _mainModules = new List<IModule>();

// 添加
private ProductionLineOperator _productionOperator;
```

3. **修改 Init()**:
```csharp
// 移除
// _mainModules.Add(new ProductionLineModule());
// _mainRunner = new StreamRunner(_mainModules);

// 添加
_productionOperator = new ProductionLineOperator();
```

4. **修改 StartRun()**:
```csharp
// 移除
// StartMainStream();

// 添加
_productionOperator?.Start();
```

5. **修改 StopRun()**:
```csharp
// 移除
// StopMainStream();

// 添加
_productionOperator?.Stop();
```

6. **添加公共控制方法**:
```csharp
public void PauseProduction() => _productionOperator?.Pause();
public void ResumeProduction() => _productionOperator?.Resume();
public void EmergencyStop() => _productionOperator?.EmergencyStop();
public void Home() => _productionOperator?.Home();
public void ClearAlarm() => _productionOperator?.ClearAlarm();
public IAlarmService Alarms => _productionOperator?.Alarms;
```

7. **修改 Dispose()**:
```csharp
_productionOperator?.Dispose();
```

---

### T3.2 更新 MainWindowViewModel (可选)

**状态**: 待执行
**优先级**: P2
**依赖**: T3.1

**说明**: 如果 UI 需要显示报警或步骤状态，需要订阅事件

**验收标准**:
- [ ] 订阅 StepChangedEventArgs 显示当前步骤
- [ ] 订阅 AlarmChanged 显示报警
- [ ] UI 绑定工作正常

---

## 阶段 4: 测试验证

### T4.1 ProductionLineOperator 单元测试

**状态**: 待执行
**优先级**: P1
**依赖**: T2.2

**文件**: `Ewan.Core.Tests\Operator\ProductionLineOperatorTests.cs`

**验收标准**:
- [ ] Start_WhenNoAlarm_ReturnsTrue
- [ ] Start_WhenHasAlarm_ReturnsFalse
- [ ] Pause_SetsSystemPaused
- [ ] Stop_ResetsAllStates
- [ ] Home_ClearsAlarmAndExecutesHomeLogic
- [ ] LogicException_AddsAlarm

---

### T4.2 HomeLogic 单元测试

**状态**: 待执行
**优先级**: P1
**依赖**: T2.1

**文件**: `Ewan.Core.Tests\Logic\HomeLogicTests.cs`

**验收标准**:
- [ ] Constructor_InitializesCorrectly
- [ ] Handler_ProgressesThroughStates
- [ ] Complete_AfterAllSteps

---

### T4.3 集成测试

**状态**: 待执行
**优先级**: P1
**依赖**: T3.1

**验收标准**:
- [ ] StreamController.Init() 成功
- [ ] StreamController.StartRun() 成功
- [ ] StreamController.StopRun() 成功
- [ ] 辅助模块正常运行

---

### T4.4 编译验证

**状态**: 待执行
**优先级**: P0
**依赖**: 所有编码任务

**验收标准**:
- [ ] 解决方案编译成功 (Debug|x64)
- [ ] 无编译错误
- [ ] 警告数量不增加

**执行命令**:
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln -p:Configuration=Debug -p:Platform=x64
```

---

## 阶段 5: 清理收尾

### T5.1 移除 ProductionLineModule 引用

**状态**: 待执行
**优先级**: P2
**依赖**: T4.3 (集成测试通过后)

**说明**: 确认新架构工作正常后，可选择保留或移除旧的 ProductionLineModule

**验收标准**:
- [ ] 评估是否保留 ProductionLineModule 作为备用
- [ ] 如移除：从 csproj 删除引用
- [ ] 如移除：删除文件

---

### T5.2 更新文档

**状态**: 待执行
**优先级**: P3
**依赖**: T5.1

**验收标准**:
- [ ] 更新 CLAUDE.md 架构描述
- [ ] 更新 README (如有)
- [ ] 标记重构完成

---

## 执行顺序总结

### 关键路径 (Critical Path)

```
T1.1 → T1.3 → T2.1 → T2.2 → T3.1 → T4.3 → T4.4
```

### 建议执行批次

**批次 1 (基础准备)**:
- T1.1 更新 csproj
- T1.2 创建 Operator 目录

**批次 2 (补充 Logic)**:
- T1.3 补充公共方法

**批次 3 (核心组件)**:
- T2.1 创建 HomeLogic
- T2.2 创建 ProductionLineOperator

**批次 4 (集成)**:
- T3.1 改造 StreamController

**批次 5 (验证)**:
- T4.4 编译验证
- T4.1 Operator 测试
- T4.2 HomeLogic 测试
- T4.3 集成测试

**批次 6 (收尾)**:
- T5.1 清理旧代码
- T5.2 更新文档

---

## 风险与注意事项

### 高风险项

1. **BinElevatorModule 集成**
   - 风险：轴控制需要持续轮询，LogicRunner 模式可能不适用
   - 缓解：保持 BinElevator 为独立 Module，通过 SharedState 协调

2. **报警系统集成**
   - 风险：AlarmService 需要正确初始化和订阅
   - 缓解：确保 AlarmChanged 事件正确传播到 UI

3. **线程安全**
   - 风险：ProductionLineSharedState 跨线程访问
   - 缓解：已使用 Interlocked 操作，需测试验证

### 注意事项

1. **编译验证**：每完成一个任务后立即编译验证
2. **增量提交**：按任务粒度提交，便于回滚
3. **保留备份**：改造 StreamController 前备份原文件
4. **测试覆盖**：关键组件需有单元测试

---

## 估算工作量

| 阶段 | 任务数 | 复杂度 |
|------|--------|--------|
| 阶段1 | 3 | 低 |
| 阶段2 | 2-3 | 高 |
| 阶段3 | 1-2 | 中 |
| 阶段4 | 3-4 | 中 |
| 阶段5 | 2 | 低 |

**总任务数**: 11-14 个原子任务

---

*文档生成时间: 2025-12-28*
*基于: ProductionLine_Architecture_Refactoring.md*
