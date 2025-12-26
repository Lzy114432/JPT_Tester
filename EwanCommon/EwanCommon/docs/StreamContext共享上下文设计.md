# StreamContext 共享上下文设计

## 概述

`StreamContext` 是一种**共享上下文模式**，用于在 `StreamRunner` 的多个模块间共享：
- **服务引用**（MessageBus、PLC 客户端、MES 客户端等）
- **实时状态**（PLC 最新快照、流程中间数据等）

通过 `IModule.SetObject(ctx)` 方法，将同一个上下文对象注入到所有模块中。

---

## 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                      StreamRunner                            │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │ PLC轮询模块  │  │  流程模块    │  │  MES上传模块 │       │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                 │                 │                │
│         └────────────────┼─────────────────┘                │
│                          ▼                                   │
│              ┌─────────────────────┐                        │
│              │    StreamContext    │  ← 共享上下文           │
│              │  - Bus (消息总线)   │                        │
│              │  - Plc (PLC客户端)  │                        │
│              │  - Mes (MES客户端)  │                        │
│              │  - Latest (最新快照)│                        │
│              └─────────────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

---

## StreamContext 定义示例

```csharp
public sealed class StreamContext
{
    // 服务引用
    public IMessageBus Bus;       // 消息总线
    public IPlcClient Plc;        // PLC 通讯客户端
    public IMesClient Mes;        // MES 客户端
    public IAlarmService Alarms;  // 报警服务

    // 实时状态
    public PlcSnapshot Latest;    // PLC 最新快照

    // 流程中间数据
    public string CurrentSn;      // 当前产品 SN
    public bool MesConfirmed;     // MES 确认标志
}
```

---

## 初始化方式

```csharp
// 1) 创建共享上下文
var ctx = new StreamContext
{
    Bus = new MessageBus(),
    Plc = new McPlcClient("192.168.1.10"),
    Mes = new HttpMesClient("http://mes.local"),
    Alarms = new AlarmService()
};

// 2) 创建模块列表
var modules = new List<IModule>
{
    new PlcPollingModule(),   // 读 PLC，更新 ctx.Latest
    new MainProcessModule(),  // 主流程，读 ctx.Latest 判断条件
    new MesGatewayModule(),   // MES 上传，用 ctx.Mes 发送
};

// 3) 统一注入上下文
foreach (var m in modules)
    m.SetObject(ctx);

// 4) 启动
var runner = new StreamRunner(modules);
runner.Start();
```

---

## 模块中使用上下文

```csharp
public class PlcPollingModule : BaseModule<PlcPollingModule>
{
    private StreamContext _ctx;
    private DateTime _last;

    protected override void OnInit()
    {
        _ctx = (StreamContext)Data;  // 从 Data 字段获取上下文
        _last = DateTime.MinValue;
    }

    protected override bool OnRun()
    {
        // 节拍控制：50ms 轮询一次
        if ((DateTime.Now - _last).TotalMilliseconds < 50)
            return true;
        _last = DateTime.Now;

        // 读 PLC 并更新共享状态
        var snapshot = _ctx.Plc.Read<PlcSnapshot>();
        _ctx.Latest = snapshot;

        // 通知其他模块（可选）
        _ctx.Bus.Post(new PlcUpdated(snapshot));
        return true;
    }

    protected override void OnDestroy() { }
}
```

---

## 使用场景

### 场景 1：多模块共享 PLC 最新状态

```csharp
// PLC 轮询模块：写入最新数据
protected override bool OnRun()
{
    var snapshot = _ctx.Plc.Read<PlcSnapshot>();
    _ctx.Latest = snapshot;  // 写入共享上下文
    _ctx.Bus.Post(new PlcUpdated(snapshot));
    return true;
}

// 流程模块：直接读取最新数据（不用等消息）
protected override bool OnRun()
{
    // 直接拿最新值，不用订阅等消息
    if (!_ctx.Latest.ExternalReady)
    {
        return true;  // 外部没准备好，等下一轮
    }
    // 继续流程...
}
```

**对比两种获取方式：**

| 方式 | 获取 PLC 状态 | 特点 |
|------|---------------|------|
| 消息订阅 | 等 `PlcUpdated` 消息 | 异步，有延迟 |
| 共享上下文 | `_ctx.Latest` | 同步，立即可用 |

### 场景 2：共享服务引用

**之前：每个模块构造函数都要传参数**
```csharp
new PlcModule(bus, plc);
new MesModule(bus, mes);
new ProcessModule(bus, plc, mes, alarms);  // 参数越来越多
```

**现在：统一用 ctx**
```csharp
foreach (var m in modules)
    m.SetObject(ctx);

// 模块里直接用
_ctx.Plc.Write(...);
_ctx.Mes.Upload(...);
_ctx.Bus.Post(...);
_ctx.Alarms.AddAlarm(...);
```

### 场景 3：流程间传递中间结果

```csharp
// 扫码模块
case "扫码完成":
    _ctx.CurrentSn = scannedSn;  // 写入共享上下文
    SwitchIndex = "下一步";
    break;

// 检测模块（不同模块，直接读）
case "开始检测":
    var sn = _ctx.CurrentSn;  // 直接拿到扫码结果
    // 执行检测...
    break;

// MES 模块
case "上传结果":
    _ctx.Mes.Upload(_ctx.CurrentSn, _ctx.LastResult);
    break;
```

---

## MessageBus vs StreamContext 分工

| 用途 | MessageBus | StreamContext |
|------|------------|---------------|
| **通知/事件** | ✅ Post/Subscribe | ❌ |
| **请求/响应** | ✅ RequestAsync | ❌ |
| **最新状态** | ❌ 需要缓存 | ✅ Latest |
| **服务引用** | ❌ | ✅ Plc/Mes/Bus |
| **中间数据** | ❌ | ✅ CurrentSn 等 |

**简单总结：**
- **MessageBus** = 通知（"发生了什么"）
- **StreamContext** = 状态（"现在是什么"）

---

## 线程安全注意事项

如果 `StreamRunner` 是单线程循环调用模块，则不需要额外的线程同步。

如果有多线程访问 `StreamContext`，建议：

```csharp
public sealed class StreamContext
{
    private readonly object _lock = new object();
    private PlcSnapshot _latest;

    public PlcSnapshot Latest
    {
        get { lock (_lock) return _latest; }
        set { lock (_lock) _latest = value; }
    }
}
```

或者使用 `volatile`（适用于简单引用类型）：

```csharp
public volatile PlcSnapshot Latest;
```

---

## 最佳实践

1. **服务引用在初始化时设置，运行时不变**
   - `Bus`、`Plc`、`Mes`、`Alarms` 等在 `ctx` 创建时赋值，之后不修改

2. **实时状态由专门的模块更新**
   - `Latest` 由 `PlcPollingModule` 独占写入，其他模块只读

3. **中间数据按流程顺序传递**
   - 前一步写入，后一步读取，避免竞争

4. **配合 MessageBus 使用**
   - 需要通知时用 `Bus.Post()`
   - 需要当前值时用 `ctx.Latest`

---

## 相关文件

- 模块接口：`EwanCore/Module/Interface/IModule.cs`
- 模块基类：`EwanCore/Module/BaseModule.cs`
- 运行器：`EwanCore/Runner/StreamRunner.cs`
- 示例：`examples/StreamRunnerExample.cs`
- 示例：`examples/MesUploadBestPracticeExample.cs`
