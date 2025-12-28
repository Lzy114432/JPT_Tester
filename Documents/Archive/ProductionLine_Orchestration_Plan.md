# 生产线架构重构 - 任务编排计划

## 概述

本文档整合任务分解和架构审计结果，提供优化后的执行计划。

---

## 一、综合任务清单

### 1.1 完整任务列表 (含审计新增)

| ID | 任务名称 | 优先级 | 复杂度 | 依赖 | 并行组 |
|----|----------|--------|--------|------|--------|
| **阶段 1: 基础准备** |
| T1.1 | 更新 csproj 添加 Logic 引用 | P0 | 低 | - | A |
| T1.2 | 创建 Operator 目录 | P0 | 低 | - | A |
| T1.3 | 补充 Logic 公共方法 | P1 | 低 | T1.1 | B |
| T1.4 | **[新增]** 提取 IBinElevator 接口 | P1 | 中 | T1.3 | C |
| **阶段 2: 核心组件** |
| T2.1 | 创建 HomeLogic.cs (非阻塞版) | P0 | 高 | T1.3 | D |
| T2.2 | 创建 ProductionLineOperator.cs | P0 | 高 | T1.2, T1.4, T2.1 | E |
| ~~T2.3~~ | ~~BinElevatorWrapperModule~~ | - | - | - | **[删除]** |
| **阶段 3: 集成改造** |
| T3.1 | 改造 StreamController.cs | P0 | 中 | T2.2 | F |
| T3.2 | 更新 MainWindowViewModel | P2 | 低 | T3.1 | G |
| **阶段 4: 测试验证** |
| T4.1 | ProductionLineOperator 单元测试 | P1 | 中 | T2.2 | H |
| T4.2 | HomeLogic 单元测试 | P1 | 中 | T2.1 | H |
| T4.3 | 集成测试 | P1 | 中 | T3.1 | I |
| T4.4 | 编译验证 | P0 | 低 | T3.1 | F |
| **阶段 5: 清理收尾** |
| T5.1 | 移除 ProductionLineModule 引用 | P2 | 低 | T4.3 | J |
| T5.2 | 更新文档 | P3 | 低 | T5.1 | J |

### 1.2 任务变更说明

| 变更类型 | 任务 | 原因 |
|----------|------|------|
| **新增** | T1.4 提取 IBinElevator 接口 | 架构审计建议，支持测试 |
| **修订** | T2.1 HomeLogic | 改为非阻塞状态机模式 |
| **修订** | T2.2 ProductionLineOperator | 内置 BinElevator 轮询线程 |
| **删除** | T2.3 BinElevatorWrapperModule | 改为 Operator 内置管理 |

---

## 二、依赖关系图

```
                    ┌─────────┐
                    │  START  │
                    └────┬────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               │
    ┌─────────┐    ┌─────────┐          │
    │  T1.1   │    │  T1.2   │          │  并行组 A
    │ csproj  │    │ mkdir   │          │
    └────┬────┘    └────┬────┘          │
         │               │               │
         ▼               │               │
    ┌─────────┐          │              │
    │  T1.3   │──────────┼──────────────┤  并行组 B
    │ public  │          │              │
    └────┬────┘          │              │
         │               │               │
         ▼               │               │
    ┌─────────┐          │              │
    │  T1.4   │◀─────────┘              │  并行组 C
    │IBinElev │                          │
    └────┬────┘                          │
         │                               │
         ▼                               │
    ┌─────────┐                          │
    │  T2.1   │                          │  并行组 D
    │HomeLogic│                          │
    └────┬────┘                          │
         │                               │
         ▼                               │
    ┌─────────┐                          │
    │  T2.2   │◀─────────────────────────┘  并行组 E
    │Operator │
    └────┬────┘
         │
    ┌────┴────┐
    ▼         ▼
┌─────────┐ ┌─────────┐
│  T3.1   │ │  T4.1   │                   并行组 F+H
│StreamCtl│ │ Op Test │
└────┬────┘ └────┬────┘
     │           │
     ▼           │
┌─────────┐      │
│  T4.4   │      │
│Compile  │      │
└────┬────┘      │
     │           │
     ▼           ▼
┌─────────┐ ┌─────────┐
│  T4.3   │ │  T4.2   │                   并行组 I+H
│IntegTest│ │HomeTest │
└────┬────┘ └─────────┘
     │
     ▼
┌─────────┐
│  T5.1   │                               并行组 J
│ Cleanup │
└────┬────┘
     │
     ▼
┌─────────┐
│  T5.2   │
│  Docs   │
└────┬────┘
     │
     ▼
┌─────────┐
│   END   │
└─────────┘
```

---

## 三、关键路径分析

### 3.1 关键路径 (Critical Path)

```
T1.1 → T1.3 → T1.4 → T2.1 → T2.2 → T3.1 → T4.4 → T4.3 → T5.1 → T5.2
```

**关键路径长度**: 10 个任务

### 3.2 路径时间估算

