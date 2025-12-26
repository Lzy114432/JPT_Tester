# EwanCommon（工业自动化通用库）

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2+-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

EwanCommon 是一个面向工业自动化/监控项目的 C# 通用库，提供消息总线、报警管理、状态机、PLC 通讯等核心能力。可直接引用到 WinForms/WPF 项目中使用。

## 目录

- [特性概览](#特性概览)
- [快速开始](#快速开始)
- [项目结构](#项目结构)
- [核心模块](#核心模块)
- [构建](#构建)
- [示例代码](#示例代码)
- [设计文档](#设计文档)
- [依赖项](#依赖项)

## 特性概览

| 模块 | 说明 | 主要类型 |
|------|------|----------|
| **消息总线** | 强类型、支持同步/异步、Request/Reply | `MessageBus`, `MessageHub` |
| **报警系统** | 线程安全、按 Key 去重、PLC 报警同步 | `AlarmService`, `PlcAlarmTracker<T>` |
| **状态机** | 步骤驱动、超时管理、全局广播 | `LogicBase`, `LogicRunner`, `MachineOperator` |
| **流程执行器** | 模块化、后台循环执行 | `StreamRunner`, `BaseModule<T>` |
| **生命周期管理** | DI 友好、优先级排序、自动销毁 | `BaseManager<T>`, `ManagerLifetimeHost` |
| **PLC 通讯** | 反射解析、字节序适配、命令模式 | `PlcBaseModel`, `IPlcValueCodec` |
| **日志** | log4net 集成、约定式配置 | `Log`, `Log4NetBootstrapper` |

## 快速开始

### 1. 引用库

将 `EwanCommon.dll` 引用到你的项目，或直接复制 `EwanCommon/` 目录作为项目引用。

### 2. 初始化日志

```csharp
// 约定式（程序目录放 log4net.config）
EwanCommon.Logging.Log4NetBootstrapper.TryConfigureByConvention();

// 或显式指定路径
EwanCommon.Logging.Log4NetBootstrapper.TryConfigureFromFile("path/to/log4net.config");
```

### 3. 设置消息总线

```csharp
using EwanCore.Messaging;

var bus = new MessageBus(new MessageBusOptions
{
    AsyncQueueCapacity = 1024,
    OverflowStrategy = MessageOverflowStrategy.DropOldest,
});
MessageHub.Current = bus;
```

### 4. 配置 PLC 字节序（可选）

```csharp
// MC 协议（小端）
EwanModel.Plc.PlcCodecDefaults.UseMc();

// 或 Modbus（大端）
EwanModel.Plc.PlcCodecDefaults.UseModbusBigEndian();
```

### 5. 使用消息总线

```csharp
// 定义消息
public sealed class RecipeRead : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public string RecipeName { get; set; }
}

// 订阅
using var sub = MessageHub.Current.Subscribe<RecipeRead>(m =>
{
    Console.WriteLine($"Recipe: {m.RecipeName}");
});

// 发布
MessageHub.Current.Publish(new RecipeRead { RecipeName = "R001" });
```

## 项目结构

```
EwanCommon/
├── EwanCore/                    # 核心框架
│   ├── AlarmSystem/             # 报警系统
│   │   ├── Alarm.cs             # 报警实体
│   │   ├── AlarmService.cs      # 报警服务（IAlarmService）
│   │   └── PlcAlarmTracker.cs   # PLC 报警同步器
│   ├── Attribute/               # 特性定义
│   │   └── ManagerAttribute.cs  # [Manager] 特性
│   ├── Bootstrap/               # 启动引导
│   │   ├── ManagerLifetimeHost.cs   # DI 友好的生命周期宿主
│   │   └── ManagerTypeScanner.cs    # Manager 类型扫描器
│   ├── Lifecycle/               # 生命周期管理
│   │   ├── BaseManager.cs       # 单例管理器基类
│   │   ├── IManager.cs          # Manager 接口
│   │   └── IAsyncManager.cs     # 异步 Manager 接口
│   ├── Messaging/               # 消息总线
│   │   ├── Abstractions/        # 接口定义
│   │   ├── Contracts/           # 消息契约
│   │   ├── Core/                # 核心实现
│   │   └── Extensions/          # 扩展方法
│   ├── Module/                  # 流程模块
│   │   ├── BaseModule.cs        # 模块基类
│   │   └── Interface/           # IModule 接口
│   ├── Plc/                     # PLC 命令
│   │   └── Cmd/                 # 命令模式
│   ├── Runner/                  # 流程执行器
│   │   └── StreamRunner.cs      # 流程运行器
│   ├── StateMachine/            # 状态机
│   │   ├── Engine/              # 引擎（LogicRunner, MachineOperator）
│   │   ├── Events/              # 事件
│   │   ├── Logic/               # 逻辑基类
│   │   └── Runtime/             # 运行时工具
│   └── Utils/                   # 工具类
├── EwanModel/                   # 数据模型
│   ├── Attribute/               # [Plc] 特性
│   ├── Common/                  # 通用枚举
│   ├── Const/                   # 常量
│   └── Plc/                     # PLC 解析策略
├── Logging/                     # 日志
│   ├── Log.cs                   # 日志入口
│   └── Log4NetBootstrapper.cs   # log4net 配置
├── docs/                        # 设计文档
├── examples/                    # 示例代码
└── log4net.config.template      # 日志配置模板
```

## 核心模块

### 1. 消息总线（MessageBus）

强类型消息总线，支持同步/异步发布、弱引用订阅、Request/Reply 模式。

```csharp
// 同步发布（轻量通知）
bus.Publish(new MyMessage());

// 异步发布（队列分发）
bus.Post(new MyMessage());

// 弱引用订阅（防泄漏）
bus.SubscribeWeak(this, (me, MyMessage msg) => me.OnMessage(msg));

// Request/Reply
var result = await bus.RequestAsync<MesRequest, MesResult>(
    new MesRequest { Api = "Confirm" },
    timeoutMs: 5000);
```

详见：[MessageBus 设计方案](docs/MessageBus设计方案.md)

### 2. 报警系统（AlarmService）

线程安全的报警服务，支持按 Key 去重、重复触发刷新时间。

```csharp
var alarms = new AlarmService();

// 添加报警
alarms.AddAlarm("传感器故障", AlarmLevel.H, needReset: true, key: "Sensor.Error");

// 订阅变化
alarms.AlarmChanged += (_, e) =>
{
    Console.WriteLine($"{e.Kind}: {e.Alarm?.Content}");
};

// PLC 报警同步
var tracker = new PlcAlarmTracker<MyPlcModel>(alarms);
tracker.Process(plcSnapshot);
```

详见：[AlarmSystem 设计](docs/AlarmSystem设计.md)

### 3. 状态机（StateMachine）

基于步骤驱动的状态机，支持超时管理和全局广播。

```csharp
public class MainLogic : LogicBase
{
    public override void Handler()
    {
        switch (SwitchIndex)
        {
            case "初始状态":
                SwitchIndex = "等待就绪";
                break;
            case "等待就绪":
                if (IsReady)
                    SwitchIndex = "执行动作";
                else if (Tw.StartCheckIsTimeout(SwitchIndex, 5000))
                    Complete(); // 超时结束
                break;
            case "执行动作":
                DoAction();
                Complete();
                break;
        }
    }
}

// 使用
var runner = new LogicRunner();
var op = new MachineOperator(alarms, runner);
op.Start(() => new MainLogic());
```

详见：[StateMachine 设计](docs/StateMachine设计.md)

### 4. 流程执行器（StreamRunner）

后台循环执行模块列表，适合 PLC 轮询、数据采集等场景。

```csharp
public class PlcPollingModule : BaseModule<PlcPollingModule>
{
    protected override void OnInit() { /* 初始化 */ }

    protected override bool OnRun()
    {
        // 轮询 PLC
        var data = ReadPlc();
        MessageHub.Current.Post(new PlcDataUpdated(data));
        Thread.Sleep(100);
        return true; // 继续运行
    }

    protected override void OnDestroy() { /* 清理 */ }
}

var modules = new List<IModule> { new PlcPollingModule() };
var runner = new StreamRunner(modules);
runner.Start();
```

### 5. 生命周期管理（ManagerLifetimeHost）

DI 友好的 Manager 生命周期管理，按优先级初始化/销毁。

```csharp
// 定义 Manager
[Manager(Priority = 1)]
public class PlcManager : BaseManager<PlcManager>
{
    public override bool Init()
    {
        // 初始化 PLC 连接
        return base.Init();
    }
}

// 启动
using var host = new ManagerLifetimeHost(t => container.GetService(t));
host.Start(requireAll: true);
```

详见：[BaseManager 与 DI](docs/BaseManager与DI.md)

### 6. PLC 通讯（PlcBaseModel）

通过反射和 `[Plc]` 特性自动解析 PLC 数据。

```csharp
public class MyPlcModel : PlcBaseModel
{
    [Plc(Prefix = "D", Addr = 100)]
    public int ProductCount { get; set; }

    [Plc(Prefix = "M", Addr = 0)]
    public bool IsRunning { get; set; }

    [Plc(Prefix = "D", Addr = 200, Len = 20)]
    public string RecipeName { get; set; }
}

// 解析数据
var model = new MyPlcModel();
model.ResolveByte(plcBytes, "D");
model.ResolveBool(plcBools, "M");
```

## 构建

### 使用 MSBuild

```bash
# Debug
dotnet msbuild EwanCommon.csproj -p:Configuration=Debug

# Release
dotnet msbuild EwanCommon.csproj -p:Configuration=Release
```

### 使用 Visual Studio

直接打开 `EwanCommon.sln` 并构建。

## 示例代码

示例文件位于 `examples/` 目录，不参与编译，可直接复制到项目中使用。

| 示例文件 | 说明 |
|----------|------|
| `RequestReplyExample.cs` | Request/Reply 模式 |
| `AlarmAndStepChangedExample.cs` | 报警与步骤变化订阅 |
| `StreamRunnerExample.cs` | 流程执行器 |
| `ManagerLifetimeHostExample.cs` | DI 启动示例 |
| `PlcCodecExample.cs` | PLC 字节序配置 |
| `CmdManagerExample.cs` | 命令模式 |
| `UiMessageBusSnippets.md` | UI 线程消息订阅 |
| `WpfAutoDisposeSubscriptionExample.md` | WPF 自动解绑 |

详细说明：[examples/README.md](examples/README.md)

## 设计文档

| 文档 | 说明 |
|------|------|
| [系统架构设计](docs/系统架构设计.md) | 整体架构与组件分层 |
| [MessageBus 设计方案](docs/MessageBus设计方案.md) | 消息总线详细设计 |
| [AlarmSystem 设计](docs/AlarmSystem设计.md) | 报警系统设计 |
| [StateMachine 设计](docs/StateMachine设计.md) | 状态机设计 |
| [BaseManager 与 DI](docs/BaseManager与DI.md) | 生命周期与依赖注入 |
| [Messaging API 参考](docs/Messaging-API参考.md) | 消息总线 API 详解 |
| [StreamContext 共享上下文](docs/StreamContext共享上下文设计.md) | 模块间数据共享 |

## 依赖项

- **.NET Framework 4.7.2+**
- **log4net** - 日志框架
- **System.Collections.Immutable** - 不可变集合
- **Microsoft.Bcl.AsyncInterfaces** - 异步接口

## 许可证

MIT License

---

## 快速参考

### 单例模式

```csharp
// 正确用法
var manager = MyManager.Instance();

// 错误用法（不支持）
var manager = MyManager.Instance;
```

### 消息定义

```csharp
public sealed class MyMessage : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public string Data { get; set; }
}
```

### 报警级别

| 级别 | 说明 |
|------|------|
| `AlarmLevel.L` | 低级别（信息） |
| `AlarmLevel.M` | 中级别（警告） |
| `AlarmLevel.H` | 高级别（需复位） |

### Manager 优先级

| Priority | 用途 |
|----------|------|
| 0 | 配置/基础设施 |
| 1 | 外部通讯 |
| 2 | 业务模块 |
| 99 | 默认值 |
