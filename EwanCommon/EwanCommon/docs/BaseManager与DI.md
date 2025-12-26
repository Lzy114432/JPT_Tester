# BaseManager 与 DI 建议

`EwanCore.BaseManager<T>` 是典型的“静态单例 + 生命周期（Init/Dispose）”方案。

## 优点

- 使用成本低：`XXXManager.Instance()` 随处可用，适合小项目/快速落地。
- 生命周期可控：`EnsureInit/EnsureDestroy` 能避免重复初始化与重复释放。
- 懒加载：不需要显式 new，按需创建实例。

## 缺点（为什么你会觉得需要弱化）

- 隐式依赖：业务类里直接调用单例，依赖关系不可见，测试/替换实现困难。
- 全局可变状态：跨线程/跨模块更容易出现“谁改了全局状态”的排查成本。
- 难以做多实例：同一进程里想跑两套逻辑/两套硬件模拟几乎不可能。
- 初始化顺序被动：依赖扫描/静态访问顺序，容易出现“先被用到、后才 Init”的隐患。
- DI 集成麻烦：若构造函数是 private，容器无法创建；并且容易出现“DI 单例 + 静态单例”两份实例。

## 推荐做法（DI-first）

- 新项目优先依赖接口：例如 `IAlarmService`、`IMessageBus`、网关/驱动接口等。
- 用 `EwanCore.Bootstrap.ManagerLifetimeHost` 做“宿主”：
  - 由容器/工厂解析实例
  - 统一调用 `Init/Dispose`，并按 Priority 排序

## 迁移落地顺序（建议）

1. 先把“外部依赖”抽成接口（PLC/MES/IO/数据库/文件）。
2. 把流程逻辑（状态机 `LogicBase`）只依赖接口 + 数据快照。
3. 用 `ManagerLifetimeHost` 承载基础组件（日志、消息、报警、后台轮询）。
4. 新代码尽量不再新增 `BaseManager<T>` 派生类；优先用 DI 单例替代静态单例。
