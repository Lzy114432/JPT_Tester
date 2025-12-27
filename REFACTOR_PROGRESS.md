# Manager 架构重构进度

> 最后更新: 2025-12-27

## 重构目标

将旧的 `BaseManager<T>` 模式迁移到 `EwanCommon` 库的 `IManager` 接口，实现：
- 解耦：移除对 `BaseManager<T>` 的继承依赖
- 依赖注入：支持通过构造函数注入 `IPublishBus` 等依赖
- 统一生命周期：使用 `ManagerLifetimeHost` 统一管理
- 消息总线：使用 `MessageHub` 替代 `MsgManager.PushMsg()`

---

## 进度概览

| 阶段 | 状态 | 说明 |
|------|------|------|
| 第0步：基础设施 | ✅ 完成 | EwanCommon 通用库 |
| 第1步：消息总线 | ✅ 完成 | MessageBus 架构 |
| 第2步：核心层迁移 | ✅ 完成 | 6个核心 Manager 已迁移至 IManager |
| 第3步：业务层迁移 | ✅ 完成 | StreamController 已迁移 |
| 第4步：清理旧代码 | ✅ 完成 | MesMsgBus 已删除，迁移至 MessageHub |
| 第5步：验证测试 | ⏳ 待开始 | 完整测试 |

### 最新进展 (2025-12-27)

- ✅ 6个核心 Manager 全部完成迁移至 `IManager` 接口
- ✅ 所有 Manager 日志统一迁移至 `log4net ILog`
- ✅ **MesMsgBus 已删除**，MES Request/Reply 迁移至 MessageHub
  - MesModule 使用 `MessageHub.Current.RespondAsync<MesRingLineRequest, MesRingLineFeedback>()`
  - MesManualSendViewModel 使用 `MessageHub.Current.RequestAsync<MesRingLineRequest, MesRingLineFeedback>()`
  - 删除 `MesMsgBus.cs`、`RequestAwaiter.cs`
  - 删除 `MsgSubject.MesRequest`、`MsgSubject.MesFeedback`
- ✅ **StatusIndicatorCommand 已迁移至 MessageHub**
  - LogWindowViewModel 使用 `MessageHub.Current.Subscribe<UILogMessage>()`
  - MainWindowViewModel 使用 `MessageHub.Current.Subscribe<StatusIndicatorCommand>()`
  - SystemStatusIndicatorModule 使用 `MessageHub.Current.Subscribe<StatusIndicatorCommand>()`
  - StreamController, SafetyModule, MaterialLoadingModule, ProductionLineModule 使用 `MessageHub.Current.Post<StatusIndicatorCommand>()`
  - Ewan.Model 升级到 .NET Framework 4.8 以支持 EwanCommon 引用
  - StatusIndicatorCommand 实现 `IMessage` 接口
- ✅ 编译验证通过，无错误
- ✅ 已完成迁移的 Manager：
  - SecurityManager (Priority 0)
  - ModbusRTUManager (Priority 1)
  - LayeredIOManager (Priority 1)
  - AxisManager (Priority 1)
  - DLManager (Priority 1)
  - StreamController (Priority 3)

### 待迁移消息类型

| 消息类型 | MsgSubject | 发布者 | 订阅者 |
|----------|------------|--------|--------|
| SystemControlCommand | SystemControl | SafetyModule, SystemControlService | SafetyModule, ProductionLineModule |
| SafetyAlert | SafetyAlert | SafetyModule | AutoProductionModule |
| RingLineModel | RingLineData | RingLineModule | - |

---

## 迁移状态明细

### ✅ 已完成迁移

| Manager | 位置 | IManager | ILog日志 | 备注 |
|---------|------|:--------:|:--------:|------|
| MsgManager | Ewan.Core/Msg/ | ✅ | ✅ | 保留旧 listener 兼容 |
| MesManager | Ewan.Core/Mes/ | ✅ | ✅ | 支持 DI |
| MainController | Ewan.BusinessBonding/ | ✅ | ✅ | 使用 ManagerLifetimeHost |
| SecurityManager | Ewan.Core/Security/ | ✅ | ✅ | Priority 0 |
| ModbusRTUManager | Ewan.Core/Plc/ | ✅ | ✅ | Priority 1, [Manager] 已注释 |
| LayeredIOManager | Ewan.Core/IO/ | ✅ | ✅ | Priority 1 |
| AxisManager | Ewan.Core/Axis/ | ✅ | ✅ | Priority 1 |
| DLManager | Ewan.Core/ScanCode/ | ✅ | ✅ | Priority 1 |
| StreamController | Ewan.BusinessBonding/ | ✅ | ✅ | Priority 3 |

### ✅ 已删除

| 类 | 位置 | 替代方案 |
|---------|------|------|
| MesMsgBus | Ewan.Core/Msg/ | MessageHub.RequestAsync/RespondAsync |
| RequestAwaiter | Ewan.Core/Msg/ | EwanCommon 内置 |

