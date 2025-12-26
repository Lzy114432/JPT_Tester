# Messaging 模块优化建议清单

> 本文档按优先级排序，列出 `EwanCore.Messaging` 模块的改进建议。
> 当前评分：9/10，对于 WinForms/WPF 工控场景已够用。
>
> **设计原则**：
> - ⚠️ 切勿过度设计，等实际遇到问题再进行优化
> - ⚠️ 每次改动都要考虑后续兼容性，避免破坏现有 API
>
> **状态说明**：✅ 已完成 | ❌ 不实现（过度设计）| ⬜ 待定（遇到需求再议）

---

## 模块现状评估

| 维度 | 评分 | 说明 |
|------|------|------|
| 功能完整性 | 9/10 | 基础功能 + 弱引用 + 协变 + Request/Reply |
| 线程安全 | 9/10 | CAS + Volatile + ConcurrentDictionary |
| 易用性 | 9/10 | API 简洁，扩展方法实用 |
| 性能 | 8/10 | 满足工控场景需求 |
| 可扩展性 | 8/10 | SubscribeAll 可满足监控需求 |
| 诊断能力 | 9/10 | IMessageBusDiagnostics 已实现 |

---

## ✅ 已完成的功能

- [x] 强类型消息发布/订阅
- [x] 同步 Publish / 异步 Post 分离
- [x] 弱引用订阅 (SubscribeWeak) - 防内存泄漏
- [x] 基类/接口订阅 (协变支持)
- [x] 内置 Request/Reply (RequestAsync/Respond)
- [x] UI 线程切换 (SubscribeOnCurrentContext)
- [x] 队列溢出策略 (DropOldest/DropNewest/Block)
- [x] 异常隔离 (CatchHandlerExceptions)
- [x] ICorrelatedMessage 关联消息接口
- [x] IMessage 时间戳 (Timestamp)

---

## 🔴 高优先级（建议实施）

### 1. ✅ 增加诊断/监控接口（已完成 2025-12-24）

**状态**：已实现 `IMessageBusDiagnostics` 接口

**已实现功能**：
- `QueueLength` - 当前异步队列长度
- `GetSubscriberCount<T>()` - 获取订阅者数量
- `TotalPublished` - 累计发布消息数
- `TotalDropped` - 累计丢弃消息数
- `TotalHandlerExceptions` - 累计处理器异常数
- `SubscribedTypes` - 获取所有已订阅的消息类型
- `ResetStatistics()` - 重置统计计数

**使用示例**：
```csharp
// 监控界面显示
lblQueueLength.Text = $"队列: {bus.QueueLength}";
lblPublished.Text = $"已发布: {bus.TotalPublished}";

// 健康检查
if (bus.QueueLength > 500)
    Log.Warn("消息队列积压严重");
```

---

### 2. ❌ 增加异步订阅处理器支持（不实现 - 过度设计）

**决定**：不实现

**原因**：
- 工控场景消息处理通常是轻量操作，同步 `Action<T>` 足够
- 需要异步时可在 handler 内部自行 `Task.Run`
- 增加 API 复杂度，不符合简单原则

---

### 3. ✅ RespondAsync 异常日志增强（已完成 2025-12-24）

**状态**：已实现

**实现方式**：
- `RespondAsyncCore` 内部已捕获异常并调用 `OnHandlerException`
- 异常会触发 `HandlerException` 事件
- 同时尝试通过 `TryFailPendingReply` 让等待方收到异常

---

### 4. ⬜ PruneDead 清理节流优化（待定）

**状态**：暂不实现，当前实现已按需清理

**说明**：
- 当前只在发现死引用时才触发清理
- 实际使用中未发现性能问题
- 如遇到高频弱引用订阅场景再优化

---

## 🟡 中优先级（不实现 - 过度设计）

### 5. ❌ 增加消息拦截/中间件机制

**决定**：不实现

**原因**：
- 工控场景不需要复杂的管道机制
- 日志/监控用 `SubscribeAll` 已足够
- 增加框架复杂度，维护成本高

---

### 6. ❌ 增加消息过滤器注册

**决定**：不实现

**原因**：
- `MessageBusExtensions.Subscribe(predicate, handler)` 扩展方法已满足需求
- 全局过滤器容易导致难以调试的问题

---

### 7. ❌ 支持消息优先级

**决定**：不实现

**原因**：
- 工控场景消息量可控，FIFO 队列足够
- 优先级队列增加实现复杂度
- 如需紧急消息，可用 `Publish`（同步）替代 `Post`（异步）

---

### 8. ❌ 增加消息历史/重放功能

**决定**：不实现

**原因**：
- 增加内存占用，需要管理消息生命周期
- UI 层可自行缓存最后状态
- 违反消息总线"fire-and-forget"的设计理念

---

## 🟢 低优先级（不实现 - 锦上添花）

### 9-14. ❌ 以下功能均不实现

| 编号 | 功能 | 不实现原因 |
|------|------|-----------|
| #9 | 订阅者优先级 | 增加复杂度，实际无需求 |
| #10 | 消息批处理 | 工控场景逐条处理更可靠 |
| #11 | 消息去重 | 业务层自行处理更灵活 |
| #12 | 消息节流 | 业务层自行处理更灵活 |
| #13 | Source Generator | .NET Framework 4.8 不支持 |
| #14 | 单元测试辅助类 | 可用 Mock 框架替代 |

---

## 总结

### 完成状态统计

| 状态 | 数量 | 说明 |
|------|------|------|
| ✅ 已完成 | 2 | #1 诊断接口、#3 异常日志 |
| ⬜ 待定 | 1 | #4 PruneDead 优化（遇到性能问题再议）|
| ❌ 不实现 | 11 | #2, #5-14（过度设计）|

### 当前模块评分：9/10

**已满足工控场景全部需求**：
- 强类型消息发布/订阅
- 同步 Publish / 异步 Post
- 弱引用订阅（防内存泄漏）
- Request/Reply 模式
- UI 线程切换
- 队列溢出策略
- 诊断/监控接口
- 异常隔离与上报

### 设计原则回顾

1. **YAGNI** - You Aren't Gonna Need It，不需要的功能不实现
2. **KISS** - Keep It Simple, Stupid，保持简单
3. **兼容性优先** - 每次改动都要考虑对现有代码的影响
