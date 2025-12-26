# EwanCommon - Claude Code 开发指南

本文档为 Claude Code 提供 EwanCommon 库的开发指南和快速参考。

## 目录

- [快速参考](#快速参考)
- [项目概述](#项目概述)
- [核心模式](#核心模式)
- [常见任务](#常见任务)
- [代码规范](#代码规范)
- [故障排查](#故障排查)

---

## 快速参考

### 构建命令

```bash
# Debug 构建
dotnet msbuild EwanCommon.csproj -p:Configuration=Debug

# Release 构建
dotnet msbuild EwanCommon.csproj -p:Configuration=Release
```

### 关键路径

```
EwanCommon/
├── EwanCore/              # 核心框架代码
│   ├── AlarmSystem/       # 报警系统
│   ├── Bootstrap/         # 启动引导
│   ├── Lifecycle/         # 生命周期（BaseManager）
│   ├── Messaging/         # 消息总线
│   ├── Module/            # 流程模块
│   ├── Runner/            # StreamRunner
│   └── StateMachine/      # 状态机
├── EwanModel/             # 数据模型
│   └── Plc/               # PLC 相关
├── Logging/               # 日志配置
├── docs/                  # 设计文档
└── examples/              # 示例代码
```

### 单例模式（重要）

```csharp
// 正确：使用 Instance() 方法
var manager = MyManager.Instance();

// 错误：不要使用 Instance 属性
var manager = MyManager.Instance;  // 不存在此属性！
```

---

## 项目概述

**EwanCommon** 是面向工业自动化的通用库，提供：

| 模块 | 命名空间 | 说明 |
|------|----------|------|
| 消息总线 | `EwanCore.Messaging` | 强类型、同步/异步、Request/Reply |
| 报警系统 | `EwanCore.AlarmSystem` | 线程安全、按 Key 去重 |
| 状态机 | `EwanCore.StateMachine` | 步骤驱动、超时管理 |
| 流程执行器 | `EwanCore.Runner` | 后台循环执行 |
| 生命周期 | `EwanCore` | BaseManager 单例模式 |
| PLC 通讯 | `EwanModel` | 反射解析、字节序适配 |

---

## 核心模式

### 1. BaseManager 单例模式

```csharp
using EwanCore;

// 定义 Manager
[Manager(Priority = 1)]  // 0=最先初始化, 99=默认
public class PlcManager : BaseManager<PlcManager>
{
    // 私有构造函数（可选，防止外部 new）
    private PlcManager() { }

    public override bool Init()
    {
        // 初始化逻辑
        s_logger.Info("PlcManager 初始化");
        return base.Init();
    }

    public override void Destroy()
    {
        // 清理逻辑
        s_logger.Info("PlcManager 销毁");
        base.Destroy();
    }
}

// 使用
var manager = PlcManager.Instance();
```

### 2. 消息总线模式

```csharp
using EwanCore.Messaging;

// 定义消息
public sealed class PlcDataUpdated : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public byte[] Data { get; set; }
}

// 初始化总线
var bus = new MessageBus(new MessageBusOptions
{
    AsyncQueueCapacity = 1024,
    OverflowStrategy = MessageOverflowStrategy.DropOldest,
});
MessageHub.Current = bus;

// 订阅（返回 IDisposable，用于取消订阅）
using var sub = bus.Subscribe<PlcDataUpdated>(msg =>
{
    Console.WriteLine($"收到数据: {msg.Data.Length} bytes");
});

// 同步发布（在调用线程执行）
bus.Publish(new PlcDataUpdated { Data = new byte[100] });

// 异步发布（入队后台线程执行）
bus.Post(new PlcDataUpdated { Data = new byte[100] });
```

### 3. Request/Reply 模式

```csharp
// 定义请求和响应
public sealed class MesRequest : IMessage, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Api { get; set; }
}

public sealed class MesResult : IMessage, ICorrelatedMessage<Guid>
{
    public Guid CorrelationId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool Success { get; set; }
}

// 响应端
using var responder = bus.Respond<MesRequest, MesResult>(req =>
{
    return new MesResult { Success = true };
});

// 请求端
var result = await bus.RequestAsync<MesRequest, MesResult>(
    new MesRequest { Api = "Confirm" },
    timeoutMs: 5000);
```

### 4. 报警系统模式

```csharp
using EwanCore.AlarmSystem;

var alarms = new AlarmService();

// 订阅报警变化
alarms.AlarmChanged += (_, e) =>
{
    switch (e.Kind)
    {
        case AlarmChangeKind.Added:
            Console.WriteLine($"新报警: {e.Alarm.Content}");
            break;
        case AlarmChangeKind.Removed:
            Console.WriteLine($"报警消除: {e.Alarm.Content}");
            break;
    }
};

// 添加报警（按 key 去重）
alarms.AddAlarm("传感器故障", AlarmLevel.H, needReset: true, key: "Sensor.Error");

// 检查是否有需要复位的报警
if (alarms.HasNeedResetAlarm)
{
    // 需要用户复位
}

// 按 key 移除报警
alarms.RemoveByKey("Sensor.Error");

// 清除所有报警
alarms.Clear();
```

### 5. 状态机模式

```csharp
using EwanCore.StateMachine;

public class MainLogic : LogicBase
{
    private readonly IMyService _service;

    public MainLogic(IMyService service)
    {
        _service = service;
    }

    public override void Handler()
    {
        switch (SwitchIndex)
        {
            case "初始状态":
                // 开始执行
                SwitchIndex = "等待条件";
                break;

            case "等待条件":
                if (_service.IsReady)
                {
                    SwitchIndex = "执行动作";
                }
                else if (Tw.StartCheckIsTimeout(SwitchIndex, 5000))
                {
                    // 超时处理
                    Complete();
                }
                break;

            case "执行动作":
                _service.Execute();
                SwitchIndex = "等待完成";
                break;

            case "等待完成":
                if (_service.IsDone)
                {
                    Complete();  // 设置 IsFinish = true, SwitchIndex = "结束状态"
                }
                break;
        }
    }
}

// 使用 MachineOperator
var runner = new LogicRunner();
var op = new MachineOperator(alarms, runner);

op.Start(() => new MainLogic(myService));  // 启动
op.Pause();                                 // 暂停
op.Step();                                  // 单步执行
op.Stop();                                  // 停止
op.Home(() => new HomeLogic());             // 复位
op.ClearAlarm();                            // 清除报警
```

### 6. 流程模块模式

```csharp
using EwanCore.Module;
using EwanCore.Runner;

public class PlcPollingModule : BaseModule<PlcPollingModule>
{
    private IPlcClient _plc;

    protected override void OnInit()
    {
        _plc = new PlcClient();
        _plc.Connect();
        s_logger.Info("PLC 连接成功");
    }

    protected override bool OnRun()
    {
        try
        {
            var data = _plc.Read();
            MessageHub.Current.Post(new PlcDataUpdated { Data = data });
            Thread.Sleep(100);  // 控制轮询频率
            return true;  // 继续运行
        }
        catch (Exception ex)
        {
            s_logger.Error("PLC 读取失败", ex);
            return false;  // 停止流程
        }
    }

    protected override void OnDestroy()
    {
        _plc?.Disconnect();
        s_logger.Info("PLC 已断开");
    }
}

// 使用
var modules = new List<IModule>
{
    new PlcPollingModule(),
    new MesModule(),
};
var runner = new StreamRunner(modules);
runner.Start();

// 停止
runner.Stop();
```

### 7. PLC 模型模式

```csharp
using EwanModel;
using EwanModel.Attribute;

public class MyPlcModel : PlcBaseModel
{
    // 整数类型
    [Plc(Prefix = "D", Addr = 100)]
    public int ProductCount { get; set; }

    // 浮点类型
    [Plc(Prefix = "D", Addr = 102)]
    public float Temperature { get; set; }

    // 布尔类型
    [Plc(Prefix = "M", Addr = 0)]
    public bool IsRunning { get; set; }

    // 字符串类型
    [Plc(Prefix = "D", Addr = 200, Len = 20)]
    public string RecipeName { get; set; }

    // 报警属性
    [Plc(Prefix = "M", Addr = 100, IsAlarmProperty = true, AlarmDesc = "急停按下")]
    public bool EmergencyStop { get; set; }
}

// 解析数据
var model = new MyPlcModel();
model.ResolveByte(plcBytes, "D");   // 解析 D 区数据
model.ResolveBool(plcBools, "M");   // 解析 M 区数据

// PLC 报警同步
var tracker = new PlcAlarmTracker<MyPlcModel>(alarms);
tracker.Process(model);  // 自动同步报警状态
```

---

## 常见任务

### 添加新的 Manager

1. 创建类继承 `BaseManager<T>`
2. 添加 `[Manager]` 特性
3. 重写 `Init()` 和 `Destroy()`

```csharp
[Manager(Priority = 2)]
public class MyManager : BaseManager<MyManager>
{
    private MyManager() { }

    public override bool Init()
    {
        // 初始化
        return true;
    }
}
```

### 添加新的消息类型

1. 实现 `IMessage` 接口
2. 需要 Request/Reply 时实现 `ICorrelatedMessage<Guid>`

```csharp
public sealed class MyMessage : IMessage
{
    public DateTimeOffset Timestamp { get; set; }
    public string Content { get; set; }
}
```

### 添加新的流程模块

1. 创建类继承 `BaseModule<T>`
2. 实现 `OnInit()`、`OnRun()`、`OnDestroy()`
3. 添加到 `StreamRunner`

### 添加新的状态机逻辑

1. 创建类继承 `LogicBase`
2. 实现 `Handler()` 方法
3. 使用 `SwitchIndex` 控制步骤
4. 使用 `Tw.StartCheckIsTimeout()` 处理超时

---

## 代码规范

### 命名规范

| 类型 | 规范 | 示例 |
|------|------|------|
| Manager | `XxxManager` | `PlcManager`, `AlarmManager` |
| 消息 | 动词过去式或名词 | `DataUpdated`, `MesRequest` |
| 模块 | `XxxModule` | `PlcPollingModule` |
| 逻辑 | `XxxLogic` | `MainLogic`, `HomeLogic` |

### 日志规范

```csharp
// 使用 s_logger（静态，来自 BaseManager/BaseModule）
s_logger.Debug("调试信息");
s_logger.Info("一般信息");
s_logger.Warn("警告信息");
s_logger.Error("错误信息", exception);
```

### 订阅生命周期

```csharp
// 显式 Dispose
private IDisposable _subscription;

public void Start()
{
    _subscription = bus.Subscribe<MyMessage>(OnMessage);
}

public void Stop()
{
    _subscription?.Dispose();
}

// 或使用 using
using var sub = bus.Subscribe<MyMessage>(OnMessage);
```

### 弱引用订阅（防内存泄漏）

```csharp
// 推荐：用于 UI/ViewModel 等长期存在的对象
bus.SubscribeWeak(this, (me, MyMessage msg) => me.HandleMessage(msg));
```

---

## 故障排查

### 问题：Manager 初始化失败

**症状**: `ManagerLifetimeHost.Start()` 返回 false

**排查**:
1. 检查 `Init()` 是否返回 true
2. 检查日志中的异常信息
3. 确认依赖的 Manager 优先级设置正确

### 问题：消息未收到

**症状**: 发布消息后订阅者未收到

**排查**:
1. 确认订阅在发布之前完成
2. 确认消息类型匹配
3. 检查 `IDisposable` 是否被提前释放
4. 异步发布时检查队列状态

```csharp
var bus = (MessageBus)MessageHub.Current;
Console.WriteLine($"队列长度: {bus.QueueLength}");
Console.WriteLine($"订阅者数: {bus.GetSubscriberCount<MyMessage>()}");
```

### 问题：状态机卡住

**症状**: 逻辑停在某个步骤不动

**排查**:
1. 检查条件是否能满足
2. 检查是否设置了超时处理
3. 使用 `GetLogicState()` 查看当前状态

```csharp
var state = logic.GetLogicState();
Console.WriteLine(state);  // 【MainLogic:等待条件】
```

### 问题：报警重复添加

**症状**: 同一报警多次出现

**排查**:
1. 确认使用了 `key` 参数
2. 检查 key 是否唯一

```csharp
// 正确：使用唯一 key
alarms.AddAlarm("错误", AlarmLevel.H, key: "UniqueKey");

// 错误：不使用 key，每次都会新增
alarms.AddAlarm("错误", AlarmLevel.H);
```

---

## 相关文档

| 文档 | 路径 |
|------|------|
| 系统架构 | `docs/系统架构设计.md` |
| 消息总线设计 | `docs/MessageBus设计方案.md` |
| 报警系统设计 | `docs/AlarmSystem设计.md` |
| 状态机设计 | `docs/StateMachine设计.md` |
| API 参考 | `docs/Messaging-API参考.md` |
| 示例代码 | `examples/README.md` |