| 任务 | 估算时间 | 累计时间 |
|------|----------|----------|
| T1.1 | 15 分钟 | 15 分钟 |
| T1.3 | 20 分钟 | 35 分钟 |
| T1.4 | 30 分钟 | 1 小时 5 分钟 |
| T2.1 | 45 分钟 | 1 小时 50 分钟 |
| T2.2 | 60 分钟 | 2 小时 50 分钟 |
| T3.1 | 40 分钟 | 3 小时 30 分钟 |
| T4.4 | 10 分钟 | 3 小时 40 分钟 |
| T4.3 | 30 分钟 | 4 小时 10 分钟 |
| T5.1 | 15 分钟 | 4 小时 25 分钟 |
| T5.2 | 20 分钟 | 4 小时 45 分钟 |

**预计总时长**: 约 5 小时 (含并行任务)

---

## 四、优化执行计划

### 4.1 执行波次 (Waves)

#### Wave 1: 基础设施 (并行)
**预计时间**: 15 分钟
**可并行**: 是

| 任务 | 执行者 | 说明 |
|------|--------|------|
| T1.1 | Agent-1 | 更新 csproj |
| T1.2 | Agent-2 | 创建目录 |

**完成标志**: 编译通过

---

#### Wave 2: Logic 增强 (顺序)
**预计时间**: 50 分钟
**依赖**: Wave 1

| 任务 | 执行者 | 说明 |
|------|--------|------|
| T1.3 | Agent-1 | 补充公共方法 |
| T1.4 | Agent-1 | 提取 IBinElevator 接口 |

**完成标志**: 接口定义完成，编译通过

---

#### Wave 3: 核心组件 (顺序)
**预计时间**: 1 小时 45 分钟
**依赖**: Wave 2

| 任务 | 执行者 | 说明 |
|------|--------|------|
| T2.1 | Agent-1 | 创建 HomeLogic (非阻塞) |
| T2.2 | Agent-1 | 创建 ProductionLineOperator |

**完成标志**: 核心组件编译通过

---

#### Wave 4: 集成与初步测试 (并行)
**预计时间**: 1 小时
**依赖**: Wave 3

| 任务 | 执行者 | 说明 |
|------|--------|------|
| T3.1 | Agent-1 | 改造 StreamController |
| T4.1 | Agent-2 | ProductionLineOperator 单元测试 |
| T4.2 | Agent-2 | HomeLogic 单元测试 |

**完成标志**: StreamController 编译通过，单元测试通过

---

#### Wave 5: 验证 (顺序)
**预计时间**: 40 分钟
**依赖**: Wave 4

| 任务 | 执行者 | 说明 |
|------|--------|------|
| T4.4 | Agent-1 | 完整编译验证 |
| T4.3 | Agent-1 | 集成测试 |

**完成标志**: 所有测试通过

---

#### Wave 6: 收尾 (顺序)
**预计时间**: 35 分钟
**依赖**: Wave 5
**可选**: 部分任务可跳过

| 任务 | 执行者 | 说明 |
|------|--------|------|
| T5.1 | Agent-1 | 清理旧代码 (可选) |
| T5.2 | Agent-1 | 更新文档 |
| T3.2 | Agent-2 | 更新 ViewModel (可选) |

---

### 4.2 并行执行矩阵

```
时间    │ Agent-1              │ Agent-2
────────┼──────────────────────┼──────────────────────
 0:00   │ T1.1 csproj          │ T1.2 mkdir
 0:15   │ T1.3 公共方法         │ (等待)
 0:35   │ T1.4 IBinElevator    │ (等待)
 1:05   │ T2.1 HomeLogic       │ (等待)
 1:50   │ T2.2 Operator        │ (等待)
 2:50   │ T3.1 StreamController │ T4.1+T4.2 单元测试
 3:50   │ T4.4 编译验证         │ (完成)
 4:00   │ T4.3 集成测试         │ (完成)
 4:30   │ T5.1 清理             │ T3.2 ViewModel
 4:45   │ T5.2 文档             │ (完成)
────────┼──────────────────────┼──────────────────────
 5:00   │ END                  │ END
```

---

## 五、风险缓解计划

### 5.1 识别的风险

| 风险 | 可能性 | 影响 | 触发条件 | 缓解措施 |
|------|--------|------|----------|----------|
| R1: BinElevator 集成问题 | 中 | 高 | 轮询线程启动失败 | 备用: 保留独立 StreamRunner |
| R2: HomeLogic 阻塞调用遗留 | 低 | 中 | 未完全转换 | 代码审查检查点 |
| R3: 编译错误积累 | 中 | 中 | 未及时验证 | 每个 Wave 结束编译 |
| R4: 测试覆盖不足 | 高 | 低 | 跳过测试任务 | 强制 T4.1/T4.2 |

### 5.2 检查点 (Checkpoints)