### 🔄 待检查 - Ewan.BusinessBonding

| Manager | 文件 | IManager | ILog日志 | 备注 |
|---------|------|:--------:|:--------:|------|
| SystemControlService | SystemControlService.cs | - | - | 待检查 |
| BinFeedController | BinFeedController.cs | - | - | 待检查 |

### ⚠️ 待删除 - 后续阶段

| 类 | 位置 | 备注 |
|---------|------|------|
| BaseManager\<T\> | Ewan.Core/ | 渐进式移除 |

---

## 迁移模板

### 改动前（旧模式）

```csharp
using Ewan.Core;
using Ewan.Core.Attribute;

[Manager(Priority = 1)]
public class XxxManager : BaseManager<XxxManager>
{
    public override bool Init()
    {
        _uiLogger.Info("初始化");
        return base.Init();
    }

    public override void Destroy()
    {
        _uiLogger.Info("销毁");
        base.Destroy();
    }
}
```

### 改动后（新模式）

```csharp
using EwanCore;
using EwanCore.Attribute;
using EwanCore.Messaging;
using EwanCommon.Logging;
using log4net;

[Manager(Priority = 1)]
public class XxxManager : IManager
{
    private static readonly ILog s_logger = Log.GetLogger(typeof(XxxManager));
    private readonly IPublishBus _publishBus;
    private bool _disposed;

    #region 单例支持（兼容现有代码）
    private static readonly Lazy<XxxManager> s_instance = new Lazy<XxxManager>(() => new XxxManager());
    public static XxxManager Instance() => s_instance.Value;
    #endregion

    // 默认构造函数（使用全局 MessageHub）
    public XxxManager() : this(MessageHub.PublishBus) { }

    // DI 构造函数
    public XxxManager(IPublishBus publishBus)
    {
        _publishBus = publishBus ?? MessageHub.PublishBus;
    }

    public bool Init()
    {
        s_logger.Info("XxxManager 初始化开始");
        // ... 初始化逻辑
        s_logger.Info("XxxManager 初始化完成");
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        s_logger.Info("XxxManager 开始销毁");
        // ... 清理逻辑
        s_logger.Info("XxxManager 销毁完成");
    }

    [Obsolete("请使用 Dispose() 方法")]
    public void Destroy() => Dispose();
}
```

### 关键改动点

1. **继承 → 接口**
   ```csharp
   // 旧
   public class XxxManager : BaseManager<XxxManager>
   // 新
   public class XxxManager : IManager
   ```

2. **命名空间**
   ```csharp
   // 旧
   using Ewan.Core;
   using Ewan.Core.Attribute;
   // 新
   using EwanCore;
   using EwanCore.Attribute;
   using EwanCore.Messaging;
   using EwanCommon.Logging;
   ```

3. **日志**
   ```csharp
   // 旧
   _uiLogger.Info("...");
   _appLogger.Error("...");
   // 新
   private static readonly ILog s_logger = Log.GetLogger(typeof(XxxManager));
   s_logger.Info("...");
   ```

4. **单例模式**
   ```csharp
   // 新增显式单例
   private static readonly Lazy<XxxManager> s_instance = new Lazy<XxxManager>(() => new XxxManager());
   public static XxxManager Instance() => s_instance.Value;
   ```

5. **生命周期方法**
   ```csharp
   // 旧
   public override bool Init() { ... base.Init(); }
   public override void Destroy() { ... base.Destroy(); }
   // 新
   public bool Init() { ... return true; }
   public void Dispose() { ... }
   [Obsolete] public void Destroy() => Dispose();
   ```

---

## 注意事项

1. **保持兼容性**：保留 `Instance()` 方法和 `Destroy()` 方法（标记 Obsolete）
2. **依赖顺序**：按 Manager Priority 顺序迁移，避免依赖问题
3. **测试验证**：每迁移一个 Manager 后进行编译和基本功能测试
4. **UILogger**：如果需要 UI 日志输出，使用 `new UILogger(_publishBus)`

---

## 消息系统整合计划

### 现状分析

当前存在三套消息系统：

| 系统 | 位置 | 功能 | 状态 |
|------|------|------|------|
| MsgManager | Ewan.Core/Msg/ | 通用消息分发 | ⚠️ 待删除 |
| MesMsgBus | Ewan.Core/Msg/ | MES Request/Reply | ✅ 已删除 |
| MessageHub | EwanCommon/EwanCore/Messaging/ | 完整消息总线 | ✅ 保留 |

### 功能对照

