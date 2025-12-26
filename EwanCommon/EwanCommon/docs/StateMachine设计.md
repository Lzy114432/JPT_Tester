# StateMachine 设计（参考 ScribingV3 / Byron.Commond）

## ScribingV3 那套“顺手”的点

- 逻辑类里用 `switch(SwitchIndex)` 写步骤，非常直观
- `TimeoutWatch` 以步骤名为 key 做超时
- `LogicThread` 维护逻辑队列，支持 Run / Step / Stop（单步跑一轮后自动 Stop）
- `ControllerBox` 聚合多个 `LogicThread`，做到“一键启停/单步”

## EwanCommon 的对应实现

命名空间：`EwanCore.StateMachine`

- `LogicBase`：对标 `BaseLogic`
  - `SwitchIndex`：步骤名（默认“初始状态/结束状态”）
  - `TimeoutWatch Tw`：超时工具
  - `AddCurSonLogic/GetLogicState`：用于“监控显示”的子逻辑组合（对标 Byron 的状态字符串）
  - `StepChanged`：步骤切换事件，并默认通过 `MessageHub.Current` 广播 `StepChangedEventArgs`（强类型，无 topic 字符串）
- `LogicRunner`：对标 `LogicThread`
  - 队列驱动 `LogicBase.Handler()`
  - `RunTag`：`Run/Step/Stop/Pause`
  - `Step()`：执行一轮后自动切换为 `Stop`
  - `LogicException`：逻辑异常时自动切到 Stop，避免线程直接崩溃
- `LogicController`：对标 `ControllerBox`
  - 聚合多个 `LogicRunner`，统一 `Start/Stop/Pause/Step`
- `MachineOperator`：面向“监控/主控 UI 按钮”的约定封装
  - `Start/Stop/Pause/Step/Home/ClearAlarm`

## 典型落地方式（监控项目）

1. 把 “主流程” 和 “复位流程” 各写成一个 `LogicBase`（内部 `switch(SwitchIndex)`）
2. UI 按钮调用 `MachineOperator`：
   - 启动：`Start(() => new MainLogic())`
   - 暂停：`Pause()`
   - 单步：`Step()`
   - 复位：`Home(() => new HomeLogic())`
   - 清报警：`ClearAlarm()`
3. 把 `StepChanged` / `AlarmChanged` 订阅到界面刷新（注意跨线程）
