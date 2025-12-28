# Module 层清理任务清单

> **状态**: ✅ 已完成
> **创建日期**: 2025-12-28
> **完成日期**: 2025-12-28
> **代码变更**: -2878 行

## 1. 背景

系统存在两套并行的实现：
- **Logic 层** (活跃使用): `LogicManager` → `MainLogic` → `MaterialLoadingLogic` / `MaterialUnloadingLogic`
- **Module 层** (未使用): `ProductionLineModule` → `MaterialLoadingModule` / `MaterialUnloadingModule`

经确认，Module 层未在生产代码中使用，只有测试文件引用。

## 2. 清理目标

删除未使用的 Module 层代码，保留 Logic 层作为唯一实现。

---

## 3. 待删除文件清单

### 3.1 Module 层源文件

| 文件路径 | 说明 |
|----------|------|
| `Ewan.Core\Module\ProductionLineModule.cs` | 生产线统一控制模块（未使用） |
| `Ewan.Core\Module\MaterialLoadingModule.cs` | 物料装载模块（未使用） |
| `Ewan.Core\Module\MaterialUnloadingModule.cs` | 物料卸载模块（未使用） |

### 3.2 对应测试文件

| 文件路径 | 说明 |
|----------|------|
| `Ewan.Core.Tests\Module\ProductionLineModuleStateTests.cs` | ProductionLineModule 测试 |
| `Ewan.Core.Tests\Module\MaterialLoadingModuleStateTests.cs` | MaterialLoadingModule 测试 |
| `Ewan.Core.Tests\Module\MaterialUnloadingModuleStateTests.cs` | MaterialUnloadingModule 测试 |

---

## 4. 任务分解

### Epic: Module 层清理

#### T1: 删除 ProductionLineModule

**描述**: 删除未使用的生产线统一控制模块

**待删除文件**:
- `Ewan.Core\Module\ProductionLineModule.cs`
- `Ewan.Core.Tests\Module\ProductionLineModuleStateTests.cs`

**验收标准**:
- [ ] 文件已删除
- [ ] 项目编译通过
- [ ] 无其他文件引用报错

---

#### T2: 删除 MaterialLoadingModule

**描述**: 删除未使用的物料装载模块

**待删除文件**:
- `Ewan.Core\Module\MaterialLoadingModule.cs`
- `Ewan.Core.Tests\Module\MaterialLoadingModuleStateTests.cs`

**验收标准**:
- [ ] 文件已删除
- [ ] 项目编译通过
- [ ] 无其他文件引用报错

---

#### T3: 删除 MaterialUnloadingModule

**描述**: 删除未使用的物料卸载模块

**待删除文件**:
- `Ewan.Core\Module\MaterialUnloadingModule.cs`
- `Ewan.Core.Tests\Module\MaterialUnloadingModuleStateTests.cs`

**验收标准**:
- [ ] 文件已删除
- [ ] 项目编译通过
- [ ] 无其他文件引用报错

---

#### T4: 更新注释引用

**描述**: 更新 SafetyModule.cs 中的注释，将 "ProductionLineModule" 改为 "LogicManager"

**修改文件**:
- `Ewan.Core\Module\SafetyModule.cs`

**修改内容**:
- 第331行: `// 发送系统控制命令到ProductionLineModule` → `// 发送系统控制命令到LogicManager`
- 第359行: 同上

**验收标准**:
- [ ] 注释已更新
- [ ] 项目编译通过

---

#### T5: 编译验证

**描述**: 完整编译验证，确保删除后无任何问题

**验收标准**:
- [ ] 解决方案完整编译通过
- [ ] 无警告（除已知警告外）
- [ ] 单元测试通过

---

#### T6: 更新相关文档

**描述**: 更新架构重构文档，标记 Module 层清理完成

**修改文件**:
- `Documents\MainLogic_SerialPolling_Refactoring.md`

**验收标准**:
- [ ] 文档已更新

---

## 5. 执行顺序

```
T1 删除 ProductionLineModule
    ↓
T2 删除 MaterialLoadingModule
    ↓
T3 删除 MaterialUnloadingModule
    ↓
T4 更新注释引用
    ↓
T5 编译验证
    ↓
T6 更新文档
```

---

## 6. 风险评估

| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|----------|
| 存在未发现的引用 | 中 | 低 | 编译验证会暴露问题 |
| 测试依赖未清理 | 低 | 低 | 删除对应测试文件 |

---

## 7. 预计工作量

| 任务 | 预计时间 |
|------|----------|
| T1-T3 删除文件 | 10 分钟 |
| T4 更新注释 | 5 分钟 |
| T5 编译验证 | 10 分钟 |
| T6 更新文档 | 5 分钟 |
| **合计** | **约 30 分钟** |
