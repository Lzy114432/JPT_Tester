# MessageHub LINQ 扩展设计方案

本文档描述 MessageHub 消息总线的 LINQ 风格扩展设计，供后续实现参考。

## 目录

- [设计目标](#设计目标)
- [扩展概览](#扩展概览)
- [Phase 1: 基础 LINQ 扩展](#phase-1-基础-linq-扩展)
- [Phase 2: 节流与防抖](#phase-2-节流与防抖)
- [Phase 3: 批量缓冲](#phase-3-批量缓冲)
- [Phase 4: 消息组合](#phase-4-消息组合)
- [Phase 5: 中间件管道](#phase-5-中间件管道)
- [Phase 6: Rx 适配器](#phase-6-rx-适配器)
- [文件结构](#文件结构)
- [实现优先级](#实现优先级)

---

## 设计目标

| 目标 | 说明 |
|------|------|
| 声明式 API | LINQ 风格链式调用，代码可读性高 |
| 零依赖 | Phase 1-5 不依赖外部库 |
| 向后兼容 | 扩展方法，不修改核心 MessageBus |
| 线程安全 | 继承 MessageBus 的线程安全保证 |
| 可组合 | 操作符可自由组合 |

---

## 扩展概览

```
当前 API                          扩展后 API
─────────────────────────────────────────────────────────────
bus.Subscribe<T>(handler)         bus.Stream<T>()
                                      .Where(x => x.Level > 5)
                                      .Select(x => Transform(x))
                                      .Throttle(100)
                                      .Tap(x => Log(x))
                                      .Subscribe(handler)
```

---

## Phase 1: 基础 LINQ 扩展

### 1.1 MessageStream 类

```csharp
namespace EwanCore.Messaging.Extensions
{
    /// <summary>
    /// 消息流构建器 - 支持 LINQ 风格链式调用
    /// </summary>
    public class MessageStream<T> where T : IMessage
    {
        private readonly IMessageBus _bus;
        private readonly List<Func<T, bool>> _filters = new List<Func<T, bool>>();
        private readonly List<Action<T>> _taps = new List<Action<T>>();
        private Func<T, T> _transform;

        internal MessageStream(IMessageBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <summary>
        /// 过滤消息
        /// </summary>
        public MessageStream<T> Where(Func<T, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            _filters.Add(predicate);
            return this;
        }

        /// <summary>
        /// 转换消息（同类型）
        /// </summary>
        public MessageStream<T> Select(Func<T, T> selector)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            _transform = selector;
            return this;
        }

        /// <summary>
        /// 副作用（日志/调试），不影响消息流
        /// </summary>
        public MessageStream<T> Tap(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _taps.Add(action);
            return this;
        }

        /// <summary>
        /// 终结操作：订阅消息流
        /// </summary>
        public IDisposable Subscribe(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            return _bus.Subscribe<T>(msg =>
            {
                // 1. 应用过滤器
                foreach (var filter in _filters)
                {
                    if (!filter(msg)) return;
                }

                // 2. 执行副作用
                foreach (var tap in _taps)
                {
                    tap(msg);
                }

                // 3. 应用转换
                var result = _transform != null ? _transform(msg) : msg;

                // 4. 调用处理器
                handler(result);
            });
        }
    }
}
```

### 1.2 扩展方法入口

```csharp
namespace EwanCore.Messaging.Extensions
{
    public static class MessageBusLinqExtensions
    {
        /// <summary>
        /// 创建消息流，支持 LINQ 风格操作
        /// </summary>
        public static MessageStream<T> Stream<T>(this IMessageBus bus) where T : IMessage
        {
            return new MessageStream<T>(bus);
        }
    }
}
```

### 1.3 使用示例

```csharp
// 过滤高级别报警
bus.Stream<AlarmMessage>()
    .Where(x => x.Level >= AlarmLevel.H)
    .Where(x => x.Unit == "PLC")
    .Subscribe(ShowAlarmDialog);

// 带调试日志
bus.Stream<SensorDataMessage>()
    .Tap(x => Debug.WriteLine($"收到传感器数据: {x.Value}"))
    .Where(x => x.Value > threshold)
    .Subscribe(HandleOverheat);
```

---

## Phase 2: 节流与防抖

### 2.1 节流 (Throttle)

固定时间间隔内只处理第一条消息。

```csharp
public class MessageStream<T> where T : IMessage
{
    private int _throttleMs;

    /// <summary>
    /// 节流：固定间隔内只处理第一条消息
    /// </summary>
    /// <param name="milliseconds">间隔毫秒数</param>
    public MessageStream<T> Throttle(int milliseconds)
    {
        if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        _throttleMs = milliseconds;
        return this;
    }

    // Subscribe 中添加节流逻辑
    private IDisposable SubscribeWithThrottle(Action<T> handler)
    {
        var lastTime = DateTime.MinValue;
        var throttleLock = new object();

        return _bus.Subscribe<T>(msg =>
        {
            // ... 过滤器逻辑 ...

            lock (throttleLock)
            {
                var now = DateTime.Now;
                if ((now - lastTime).TotalMilliseconds < _throttleMs)
                {
                    return; // 节流期内，跳过
                }
                lastTime = now;
            }

            handler(msg);
        });
    }
}
```

### 2.2 防抖 (Debounce)

等待静默期后处理最后一条消息。

```csharp
public class MessageStream<T> where T : IMessage
{
    private int _debounceMs;

    /// <summary>
    /// 防抖：等待静默期后处理最后一条消息
    /// </summary>
    /// <param name="milliseconds">静默期毫秒数</param>
    public MessageStream<T> Debounce(int milliseconds)
    {
        if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        _debounceMs = milliseconds;
        return this;
    }

    // Subscribe 中添加防抖逻辑
    private IDisposable SubscribeWithDebounce(Action<T> handler)
    {
        Timer debounceTimer = null;
        T lastMessage = default;
        var debounceLock = new object();

        var subscription = _bus.Subscribe<T>(msg =>
        {
            // ... 过滤器逻辑 ...

            lock (debounceLock)
            {
                lastMessage = msg;
                debounceTimer?.Dispose();
                debounceTimer = new Timer(_ =>
                {
                    T toHandle;
                    lock (debounceLock)
                    {
                        toHandle = lastMessage;
                        lastMessage = default;
                    }
                    if (toHandle != null)
                    {
                        handler(toHandle);
                    }
                }, null, _debounceMs, Timeout.Infinite);
            }
        });

        return new CompositeDisposable(subscription, debounceTimer);
    }
}
```

### 2.3 使用示例

```csharp
// 节流：100ms 内只更新一次 UI
bus.Stream<SensorDataMessage>()
    .Throttle(100)
    .Subscribe(UpdateDisplay);

// 防抖：用户停止输入 300ms 后搜索
bus.Stream<SearchInputMessage>()
    .Debounce(300)
    .Subscribe(PerformSearch);
```

---

## Phase 3: 批量缓冲

### 3.1 按数量缓冲

```csharp
public class MessageStream<T> where T : IMessage
{
    private int _bufferCount;

    /// <summary>
    /// 缓冲：累积 N 条后批量处理
    /// </summary>
    public MessageStream<T> Buffer(int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        _bufferCount = count;
        return this;
    }

    /// <summary>
    /// 批量订阅
    /// </summary>
    public IDisposable SubscribeBatch(Action<IReadOnlyList<T>> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var buffer = new List<T>();
        var bufferLock = new object();

        return _bus.Subscribe<T>(msg =>
        {
            // ... 过滤器逻辑 ...

            List<T> snapshot = null;
            lock (bufferLock)
            {
                buffer.Add(msg);
                if (buffer.Count >= _bufferCount)
                {
                    snapshot = new List<T>(buffer);
                    buffer.Clear();
                }
            }

            if (snapshot != null)
            {
                handler(snapshot);
            }
        });
    }
}
```

### 3.2 按时间窗口缓冲

```csharp
public class MessageStream<T> where T : IMessage
{
    private int _bufferTimeMs;

    /// <summary>
    /// 缓冲：按时间窗口批量处理
    /// </summary>
    public MessageStream<T> BufferTime(int milliseconds)
    {
        if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        _bufferTimeMs = milliseconds;
        return this;
    }

    // SubscribeBatch 中添加时间窗口逻辑
    private IDisposable SubscribeBatchWithTime(Action<IReadOnlyList<T>> handler)
    {
        var buffer = new List<T>();
        var bufferLock = new object();

        var timer = new Timer(_ =>
        {
            List<T> snapshot;
            lock (bufferLock)
            {
                if (buffer.Count == 0) return;
                snapshot = new List<T>(buffer);
                buffer.Clear();
            }
            handler(snapshot);
        }, null, _bufferTimeMs, _bufferTimeMs);

        var subscription = _bus.Subscribe<T>(msg =>
        {
            // ... 过滤器逻辑 ...

            List<T> snapshot = null;
            lock (bufferLock)
            {
                buffer.Add(msg);

                // 如果同时设置了数量限制
                if (_bufferCount > 0 && buffer.Count >= _bufferCount)
                {
                    snapshot = new List<T>(buffer);
                    buffer.Clear();
                }
            }

            if (snapshot != null)
            {
                handler(snapshot);
            }
        });

        return new CompositeDisposable(subscription, timer);
    }
}
```

### 3.3 使用示例

```csharp
// 每 100 条日志批量写入数据库
bus.Stream<ProductionLogMessage>()
    .Buffer(100)
    .SubscribeBatch(logs => Database.BulkInsert(logs));

// 每秒或满 50 条处理一批
bus.Stream<TelemetryMessage>()
    .Buffer(50)
    .BufferTime(1000)
    .SubscribeBatch(batch => SendToCloud(batch));
```

---

## Phase 4: 消息组合

### 4.1 合并多个消息流 (Merge)

```csharp
public static class MessageBusCombineExtensions
{
    /// <summary>
    /// 合并多个消息类型 - 任一触发
    /// </summary>
    public static IDisposable Merge<T1, T2>(
        this IMessageBus bus,
        Action<IMessage> handler)
        where T1 : IMessage
        where T2 : IMessage
    {
        var sub1 = bus.Subscribe<T1>(msg => handler(msg));
        var sub2 = bus.Subscribe<T2>(msg => handler(msg));
        return new CompositeDisposable(sub1, sub2);
    }

    /// <summary>
    /// 合并三个消息类型
    /// </summary>
    public static IDisposable Merge<T1, T2, T3>(
        this IMessageBus bus,
        Action<IMessage> handler)
        where T1 : IMessage
        where T2 : IMessage
        where T3 : IMessage
    {
        var sub1 = bus.Subscribe<T1>(msg => handler(msg));
        var sub2 = bus.Subscribe<T2>(msg => handler(msg));
        var sub3 = bus.Subscribe<T3>(msg => handler(msg));
        return new CompositeDisposable(sub1, sub2, sub3);
    }
}
```

### 4.2 配对消息 (Zip)

```csharp
public static class MessageBusCombineExtensions
{
    /// <summary>
    /// 等待两个关联消息都到达后触发
    /// </summary>
    /// <param name="matcher">匹配函数，判断两条消息是否关联</param>
    /// <param name="handler">匹配成功后的处理函数</param>
    /// <param name="timeoutMs">等待超时（毫秒）</param>
    public static IDisposable Zip<T1, T2>(
        this IMessageBus bus,
        Func<T1, T2, bool> matcher,
        Action<T1, T2> handler,
        int timeoutMs = 5000)
        where T1 : IMessage
        where T2 : IMessage
    {
        var pending1 = new ConcurrentDictionary<Guid, (T1 msg, DateTime time)>();
        var pending2 = new ConcurrentDictionary<Guid, (T2 msg, DateTime time)>();
        var matchLock = new object();

        void CleanExpired()
        {
            var now = DateTime.Now;
            var expired1 = pending1.Where(kv => (now - kv.Value.time).TotalMilliseconds > timeoutMs)
                                   .Select(kv => kv.Key).ToList();
            var expired2 = pending2.Where(kv => (now - kv.Value.time).TotalMilliseconds > timeoutMs)
                                   .Select(kv => kv.Key).ToList();

            foreach (var key in expired1) pending1.TryRemove(key, out _);
            foreach (var key in expired2) pending2.TryRemove(key, out _);
        }

        void TryMatch()
        {
            lock (matchLock)
            {
                CleanExpired();

                foreach (var kv1 in pending1)
                {
                    foreach (var kv2 in pending2)
                    {
                        if (matcher(kv1.Value.msg, kv2.Value.msg))
                        {
                            pending1.TryRemove(kv1.Key, out _);
                            pending2.TryRemove(kv2.Key, out _);
                            handler(kv1.Value.msg, kv2.Value.msg);
                            return;
                        }
                    }
                }
            }
        }

        var sub1 = bus.Subscribe<T1>(msg =>
        {
            pending1.TryAdd(Guid.NewGuid(), (msg, DateTime.Now));
            TryMatch();
        });

        var sub2 = bus.Subscribe<T2>(msg =>
        {
            pending2.TryAdd(Guid.NewGuid(), (msg, DateTime.Now));
            TryMatch();
        });

        return new CompositeDisposable(sub1, sub2);
    }
}
```

### 4.3 使用示例

```csharp
// 合并多种报警类型
bus.Merge<PlcAlarmMessage, SensorAlarmMessage, SystemAlarmMessage>(
    msg => ShowAlarm(msg));

// 等待扫码和 PLC 确认配对
bus.Zip<ScanResultMessage, PlcConfirmMessage>(
    matcher: (scan, plc) => scan.ProductId == plc.ProductId,
    handler: (scan, plc) => ProcessProduct(scan, plc),
    timeoutMs: 3000);
```

---

## Phase 5: 中间件管道

### 5.1 中间件接口

```csharp
namespace EwanCore.Messaging.Middleware
{
    /// <summary>
    /// 消息中间件接口
    /// </summary>
    public interface IMessageMiddleware<T> where T : IMessage
    {
        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="next">下一个中间件</param>
        void Invoke(T message, Action<T> next);
    }
}
```

### 5.2 内置中间件

```csharp
namespace EwanCore.Messaging.Middleware
{
    /// <summary>
    /// 日志中间件
    /// </summary>
    public class LoggingMiddleware<T> : IMessageMiddleware<T> where T : IMessage
    {
        private readonly Action<string> _logger;

        public LoggingMiddleware(Action<string> logger = null)
        {
            _logger = logger ?? (s => Debug.WriteLine(s));
        }

        public void Invoke(T message, Action<T> next)
        {
            var start = DateTime.Now;
            _logger($"[{start:HH:mm:ss.fff}] 开始处理: {typeof(T).Name}");

            next(message);

            var elapsed = (DateTime.Now - start).TotalMilliseconds;
            _logger($"[{DateTime.Now:HH:mm:ss.fff}] 处理完成: {typeof(T).Name}, 耗时: {elapsed:F1}ms");
        }
    }

    /// <summary>
    /// 验证中间件
    /// </summary>
    public class ValidationMiddleware<T> : IMessageMiddleware<T> where T : IMessage
    {
        private readonly Func<T, bool> _validator;
        private readonly Action<T> _onInvalid;

        public ValidationMiddleware(Func<T, bool> validator, Action<T> onInvalid = null)
        {
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _onInvalid = onInvalid;
        }

        public void Invoke(T message, Action<T> next)
        {
            if (_validator(message))
            {
                next(message);
            }
            else
            {
                _onInvalid?.Invoke(message);
            }
        }
    }

    /// <summary>
    /// 异常处理中间件
    /// </summary>
    public class ExceptionHandlingMiddleware<T> : IMessageMiddleware<T> where T : IMessage
    {
        private readonly Action<Exception, T> _onException;

        public ExceptionHandlingMiddleware(Action<Exception, T> onException)
        {
            _onException = onException ?? throw new ArgumentNullException(nameof(onException));
        }

        public void Invoke(T message, Action<T> next)
        {
            try
            {
                next(message);
            }
            catch (Exception ex)
            {
                _onException(ex, message);
            }
        }
    }
}
```

### 5.3 中间件扩展方法

```csharp
namespace EwanCore.Messaging.Extensions
{
    public static class MessageBusMiddlewareExtensions
    {
        /// <summary>
        /// 使用中间件管道订阅消息
        /// </summary>
        public static IDisposable SubscribeWithMiddleware<T>(
            this IMessageBus bus,
            Action<T> handler,
            params IMessageMiddleware<T>[] middlewares)
            where T : IMessage
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (middlewares == null || middlewares.Length == 0)
            {
                return bus.Subscribe(handler);
            }

            return bus.Subscribe<T>(msg =>
            {
                // 从后向前构建管道
                Action<T> pipeline = handler;
                for (int i = middlewares.Length - 1; i >= 0; i--)
                {
                    var middleware = middlewares[i];
                    var next = pipeline;
                    pipeline = m => middleware.Invoke(m, next);
                }

                pipeline(msg);
            });
        }
    }
}
```

### 5.4 使用示例

```csharp
// 组合多个中间件
bus.SubscribeWithMiddleware<ProductionCommand>(
    handler: cmd => ExecuteCommand(cmd),
    new LoggingMiddleware<ProductionCommand>(),
    new ValidationMiddleware<ProductionCommand>(cmd => cmd.IsValid),
    new ExceptionHandlingMiddleware<ProductionCommand>((ex, cmd) =>
        Logger.Error($"命令执行失败: {cmd.Id}", ex)));
```

---

## Phase 6: Rx 适配器

> 依赖: `System.Reactive` NuGet 包

### 6.1 IObservable 适配

```csharp
using System.Reactive.Linq;

namespace EwanCore.Messaging.Extensions
{
    public static class MessageBusRxExtensions
    {
        /// <summary>
        /// 将消息类型转换为 IObservable 流
        /// </summary>
        public static IObservable<T> AsObservable<T>(this IMessageBus bus) where T : IMessage
        {
            return Observable.Create<T>(observer =>
            {
                return bus.Subscribe<T>(msg => observer.OnNext(msg));
            });
        }

        /// <summary>
        /// 将 Observable 流发布到消息总线
        /// </summary>
        public static IDisposable PostTo<T>(this IObservable<T> source, IMessageBus bus)
            where T : IMessage
        {
            return source.Subscribe(msg => bus.Post(msg));
        }

        /// <summary>
        /// 将 Observable 流同步发布到消息总线
        /// </summary>
        public static IDisposable PublishTo<T>(this IObservable<T> source, IMessageBus bus)
            where T : IMessage
        {
            return source.Subscribe(msg => bus.Publish(msg));
        }
    }
}
```

### 6.2 使用示例

```csharp
// 使用完整的 Rx 操作符
bus.AsObservable<SensorDataMessage>()
    .Where(x => x.Temperature > 80)
    .Throttle(TimeSpan.FromMilliseconds(100))
    .Buffer(TimeSpan.FromSeconds(1))
    .Where(batch => batch.Count > 0)
    .Subscribe(batch => ProcessBatch(batch));

// 外部 Observable 发布到总线
sensorStream
    .Select(raw => new SensorDataMessage { Value = raw })
    .PostTo(bus);
```

---

## 文件结构

```
EwanCore.Messaging/
├── Extensions/
│   ├── MessageBusLinqExtensions.cs       # Phase 1: Stream/Where/Select/Tap
│   ├── MessageBusTimingExtensions.cs     # Phase 2: Throttle/Debounce
│   ├── MessageBusBufferExtensions.cs     # Phase 3: Buffer/BufferTime
│   ├── MessageBusCombineExtensions.cs    # Phase 4: Merge/Zip
│   ├── MessageBusMiddlewareExtensions.cs # Phase 5: 中间件扩展
│   └── MessageBusRxExtensions.cs         # Phase 6: Rx 适配（可选）
├── Operators/
│   ├── MessageStream.cs                  # 消息流构建器
│   └── CompositeDisposable.cs            # 组合释放器
├── Middleware/
│   ├── IMessageMiddleware.cs
│   ├── LoggingMiddleware.cs
│   ├── ValidationMiddleware.cs
│   └── ExceptionHandlingMiddleware.cs
└── ...
```

---

## 实现优先级

| 阶段 | 功能 | 优先级 | 依赖 | 工作量 |
|------|------|--------|------|--------|
| Phase 1 | Where/Select/Tap | P0 高 | 无 | 1 天 |
| Phase 2 | Throttle/Debounce | P1 中 | Phase 1 | 0.5 天 |
| Phase 3 | Buffer/BufferTime | P1 中 | Phase 1 | 0.5 天 |
| Phase 4 | Merge/Zip | P2 低 | 无 | 1 天 |
| Phase 5 | 中间件管道 | P2 低 | 无 | 0.5 天 |
| Phase 6 | Rx 适配器 | P3 可选 | System.Reactive | 0.5 天 |

### 建议实现顺序

1. **Phase 1** - 基础 LINQ，日常最常用
2. **Phase 2** - 节流防抖，传感器数据处理必备
3. **Phase 3** - 批量缓冲，日志/数据库写入优化
4. **Phase 5** - 中间件，横切关注点
5. **Phase 4** - 消息组合，复杂业务场景
6. **Phase 6** - Rx 适配，已有 Rx 代码迁移

---

## 测试用例清单

### Phase 1 测试

- [ ] Where 单条件过滤
- [ ] Where 多条件组合
- [ ] Select 转换
- [ ] Tap 副作用不影响消息流
- [ ] 链式调用组合
- [ ] 空消息处理
- [ ] 异常传播

### Phase 2 测试

- [ ] Throttle 基本功能
- [ ] Throttle 边界：间隔为 0
- [ ] Debounce 基本功能
- [ ] Debounce 连续触发
- [ ] Dispose 后 Timer 清理

### Phase 3 测试

- [ ] Buffer 数量触发
- [ ] BufferTime 时间触发
- [ ] Buffer + BufferTime 组合
- [ ] 空缓冲区处理
- [ ] Dispose 后剩余数据处理

### Phase 4 测试

- [ ] Merge 多类型合并
- [ ] Zip 匹配成功
- [ ] Zip 超时清理
- [ ] Zip 多次匹配

### Phase 5 测试

- [ ] 单中间件
- [ ] 多中间件顺序
- [ ] 中间件短路（不调用 next）
- [ ] 中间件异常处理

---

## 参考资料

- [ReactiveX 官方文档](http://reactivex.io/)
- [System.Reactive GitHub](https://github.com/dotnet/reactive)
- [MediatR Pipeline Behaviors](https://github.com/jbogard/MediatR/wiki/Behaviors)

---

*文档创建时间: 2024-12-29*
*作者: Claude Code Review*
