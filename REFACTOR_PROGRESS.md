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
| 第2步：核心层迁移 | 🔄 进行中 | Manager 接口迁移 |
| 第3步：业务层迁移 | ⏳ 待开始 | BusinessBonding 层 |
| 第4步：清理旧代码 | ⏳ 待开始 | 移除旧 BaseManager |
| 第5步：验证测试 | ⏳ 待开始 | 完整测试 |

---

## 迁移状态明细

### ✅ 已完成迁移

| Manager | 位置 | 提交 | 备注 |
|---------|------|------|------|
| MsgManager | Ewan.Core/Msg/ | 当前 | 保留旧 listener 兼容 |
| MesManager | Ewan.Core/Mes/ | 当前 | 支持 DI |
| MainController | Ewan.BusinessBonding/ | 当前 | 使用 ManagerLifetimeHost |

### ⏳ 待迁移 - Ewan.Core（6个）

| Manager | 文件 | 优先级 | 复杂度 | 备注 |
|---------|------|--------|--------|------|
| SecurityManager | Security/SecurityManager.cs | 高 | 中 | 被多处依赖 |
| LayeredIOManager | IO/LayeredIOManager.cs | 高 | 高 | IO基础设施 |
| AxisManager | Axis/AxisManager.cs | 中 | 中 | 轴控制 |
| ModbusRTUManager | Plc/ModbusRTUManager.cs | 中 | 低 | 通信层 |
| DLManager | ScanCode/DLManager.cs | 中 | 低 | 扫码设备 |
| MesMsgBus | Msg/MesMsgBus.cs | 低 | 低 | ⚠️ 阶段三删除，用 MessageHub 替代 |

### ⏳ 待迁移 - Ewan.BusinessBonding（3个）

| Manager | 文件 | 优先级 | 复杂度 | 备注 |
|---------|------|--------|--------|------|
| StreamController | StreamController.cs | 中 | 高 | 流程控制 |
| SystemControlService | SystemControlService.cs | 低 | 中 | 系统控制 |
| BinFeedController | BinFeedController.cs | 低 | 中 | 料仓控制 |

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
| MesMsgBus | Ewan.Core/Msg/ | MES Request/Reply | ⚠️ 待删除 |
| MessageHub | EwanCommon/EwanCore/Messaging/ | 完整消息总线 | ✅ 保留 |

### 功能对照

| 功能 | MsgManager | MesMsgBus | MessageHub |
|------|------------|-----------|------------|
| 发布消息 | `PushMsg()` | - | ✅ `Publish()` / `Post()` |
| 订阅消息 | `RegisterListener()` | - | ✅ `Subscribe()` |
| Request/Reply | ❌ | `RequestAsync()` | ✅ `RequestAsync()` / `Respond()` |
| 弱引用订阅 | ❌ | ❌ | ✅ `SubscribeWeak()` |
| UI线程调度 | ❌ | ❌ | ✅ `SubscribeOnContext()` |
| 诊断监控 | ❌ | ❌ | ✅ `IMessageBusDiagnostics` |
| 溢出策略 | 简单丢弃 | - | ✅ 3种策略可选 |
| 异步处理 | ❌ | ❌ | ✅ `SubscribeAsync()` |

### 整合决定

**MessageHub 完全覆盖 MsgManager 和 MesMsgBus 的功能，阶段三统一使用 MessageHub。**

### 阶段三清理清单

#### 待删除文件

```
Ewan.Core/Msg/
├── MsgManager.cs        # 用 MessageHub.Subscribe/Post 替代
├── MesMsgBus.cs         # 用 MessageHub.RequestAsync/Respond 替代
├── MsgListener.cs       # 用 IDisposable subscription 替代
├── MsgSubject.cs        # 用强类型消息类替代
├── MessageModel.cs      # 用 IMessage 替代
└── RequestAwaiter.cs    # MessageHub 内置

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

- [x] ~~MesMsgBus 是否与 MsgManager 合并？~~ → **已决定：阶段三统一用 MessageHub 替代，两者都删除**
- [ ] 是否需要为每个 Manager 添加 DI 构造函数？
- [ ] 旧的 `Ewan.Core\BaseManager.cs` 何时移除？

---

## 相关提交历史

```
d54ec60 重构：Manager架构迁移至IManager接口，集成MessageBus消息总线
40eb4d3 重构：AxisManager集成SMC606控制卡
6f705d1 新增：EwanCommon 通用库
3d06e27 新增：MES环线通信消息总线架构
8e94e90 重构：IO状态更新改为本地轮询模式
5cec74a 重构：移除IOController，直接使用LayeredIOManager API
```
