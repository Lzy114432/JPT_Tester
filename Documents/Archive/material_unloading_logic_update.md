# 下料流程联动逻辑改造说明

## 1. 涉及模块
- `MaterialUnloadingModule.cs`
- `BinElevatorModule.cs`

---

## 2. 改造目标
在正式下料前由 `BinElevatorModule` 统一完成“料仓是否有料”的检测，并将结果反馈给 `MaterialUnloadingModule`：

- 检测到有料 → 正常执行下料动作。
- 超时仍无料感应 → 判定料仓为空，立即释放空车。

---

## 3. 逻辑调整详解

### 3.1 MaterialUnloadingModule
1. **ProcessIdleState**：
   - 在切入下料流程之前调用 `_binElevator?.RaiseToSensor(binNumber)`。
   - 根据返回状态分支：
     - `HasMaterial = true` → 继续原下料工序。
     - `IsBinEmpty = true` → 调用 `SendCartCompletionToModbus(false)` 走无料/空车流程，并回到空闲。
2. **RequestUnloading**：移除原先直接调用 `_binElevator?.RaiseToSensor(binNumber)` 的代码，统一由 `ProcessIdleState` 触发。
3. **SendCartCompletionToModbus(bool hasMaterial)**：
   - `hasMaterial = true` → `_modbusRTUManager.WriteAny(MATERIAL_STATUS_REGISTER, (ushort)1)`（正常下料）。
   - `hasMaterial = false` → `_modbusRTUManager.WriteAny(MATERIAL_STATUS_REGISTER, (ushort)0)`（空车离开）。

### 3.2 BinElevatorModule
1. **RaiseToSensor**：
   - 若传感器已检测到物料则无需继续升降，直接返回 `HasMaterial = true`。
   - 若未检测到物料则执行升降动作并进入 `ProcessUnloadingMode`。
2. **ProcessUnloadingMode**（扩展）：
   - 在指定超时时间（建议 5s，可配置）内循环检测“有料感应”输入位。
   - 期间若检测到物料 → 返回 `HasMaterial = true`。
   - 超时仍无料 → 返回 `IsBinEmpty = true`。
3. 将检测结果回传给 `MaterialUnloadingModule`，由后者决定走“下料”还是“空车”分支。

---

## 4. Modbus 状态写入
- 料仓有料并已下料 → 写入 `1`（MATERIAL_STATUS_REGISTER）。
- 料仓无料，释放空车 → 写入 `0`。

该写入仅通过更新后的 `SendCartCompletionToModbus` 方法完成，避免重复逻辑。