| 检查点 | 位置 | 验证内容 |
|--------|------|----------|
| CP1 | Wave 1 后 | csproj 编译通过 |
| CP2 | Wave 2 后 | IBinElevator 接口定义正确 |
| CP3 | Wave 3 后 | HomeLogic 无 Thread.Sleep |
| CP4 | Wave 4 后 | StreamController 编译通过 |
| CP5 | Wave 5 后 | 所有测试通过 |

### 5.3 回滚策略

| 阶段 | 回滚点 | 回滚操作 |
|------|--------|----------|
| Wave 1-2 | T1.1 前 | git reset --hard |
| Wave 3 | T2.1 前 | 删除新文件，恢复 csproj |
| Wave 4+ | T3.1 前 | 保留新组件，不修改 StreamController |

---

## 六、执行指令

### 6.1 启动命令

```bash
# 1. 确保在正确目录
cd "C:\Users\Administrator\Desktop\钧崴\MarkingMachineFeeder\MarkingMachineFeeder"

# 2. 创建备份分支
git checkout -b refactor/production-line-operator

# 3. 开始执行 Wave 1
```

### 6.2 每个 Wave 的验证命令

```bash
# 编译验证
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln -p:Configuration=Debug -p:Platform=x64 -verbosity:minimal

# 运行测试 (Wave 4+)
dotnet test Ewan.Core.Tests\Ewan.Core.Tests.csproj --filter "FullyQualifiedName~ProductionLineOperator|FullyQualifiedName~HomeLogic"
```

### 6.3 提交策略

| Wave | 提交消息 |
|------|----------|
| Wave 1 | `chore: 添加 Logic 文件引用和 Operator 目录` |
| Wave 2 | `refactor: Logic 公共方法和 IBinElevator 接口` |
| Wave 3 | `feat: HomeLogic 和 ProductionLineOperator 实现` |
| Wave 4 | `refactor: StreamController 使用 ProductionLineOperator` |
| Wave 5 | `test: 添加 Operator 和 HomeLogic 单元测试` |
| Wave 6 | `docs: 更新架构文档` |

---

## 七、任务执行清单 (可打印)

### Wave 1: 基础设施
- [ ] T1.1 更新 Ewan.Core.csproj 添加 Logic 引用
- [ ] T1.2 创建 Ewan.Core\Operator 目录
- [ ] **CP1**: 编译验证通过

### Wave 2: Logic 增强
- [ ] T1.3 MaterialLoadingLogic.ForceCleanup 改为 public
- [ ] T1.3 MaterialUnloadingLogic.ForceCleanup 改为 public
- [ ] T1.3 验证 ProductionLineLogic.SetBinElevatorModule 存在
- [ ] T1.4 创建 IBinElevator 接口
- [ ] T1.4 BinElevatorModule 实现 IBinElevator
- [ ] **CP2**: 接口定义正确，编译通过

### Wave 3: 核心组件
- [ ] T2.1 创建 HomeLogic.cs
- [ ] T2.1 实现非阻塞状态机模式 (无 Thread.Sleep)
- [ ] **CP3**: HomeLogic 代码审查 - 无阻塞调用
- [ ] T2.2 创建 ProductionLineOperator.cs
- [ ] T2.2 实现 BinElevator 轮询线程
- [ ] T2.2 添加到 csproj
- [ ] 编译验证

### Wave 4: 集成
- [ ] T3.1 修改 StreamController - 移除 ProductionLineModule
- [ ] T3.1 修改 StreamController - 添加 ProductionLineOperator
- [ ] T3.1 添加控制方法 (Pause/Resume/Home/ClearAlarm)
- [ ] **CP4**: StreamController 编译通过
- [ ] T4.1 创建 ProductionLineOperatorTests.cs
- [ ] T4.2 创建 HomeLogicTests.cs

### Wave 5: 验证
- [ ] T4.4 完整解决方案编译
- [ ] T4.3 运行集成测试
- [ ] **CP5**: 所有测试通过

### Wave 6: 收尾
- [ ] T5.1 评估是否移除 ProductionLineModule
- [ ] T5.2 更新 CLAUDE.md
- [ ] T3.2 (可选) 更新 MainWindowViewModel

---

## 八、完成定义 (Definition of Done)

### 整体完成标准

1. **代码完成**
   - [ ] 所有 P0/P1 任务已完成
   - [ ] 无编译错误
   - [ ] 无新增编译警告

2. **测试完成**
   - [ ] ProductionLineOperator 单元测试通过
   - [ ] HomeLogic 单元测试通过
   - [ ] 集成测试通过

3. **架构符合**
   - [ ] BinElevator 由 Operator 单一管理
   - [ ] HomeLogic 无阻塞调用
   - [ ] IBinElevator 接口已定义并使用

4. **文档完成**
   - [ ] 架构文档已更新
   - [ ] CLAUDE.md 反映新架构

---

*文档生成时间: 2025-12-28*
*整合来源: ProductionLine_Refactoring_Tasks.md, ProductionLine_Architecture_Audit.md*
