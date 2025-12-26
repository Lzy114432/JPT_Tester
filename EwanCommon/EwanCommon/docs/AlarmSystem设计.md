# AlarmSystem 设计（对标 Byron.AlarmSystem）

## Byron.AlarmSystem.dll 是怎么做的（结论）

- 类型只有 2 个：`Alarm` + `AlarmCollection`
- `Alarm`：`Content / AlarmTime / NeedReset / Owner`
- `AlarmCollection`：内部是 `List<Alarm>` + `syncRoot` 加锁
  - `AddAlarm(content, needReset=false, owner=null)`：按 `Content` 去重，新增后触发 `AlarmListChanged`
  - `Remove(alarm)`：按 `Content` 删除，触发 `AlarmListChanged`
  - `Clear()`：清空并触发 `AlarmListChanged`（即便原本为空也会触发）
  - `NeedReset()`：只要任一报警 `NeedReset=true` 就返回 true

它非常轻量，核心价值是：**去重 + 变更事件 + NeedReset 汇总**。

## EwanCommon 的实现（面向监控项目/DI）

命名空间：`EwanCore.AlarmSystem`

- `AlarmService`：线程安全、按 `Alarm.Key` 去重；重复触发会更新 `AlarmTime` 并递增 `Occurrence`
- `Alarm`：增加了 `Key/Unit/Level/Occurrence`，依然保留 `Content/AlarmTime/NeedReset/Owner` 的核心语义
- `IAlarmService`：用于 DI 注入（状态机/监控/UI 都依赖接口而不是静态全局）
- `PlcAlarmTracker<TModel>`：把 PLC 模型上 `[Plc(IsAlarmProperty=true)]` 的 bool 属性同步进 `AlarmService`
  - 0→1：`AddAlarm(...)`
  - 1→0：`RemoveByKey(...)`
  - `Key` 默认用属性名（例如 `M10000`），`Content` 用 `AlarmDesc`
  - `NeedReset` 优先用 `PlcAttribute.NeedReset`；未配置时默认 `H` 级报警需要复位

## 建议用法

- 监控项目里把 `IAlarmService` 注册为单例（或直接 new `AlarmService` 单例）
- PLC 通讯收到数据快照时调用 `PlcAlarmTracker.Process(model)`
- UI 订阅 `AlarmChanged/AlarmListChanged` 刷新报警面板；需要跨线程时自行 marshal 到 UI 线程

