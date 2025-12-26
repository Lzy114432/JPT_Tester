# EwanCommon 示例总览

这些文件都是“用法示例”，默认不参与 `EwanCommon.csproj` 编译；你可以把示例文件直接拷贝到你的 WPF/WinForms/Console 项目里（引用 `EwanCommon.dll`）运行/改造。

## 1) 日志（log4net）

- 模板：`EwanCommon/log4net.config.template`（复制到程序目录并改名为 `log4net.config`）
- 启动初始化：`EwanCommon.Logging.Log4NetBootstrapper.TryConfigureByConvention();`

## 2) 消息总线（MessageBus）+ Request/Reply

- 推荐示例（内置 Request/Reply）：`EwanCommon/examples/RequestReplyExample.cs`
  - 演示：`RequestAsync` + `Respond`（CorrelationId 自动配合）
- 底层示例（更细控制时）：`EwanCommon/examples/RequestAwaiterExample.cs`
  - 演示：手工 `RequestAwaiter.Register/TrySetResult` 桥接

## 3) 报警（AlarmService）

- 示例（含 MES 失败/流程异常如何报警、以及 StepChanged 订阅）：`EwanCommon/examples/AlarmAndStepChangedExample.cs`
- UI 订阅（WPF/WinForms 切回 UI 线程刷新）：`EwanCommon/examples/UiAlarmSubscribeSnippets.md`
- UI 操作（MachineOperator 启停/暂停/复位/清警）：`EwanCommon/examples/UiMachineOperatorSnippets.md`

## 4) 步骤状态机（LogicBase/LogicRunner/MachineOperator）+ StepChanged

- 示例：`EwanCommon/examples/AlarmAndStepChangedExample.cs`
  - `LogicBase.SwitchIndex` 每次变化都会产生 `StepChangedEventArgs`
  - 默认会通过 `MessageHub.Current` 广播 `StepChangedEventArgs`（强类型），便于 UI/监控统一订阅

## 5) DI-first 启动/释放（ManagerLifetimeHost）

- 示例：`EwanCommon/examples/ManagerLifetimeHostExample.cs`
  - 不依赖任何第三方 DI 包，用一个最小 `IServiceProvider` 演示

## 6) 后台轮询 Runner（StreamRunner + IModule）

- 示例：`EwanCommon/examples/StreamRunnerExample.cs`
  - 演示：如何写模块（PLC 轮询 / MES 发送）并通过 `MessageBus` 解耦
- 推荐写法（MES 需要等待返回/超时）：`EwanCommon/examples/MesUploadBestPracticeExample.cs`
  - 演示：`RequestAsync + RespondAsync`（流程侧不阻塞 `Run()`，后台侧异步发送 + 可限并发/重试）
- 共享上下文设计：`EwanCommon/docs/StreamContext共享上下文设计.md`
  - 演示：`SetObject(ctx)` 注入共享状态和服务引用，`MessageBus` 与 `StreamContext` 配合使用

## 7) PLC 标签解析（PlcBaseModel）+ 项目级默认字节序（MC/Modbus）

- 示例：`EwanCommon/examples/PlcCodecExample.cs`
  - 演示：`PlcCodecDefaults.UseMc()` / `UseModbusBigEndian()` / `Set(...)`
  - 演示：`[Plc(ByteOrder=Auto)]` 项目默认 + 特殊标签覆盖

## 8) 命令模式（CmdManager + CommonCommand + PlcCmdReceiver）

- 示例：`EwanCommon/examples/CmdManagerExample.cs`
  - 演示：通过委托注入写入实现（方便替换不同 PLC 驱动）

## 9) MessageBus（Publish/Post）

- 示例（订阅/发布/UI 线程切换/取消订阅注意事项）：`EwanCommon/examples/UiMessageBusSnippets.md`

## 10) WinForms/WPF：订阅 + 自动随窗口销毁解绑

- WinForms 例子程序（含扩展方法）：`EwanCommon/examples/WinFormsAutoDisposeSubscriptionExample.cs`
- WPF 例子程序（文档版，含扩展方法 + XAML）：`EwanCommon/examples/WpfAutoDisposeSubscriptionExample.md`
