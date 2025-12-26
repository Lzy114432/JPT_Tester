# EwanCommon（抽取复用库）

这个目录是从 BondingPro 里“抽取出来用于其它项目复用”的核心设计代码，不会改动原有工程；你可以把整个 `EwanCommon/` 目录拷贝到任意 WinForms/WPF 项目中作为类库引用。

## 包含内容

- `EwanCore.BaseManager`：单例管理器基类
  - DI 建议：`docs/BaseManager与DI.md`
- `EwanCore.Bootstrap.ManagerTypeScanner`：按 `[Manager]` 扫描并排序 Manager 类型（供 `ManagerLifetimeHost` 使用）
- `EwanCore.AlarmSystem.*`：报警服务（线程安全、按 Key 去重）+ `PlcAlarmTracker<T>`（从 PLC 模型同步报警）
- `EwanCore.Messaging.*`：强类型消息总线（`MessageBus`）
  - `Publish<T>`：同步通知（适合状态变化/轻量事件）
  - `Post<T>`：异步队列分发（适合跨线程解耦/限流/请求响应）
  - `Subscribe<T>`：支持订阅基类/接口（按消息运行时类型分发）
  - `SubscribeWeak(...)`：弱引用订阅（防泄漏）
  - `RequestAsync/Respond`：内置 Request/Reply（基于 CorrelationId）
  - `ICorrelatedMessage<TKey>`：关联 ID 消息契约
  - `IMessage.Timestamp`：统一时间戳（默认由总线在 Publish/Post 时补齐）
  - `MessageHub.Current`：进程级入口（可在项目启动时替换为你自己的 bus 实例）
  - `RequestAwaiter<TKey,TResult>`：底层等待表（需要更细控制时使用）
- `EwanCore.Runner.StreamRunner` + `EwanCore.Module.*`：流程节点执行器
  - `IModule.Destroy()` 为标准销毁入口；如需兼容历史拼写，可使用 `Destory()` 扩展方法（`EwanCore.Module.Interface.ModuleExtensions`，Obsolete）
- `EwanCore.StateMachine.*`：状态机（参考 ScribingV3/Byron.Commond 的 BaseLogic/LogicThread/ControllerBox 思路）
  - `LogicBase`：`SwitchIndex` 步骤驱动 + `TimeoutWatch` 超时工具 + `StepChanged` 广播
  - `LogicRunner`：Run/Step/Stop/Pause 驱动队列，单步跑一轮后自动 Stop
  - `MachineOperator`：封装 Start/Stop/Pause/Home/ClearAlarm 常用操作
- `EwanModel.PlcBaseModel` + `[Plc]`：PLC 标签读写基类
  - 组合策略：`IPlcValueCodec`（字节序/编码）、`IPlcAddressFormatter`（地址格式）、`IPlcBitIndexMapper`（X/Y 等地址映射）
- `EwanCore.Plc.Cmd.*`：命令模式（Undo/Redo）+ `PlcCmdReceiver`（通过委托注入写入实现，便于 DI）

## 构建

在仓库根目录执行：

`dotnet msbuild .\\EwanCommon\\EwanCommon.csproj -p:Configuration=Release`

## 最小用法示例

- 设置默认解析方式（项目级一次即可）：
  - MC（小端）：`EwanModel.Plc.PlcCodecDefaults.UseMc();`
  - Modbus（大端）：`EwanModel.Plc.PlcCodecDefaults.UseModbusBigEndian();`
- 日志初始化（log4net）：
  - 方案 B（约定式）：程序目录放 `log4net.config`，启动时调用 `EwanCommon.Logging.Log4NetBootstrapper.TryConfigureByConvention();`
  - 方案 A（纯代码配置）：显式指定路径 `EwanCommon.Logging.Log4NetBootstrapper.TryConfigureFromFile(configPath);`
  - 模板：`EwanCommon/log4net.config.template`（复制到程序目录并改名为 `log4net.config`）
- DI 方式启动/销毁（不依赖静态单例）：
  - `using var host = new EwanCore.Bootstrap.ManagerLifetimeHost(t => provider.GetService(t));`
  - `host.Start(requireAll: true);`
- PLC 写入命令（注入写入委托）：
  - `var receiver = new EwanCore.Plc.Cmd.Receiver.PlcCmdReceiver(writeBytes, writeBool);`
  - `EwanCore.Plc.Cmd.CmdManager.Instance().ExecuteCommand(new EwanCore.Plc.Cmd.CommonCommand<MyModel>(receiver, exe, undo));`
- 报警（DI 友好）：
  - `var alarms = new EwanCore.AlarmSystem.AlarmService();`
  - `alarms.AlarmChanged += (_, e) => { /* e.Kind / e.Alarm */ };`
  - PLC 模型同步：`var tracker = new EwanCore.AlarmSystem.PlcAlarmTracker<MyPlcModel>(alarms);`，每次收到 PLC 快照调用 `tracker.Process(model);`
- 状态机（类似 ScribingV3）：
  - `var runner = new EwanCore.StateMachine.LogicRunner();`
  - `var op = new EwanCore.StateMachine.MachineOperator(alarms, runner);`
  - UI：`op.Start(() => new MainLogic()); op.Pause(); op.Step(); op.Home(() => new HomeLogic()); op.ClearAlarm();`
- 消息总线（强类型）：
  - `var bus = new EwanCore.Messaging.MessageBus();`
  - `EwanCore.Messaging.MessageHub.Current = bus;`
  - 订阅：`using var sub = bus.Subscribe<MyMessage>(m => { /* ... */ });`
  - 弱引用订阅：`bus.SubscribeWeak(this, (me, MyMessage m) => me.OnMsg(m));`
  - 同步发布：`bus.Publish(new MyMessage(...));`
  - 异步发布：`bus.Post(new MyMessage(...));`
  - Request/Reply：`await bus.RequestAsync<MyReq, MyRes>(new MyReq(), timeoutMs: 3000);`（MyReq/MyRes 实现 `ICorrelatedMessage<Guid>`）
- 订阅步骤变化（全局）：`EwanCore.Messaging.MessageHub.Current.Subscribe<EwanCore.StateMachine.StepChangedEventArgs>(args => { /* args.ToStep */ });`
- MessageBus 用法示例（订阅/发布/UI 线程切换/取消订阅）：`EwanCommon/examples/UiMessageBusSnippets.md`

## 示例文件（推荐先看）

`EwanCommon/examples/README.md`