| 功能 | MsgManager | MesMsgBus | MessageHub |
|------|------------|-----------|------------|
| 发布消息 | `PushMsg()` | - | ✅ `Publish()` / `Post()` |
| 订阅消息 | `RegisterListener()` | - | ✅ `Subscribe()` |
| Request/Reply | ❌ | ~~`RequestAsync()`~~ | ✅ `RequestAsync()` / `Respond()` |
| 弱引用订阅 | ❌ | ❌ | ✅ `SubscribeWeak()` |
| UI线程调度 | ❌ | ❌ | ✅ `SubscribeOnContext()` |
| 诊断监控 | ❌ | ❌ | ✅ `IMessageBusDiagnostics` |
| 溢出策略 | 简单丢弃 | - | ✅ 3种策略可选 |
| 异步处理 | ❌ | ❌ | ✅ `SubscribeAsync()` |

### 整合决定

**MessageHub 完全覆盖 MsgManager 和 MesMsgBus 的功能，阶段三统一使用 MessageHub。**

### 阶段四清理清单

#### ✅ 已删除文件

```
Ewan.Core/Msg/
├── MesMsgBus.cs         # ✅ 已删除 - 用 MessageHub.RequestAsync/Respond 替代
└── RequestAwaiter.cs    # ✅ 已删除 - MessageHub 内置
```

#### ⏳ 待删除文件

```
Ewan.Core/Msg/
├── MsgManager.cs        # 用 MessageHub.Subscribe/Post 替代
├── MsgListener.cs       # 用 IDisposable subscription 替代
├── MsgSubject.cs        # 用强类型消息类替代
└── MessageModel.cs      # 用 IMessage 替代

Ewan.Model/Messages/
└── UILogMsg.cs          # 用 UILogMessage 替代
```

#### UI 层替换

**LogWindowViewModel.cs 改动示例：**

```csharp
// 旧代码
private MsgListener _logListener;

private void InitializeLogListener()
{
    _logListener = new MsgListener(MsgSubject.UILog, OnLogMessageReceived);
    MsgManager.Instance().RegisterListener(_logListener);
}

private void OnLogMessageReceived(MessageModel msg)
{
    var logMsg = msg.GetData<UILogMsg>();
    // ...
}

public void Dispose()
{
    MsgManager.Instance().UnRegisterListener(_logListener);
}
```

```csharp
// 新代码
using EwanCore.Messaging;
using EwanCore.Messaging.Messages;

private IDisposable _logSubscription;

private void InitializeLogListener()
{
    _logSubscription = MessageHub.SubscribeBus.Subscribe<UILogMessage>(OnUILogMessage);
}

private void OnUILogMessage(UILogMessage msg)
{
    var logEntry = new LogEntry
    {
        Timestamp = msg.Timestamp.DateTime,
        Level = ConvertLevel(msg.Level),
        Message = msg.Message
    };
    // ...
}

public void Dispose()
{
    _logSubscription?.Dispose();
}
```

#### MES Request/Reply 替换

**旧代码（使用 MesMsgBus）：**

```csharp
var feedback = await MesMsgBus.Instance().RequestAsync(
    new MesRingLineRequest { ... },
    timeoutMs: 30000);
```

**新代码（使用 MessageHub）：**

```csharp
var feedback = await MessageHub.RequestReplyBus.RequestAsync<MesRequest, MesResult>(
    new MesRequest { ... },
    timeoutMs: 30000);
```

### 参考文档

- `EwanCommon/docs/Messaging-API参考.md` - 完整 API 文档
- `EwanCommon/docs/MessageBus设计方案.md` - 设计说明
- `EwanCommon/examples/RequestReplyExample.cs` - Request/Reply 示例
- `EwanCommon/examples/WinFormsAutoDisposeSubscriptionExample.cs` - UI 订阅示例

---

## 待决策项

- [x] ~~MesMsgBus 是否与 MsgManager 合并？~~ → **已完成：MesMsgBus 已删除，MES Request/Reply 迁移至 MessageHub**
- [ ] 是否需要为每个 Manager 添加 DI 构造函数？
- [x] ~~旧的 `Ewan.Core\BaseManager.cs` 何时移除？~~ → **已决定：保留 BaseManager\<T\> 并实现 IManager，渐进式迁移**
- [x] ~~各 Manager 是否需要从 `_uiLogger` 迁移到 `ILog`？~~ → **已完成：6个核心 Manager 已全部迁移至 log4net ILog**

---

## 相关提交历史

```
a166ff6 重构：Manager 生命周期管理统一化
f3a34be 修复：升级 System.Runtime.CompilerServices.Unsafe 到 6.0.0
d54ec60 重构：Manager架构迁移至IManager接口，集成MessageBus消息总线
40eb4d3 重构：AxisManager集成SMC606控制卡
6f705d1 新增：EwanCommon 通用库
3d06e27 新增：MES环线通信消息总线架构
8e94e90 重构：IO状态更新改为本地轮询模式
5cec74a 重构：移除IOController，直接使用LayeredIOManager API
```
