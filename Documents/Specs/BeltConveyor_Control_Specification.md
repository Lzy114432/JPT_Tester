# Material Loading / Unloading & Belt Conveyor Control Specification

## 模块说明

### 1. MaterialLoadingModule（装料模块）
- 控制装料流程。
- 特殊点：需要根据 **IN20 机械手忙碌状态信号** 来控制皮带。
- 当进入 `MaterialLoadingModule.PickingMaterial`（开始抓料阶段）时，IN20 会变为脉冲/高电平。
- 当 IN20 亮起时，需要 **立即停止皮带**。
- 当装料流程完成后，需要 **重新启动皮带**。

### 2. MaterialUnloadingModule（下料模块）
- 控制下料流程。
- 下料流程进行中时，需要停止皮带。
- 当下料流程完成后，需要重新启动皮带。

### 3. BeltConveyorModule（皮带模块）
- 控制皮带启停。
- 由装料模块与下料模块共同“请求”控制。

---

## 信号说明

| 名称                                      | 类型     | 描述                                                             |
| ----------------------------------------- | -------- | ---------------------------------------------------------------- |
| **IN20**                                  | 输入信号 | 表示机械手忙碌状态（脉冲）。亮起表示机械手正在抓料               |
| **OUT_ALLOW_PICK**                        | 输出信号 | `WriteOutBit(OUT_ALLOW_PICK, true)` 时开启，表示允许抓料逻辑进入 |
| **BeltConveyorModule.StartBeltReverse()** | 方法     | 允许皮带运行                                                     |
| **BeltConveyorModule.StopBelt()**         | 方法     | 停止皮带运行                                                     |

---

## 皮带控制核心需求

### 1. 在装料流程中
- 当状态进入 `MaterialLoadingState.PickingMaterial`
- 且 **IN20 = true**（机械手开始抓料）
- → **停止皮带**

- 当装料流程完成
- → **启动皮带**

---

### 2. 在下料流程中
- 下料流程期间 → **停止皮带**
- 下料完成 → **启动皮带**

---

### 3. 只有下料流程（无装料流程）
- 因为只有下料 → **皮带保持停止，不自动启动**

---

## 皮带控制条件汇总

### 停止皮带条件（任一满足即停止）

- **Condition A（装料阶段）**  
  `MaterialLoadingState == PickingMaterial && IN20 == true`

- **Condition B（下料阶段）**  
  进入`MaterialUnloadingModule.RequestUnloading`

---

### 启动皮带条件（全部满足才启动）

- 不在装料抓料阶段  
  `!(MaterialLoadingState == PickingMaterial && IN20 == true)`

- 不在下料流程中  
  `MaterialUnloadingModule.IsUnloading == false`

- “只有下料模式”  
  `_loadingEnabled = fasle`  装料没有启用
  就是MaterialUnloadingModule就不启动皮带
