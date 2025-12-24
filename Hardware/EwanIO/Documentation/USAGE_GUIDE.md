# EwanIO 使用指南

本文档说明 EwanIO 库的正确使用方式、API 参考、线程安全规范和配置限制。

---

## 目录

1. [设计理念](#设计理念)
2. [基本使用流程](#基本使用流程)
3. [API 速查表](#api-速查表)
4. [线程安全规范](#线程安全规范)
5. [映射配置规范](#映射配置规范)
6. [Wait/Confirm 使用模式](#waitconfirm-使用模式)
7. [API 生命周期](#api-生命周期)
8. [常见错误](#常见错误)
9. [性能建议](#性能建议)

---

## 设计理念

### 核心目标

- **稳定**：帧一致性、线程安全、零分配热路径
- **灵活**：同一 Layout 可创建多个实例（多工位复用）
- **解耦**：换板卡/换 PLC 不改业务代码，只改配置

### 三大核心对象

| 对象 | 说明 |
|------|------|
| `IoContext<TLayout>` | 一个业务域的 IO 上下文（如 StationA/StationB） |
| `Snapshot (ctx.R)` | 只读快照，帧一致、原子切换 |
| `Command` | 写命令缓冲，线程安全，Tick 时按 dirty 下发 |

> 说明：`ctx.R` 由 Layout 双缓冲 + 原子交换实现。改造原因是早期单实例逐属性同步时，读线程可能遇到“半更新读/帧撕裂”。现在读取的是完整一帧模型值。
> 注意：`ctx.R` 返回的是当前帧对象引用，Tick 会交换前后缓冲；长期持有旧引用可能一直不变，或在后续 Tick 被复用并改写。需要长期保存请自行复制字段。
> 注意：`ctx.R` 仅用于读取，禁止对其属性赋值；写入不会下发硬件且会污染本帧（其他线程可能读到被修改的数据），请使用 `On/Off/Pulse`。

### Tick 模式

- 外部 10ms 调用一次 `Tick()`（不在库内开线程）
- Tick 期间**零分配**（热路径不 new 对象）
- **帧一致性**：Snapshot 原子切换，同一帧内所有值一致

---

## 基本使用流程

### 1. 定义 Layout

```csharp
public class MyIOLayout
{
    [IO(0)]
    public InputSignal 启动按钮 { get; set; }

    [IO(1)]
    public InputSignal 停止按钮 { get; set; }

    [IO(0)]
    public OutputSignal 运行指示灯 { get; set; }

    [IO(1)]
    public OutputSignal 报警指示灯 { get; set; }
}
```

### 2. 构建 IoContext

```csharp
// 方式一：分步构建
var ctx = IoContextBuilder.For<MyIOLayout>()
    .WithId("Station1")
    .WithHardware(mcPlc)
    .WithMapping("config/io_mapping.json")  // 可选
    .Build();

// 连接硬件
mcPlc.Connect("192.168.1.10:5000");

// 方式二：一步完成（推荐）
var ctx = IoContextBuilder.For<MyIOLayout>()
    .WithId("Station1")
    .WithHardware(mcPlc)
    .BuildAndConnect("192.168.1.10:5000");
```

### 3. 主循环调用 Tick

```csharp
// 在定时器或主循环中调用（推荐 10-20ms 周期）
void OnTimer()
{
    ctx.Tick();  // 同步硬件 + 更新快照 + 下发输出

    // 读取输入（从快照读取，保证帧一致性）
    if (ctx.R.启动按钮)
    {
        ctx.On(x => x.运行指示灯);
    }
}
```

### 4. 资源释放

```csharp
ctx.Dispose();
```

---

## API 速查表

> `ctx.R` 代表 Snapshot（只读、一帧一致），`On/Off/Pulse` 写入 Command（输出命令），默认由下一次 `Tick()` 下发到硬件。

| 调用 | 效果 |
|------|------|
| `ctx.Tick()` | 同步一次：硬件输入刷新 → 应用映射(NO/NC)与模拟 → 更新 Snapshot → 更新 Edge → 下发 dirty 输出 |
| `ctx.R.<属性>` | 读取 Snapshot 中的值；同一帧内所有属性一致 |
| `ctx.GetInput(index)` | 按索引读取输入（值来自 Snapshot） |
| `ctx.GetOutput(index)` | 按索引读取输出（值来自 Snapshot） |
| `ctx.On(expr/index)` | 设置输出为 ON，默认等下一次 Tick 下发 |
| `ctx.On(expr/index, now: true)` | 设置输出为 ON 并立即下发（不做输入同步） |
| `ctx.Off(expr/index)` | 设置输出为 OFF，默认等下一次 Tick 下发 |
| `ctx.Off(expr/index, now: true)` | 设置输出为 OFF 并立即下发 |
| `ctx.Pulse(expr/index, durationTicks)` | 输出脉冲（按 Tick 计数，默认 ON→OFF） |
| `ctx.Pulse(expr/index, durationTicks, now: true, value: false)` | 反向脉冲（OFF→ON），立即下发 |
| `ctx.Flush()` | 立刻下发当前 dirty 输出（不做输入同步） |
| `ctx.Edge.R(expr/index)` | 读取并清除上升沿 |
| `ctx.Edge.F(expr/index)` | 读取并清除下降沿 |
| `ctx.Edge.PeekR/PeekF` | 只读边沿，不清除 |
| `ctx.Sim.ForceOn(expr/index)` | 强制输入为 ON（下一次 Tick 生效） |
| `ctx.Sim.ForceOff(expr/index)` | 强制输入为 OFF |
| `ctx.Sim.ClearSimulate/ClearAll` | 取消模拟，恢复读硬件 |
| `ctx.Mapping.Load(file)` | 加载映射配置 |
| `ctx.Mapping.Save(file)` | 保存映射配置 |
| `ctx.Meta.GetInputName(index)` | 获取输入属性名（用于 UI/日志） |
| `ctx.Until(expr, expected, timeout)` | 等待输入到达期望值，返回 `IoOp<bool>` |
| `ctx.Confirm(output, value, confirm, expected, timeout, now)` | 写输出后等待输入反馈 |
| `ctx.Health` | 获取健康状态（连接、性能、错误） |
| `ctx.Dispose()` | 释放硬件资源 |

---

## 线程安全规范

### 并发模型假设

- `Tick()` 必须单线程调用
- Mapping/Meta 等共享对象无锁，只允许初始化时配置
- 写操作仅保证单点线程安全，跨多点一致性由业务层控制

### Tick() 方法

| 规则 | 说明 |
|------|------|
| **单线程调用** | `Tick()` 必须由**同一个线程**调用，不要从多个线程并发调用 |
| **推荐方式** | 使用单一定时器线程（如 `System.Timers.Timer`）|

```csharp
// 正确：单一定时器线程
private Timer _timer;

void Start()
{
    _timer = new Timer(10);  // 10ms
    _timer.Elapsed += (s, e) => ctx.Tick();
    _timer.Start();
}

// 错误：多线程调用 Tick
Task.Run(() => ctx.Tick());  // 线程 A
Task.Run(() => ctx.Tick());  // 线程 B - 不要这样做！
```

### On/Off/Pulse 方法

| 方法 | 线程安全 | 说明 |
|------|---------|------|
| `ctx.On/Off(...)` | 安全 | 内部使用 `_ioLock`；`now: true` 会等待 Tick 锁并立即下发 |
| `ctx.Pulse(...)` | 安全 | 按 Tick 计数；同一输出在脉冲未结束前再次 Pulse 会被忽略 |

```csharp
// 安全：可以从任意线程调用
ctx.On(0);                      // 缓存，等待 Tick 下发
ctx.Off(0, now: true);          // 立即下发
ctx.Pulse(0, durationTicks: 3); // 持续 3 个 Tick
```

> 注意：不要在 Tick 线程内调用 `now: true`，避免等待同一把锁导致阻塞。

### 写 API 收敛原因

- `Write` 使用共享布局实例，跨线程易混合写入，导致半更新/错值
- `Toggle` 属于读-改-写，无法保证原子性，遇到并发写会互相覆盖
- `SetOutput` 从公有 API 移除，统一入口为 `On/Off/Pulse`，便于约束并发模型
- 为了可推理的线程安全，仅保留单点写入的 `On/Off/Pulse`
- 需要多点一致变更时，请在业务层加锁并逐点调用

### 读取操作

| 操作 | 线程安全 | 说明 |
|------|---------|------|
| `ctx.R.xxx` | 安全 | 从快照读取，只读 |
| `ctx.GetInput(i)` | 安全 | 从快照读取 |
| `ctx.GetOutput(i)` | 安全 | 从快照读取 |

> 提示：不要缓存 `ctx.R` 引用；每次 Tick 后重新读取，必要时自行复制字段。

### 边缘检测

| 操作 | 线程安全 | 说明 |
|------|---------|------|
| `ctx.Edge.R(i)` | **安全** | 读取并清除，使用 `Interlocked.Exchange` 实现原子操作 |
| `ctx.Edge.F(i)` | **安全** | 读取并清除，使用 `Interlocked.Exchange` 实现原子操作 |
| `ctx.Edge.PeekR(i)` | **安全** | 只读，使用 `Volatile.Read` 确保可见性 |
| `ctx.Edge.PeekF(i)` | **安全** | 只读，使用 `Volatile.Read` 确保可见性 |

```csharp
// 推荐：Tick 线程负责刷新，流程线程负责边缘检测
void TickThread()
{
    ctx.Tick();  // 更新快照 + 更新边沿
}

void ProcessThread()
{
    if (ctx.Edge.R(0))  // 线程安全的原子读取并清除
    {
        // 处理上升沿
    }
}
```

---

## 映射配置规范

### 支持的映射类型

| 类型 | 支持 | 说明 |
|------|------|------|
| 1:1 映射 | 支持 | 逻辑 Y0 → 物理 Y0（默认）|
| 重排映射 | 支持 | 逻辑 Y0 → 物理 Y5 |
| 跨端口映射 | 支持 | 逻辑 Y0 → 物理 Y33（跨越 32 位边界）|
| N:1 映射 | **不支持** | 多个逻辑点映射到同一物理点 |
| 1:N 映射 | **不支持** | 一个逻辑点映射到多个物理点 |

### 映射配置示例

```json
{
  "inputs": [
    { "logical": 0, "physical": 0, "nc": false },
    { "logical": 1, "physical": 1, "nc": true }
  ],
  "outputs": [
    { "logical": 0, "physical": 0, "nc": false },
    { "logical": 1, "physical": 33, "nc": false }
  ]
}
```

### 映射配置限制

| 限制 | 说明 |
|------|------|
| 物理索引范围 | 必须在 `[0, Hardware.OutputCount)` 范围内 |
| 唯一性 | 每个物理点最多被一个逻辑点映射 |
| 配置时机 | 应在 `Build()` 之前完成，不要运行时动态修改 |
| 线程安全 | MappingCache 无锁，运行期修改可能导致半更新/错映射 |

> 运行期修改映射会与 Tick 热路径并发读取冲突，可能出现短暂错映射或不一致。若必须调整，请先停止 Tick 并确保无并发访问，再调用 `Mapping.Set*` 或 `Mapping.Load`。

```csharp
// 正确：初始化时配置映射
var ctx = IoContextBuilder.For<MyIOLayout>()
    .WithHardware(hw)
    .WithMapping(cfg =>
    {
        cfg.SetOutputMapping(0, 5, isNormallyClosed: false);
        cfg.SetOutputMapping(1, 6, isNormallyClosed: true);
    })
    .Build();

// 错误：运行时修改映射（不推荐）
// ctx.Mapping.SetOutputMapping(0, 10);  // 不要这样做
```

---

## Wait/Confirm 使用模式

Wait/Confirm 用于"写输出 + 等待反馈确认"的场景（如真空吸取、夹爪动作）。

### 基本用法

```csharp
// 定义带超时的 IO 点
public class StationLayout
{
    [IO(10)]
    public OutputSignal 真空阀 { get; set; }

    [IO(20, ConfirmTimeoutMs = 300)]  // 单点超时
    public InputSignal 真空到位 { get; set; }
}
```

### async/await 方式（推荐）

```csharp
// 开真空并等待反馈
var ok = await ctx.Confirm(
    output: w => w.真空阀,
    value: true,
    confirm: r => r.真空到位,
    expected: true,
    timeout: TimeSpan.FromMilliseconds(500),
    now: true);

if (!ok)
{
    // 超时处理
}
```

### 状态机方式（10ms 扫描）

```csharp
IoOp<bool>? pending;

switch (step)
{
    case 0:
        // 只创建一次，不要每 10ms 重复创建
        pending = ctx.Confirm(
            output: w => w.真空阀,
            value: true,
            confirm: r => r.真空到位,
            expected: true,
            now: true);
        step = 1;
        break;

    case 1:
        if (pending == null || !pending.TryGetResult(out var ok))
            break;
        pending = null;
        step = ok ? 2 : -1;  // 成功或超时
        break;
}
```

### 超时优先级

1. 调用参数 `timeout`（如果传了）
2. IO 标签 `ConfirmTimeoutMs`
3. IoContext 默认超时

### 重要约束

- Wait/Confirm **依赖 Tick() 持续调用**才能检测条件变化
- **不要在 Tick 线程中阻塞等待**（会导致死锁）

---

## API 生命周期

### 初始化阶段（Build 之前）

| 操作 | 允许 |
|------|------|
| `WithHardware()` | 是 |
| `WithMapping()` | 是 |
| `WithId()` | 是 |
| `WithIndexOutOfRangeBehavior()` | 是 |

### 运行阶段（Build 之后）

| 操作 | 允许 | 说明 |
|------|------|------|
| `Tick()` | 是 | 主循环调用 |
| `On/Off/Pulse()` | 是 | 设置输出 |
| `GetInput()` / `GetOutput()` | 是 | 读取状态 |
| `Edge.R()` / `Edge.F()` | 是 | 边缘检测 |
| `Sim.ForceOn()` / `ForceOff()` | 是 | 模拟输入 |
| `Until()` / `Confirm()` | 是 | 异步等待 |
| `Flush()` | 是 | 强制下发 |
| 修改映射配置 | **否** | 不要在运行时修改 |

### 销毁阶段

```csharp
ctx.Dispose();  // 释放硬件连接和内部资源
// Dispose 后不要再调用任何方法
```

---

## 常见错误

### 1. 多线程调用 Tick

```csharp
// 错误
Parallel.For(0, 10, i => ctx.Tick());

// 正确
// 使用单一定时器线程
```

### 2. N:1 映射

```csharp
// 错误：两个逻辑点映射到同一物理点
cfg.SetOutputMapping(0, 5);
cfg.SetOutputMapping(1, 5);  // 物理点 5 被重复映射

// 正确：一对一映射
cfg.SetOutputMapping(0, 5);
cfg.SetOutputMapping(1, 6);
```

### 3. 运行时修改映射

```csharp
// 错误：Build 后修改映射
var ctx = builder.Build();
ctx.Mapping.SetOutputMapping(0, 10);  // 可能导致不一致

// 正确：Build 前配置完成
var ctx = builder
    .WithMapping(cfg => cfg.SetOutputMapping(0, 10))
    .Build();
```

### 4. 忘记调用 Tick

```csharp
// 错误：只设置输出，不调用 Tick
ctx.On(0);
// 输出不会下发到硬件！

// 正确：调用 Tick 或使用 now: true
ctx.On(0);
ctx.Tick();  // 下发到硬件

// 或者
ctx.On(0, now: true);  // 立即下发
```

> 注意：`Pulse` 需要 Tick 推进计数，不调用 Tick 将不会自动结束。

### 5. Dispose 后继续使用

```csharp
// 错误
ctx.Dispose();
ctx.Tick();  // ObjectDisposedException

// 正确
ctx.Dispose();
ctx = null;  // 清除引用，不再使用
```

### 6. 索引越界导致读写无效

```csharp
// 错误：索引越界，读返回 false，写不会生效
ctx.GetInput(999);
ctx.On(999);
```

**默认行为：**
- 返回 `false` / 忽略写入
- 在 `ctx.Health` 记录错误并触发 `HealthChanged`（`IndexOutOfRange`）

**严格模式：**
```csharp
var ctx = IoContextBuilder.For<MyLayout>()
    .WithHardware(hw)
    .WithIndexOutOfRangeBehavior(IndexOutOfRangeBehavior.Throw)
    .Build();
```
如需保持旧行为，可使用 `IndexOutOfRangeBehavior.Ignore`。

### 7. 轮询采样漏检短脉冲

`Tick()` 采用轮询采样，Snapshot/边沿检测基于相邻两帧。如果输入脉冲宽度小于 Tick 周期，脉冲可能在两次 Tick 之间出现并消失，从而被漏检（边缘丢失是轮询模型的固有限制）。

**规避建议：**
- 降低 Tick 周期（提高采样频率）
- 使用硬件锁存/高速计数/中断捕获（PLC/板卡支持）
- 通过硬件或电路对脉冲进行展宽/滤波
- 业务层避免用短脉冲做关键触发，改为稳定电平确认

### 8. 缓存 ctx.R 导致数据长期不变

`ctx.R` 返回当前帧对象引用；Tick 会交换前后缓冲。如果把 `ctx.R` 缓存在字段里，后续读取的是旧帧或被复用的对象，表现为“长期不变”或突变。正确做法是每次使用都重新读取 `ctx.R`，需要长期保存请自行复制字段。

---

## 性能建议

### Tick 周期选择

| 场景 | 推荐周期 |
|------|---------|
| 普通控制 | 10-20ms |
| 高速检测 | 5-10ms |
| 低速监控 | 50-100ms |

### 输出下发策略

| 策略 | 使用场景 |
|------|---------|
| `now: false`（默认） | 普通输出，批量下发效率高 |
| `now: true` | 紧急输出，需要立即生效 |

### 边缘检测 vs 轮询

```csharp
// 推荐：使用边缘检测（更高效）
if (ctx.Edge.R(x => x.启动按钮))
{
    StartProcess();
}

// 不推荐：每次轮询判断（效率低）
if (ctx.R.启动按钮 && !_lastButtonState)
{
    StartProcess();
}
_lastButtonState = ctx.R.启动按钮;
```

---

## 快捷方法

提供简洁的输出控制 API，减少样板代码。

### 方法列表

| 方法 | 效果 | 默认行为 |
|------|------|---------|
| `ctx.On(expr/index)` | 设置输出为 ON | 等 Tick 下发 |
| `ctx.Off(expr/index)` | 设置输出为 OFF | 等 Tick 下发 |
| `ctx.Pulse(expr/index, durationTicks, now=false, value=true)` | 输出脉冲 | 按 Tick 计数 |

### 使用示例

```csharp
// 默认：等 Tick 下发（now: false）
ctx.On(x => x.运行灯);
ctx.Off(x => x.报警灯);
ctx.Pulse(x => x.蜂鸣器, durationTicks: 3);

// 立即下发（now: true）
ctx.On(x => x.运行灯, now: true);
ctx.Off(x => x.报警灯, now: true);
ctx.Pulse(x => x.蜂鸣器, durationTicks: 1, now: true);

// 反向脉冲：OFF -> ON -> OFF
ctx.Pulse(x => x.指示灯, durationTicks: 2, value: false);
```

### Pulse 说明

- `durationTicks` 为 Tick 次数（非毫秒），例如 10ms Tick 下 3 表示约 30ms
- 同一输出在脉冲未结束时再次 `Pulse` 会被忽略
- 脉冲结束后的回落由 Tick 驱动，下一个 Tick 才会下发结束值
- 对同一输出调用 `On/Off` 会取消尚未完成的脉冲

---

## SMC606IO 连接字符串

`EwanIO.Hardware.SMC606IO.IOSMC606DriverWrapper` 支持富地址连接字符串（`|` 分隔，`key=value`）：

- 示例：`192.168.1.100|card=0|connecttype=2|baud=115200|input=40|output=34|inputboards=2|outputboards=2`
- `card/cardno`：板卡号（支持多板卡）
- `connecttype`：连接类型（0=PCI, 1=PCI-E, 2=Ethernet）
- `baud`：未指定时会依次尝试 `115200` 和 `1000000`

同时使用 `EwanAxis.Hardware.SMC606.SMC606Card` 与 `IOSMC606DriverWrapper` 时：

- 只需要各自调用 `Connect()`；两侧通过 `EwanSMC606.Smc606ConnectionPool` 自动共享连接，并使用同一互斥锁串行化 SDK 调用。

---

## 版本信息

- **文档版本**: 1.5
- **EwanIO 版本**: V2.4
- **更新日期**: 2025-12-21

---

## 更新记录

| 日期 | 版本 | 更新内容 |
|------|------|----------|
| 2025-12-16 | 1.0 | 初始文档 |
| 2025-12-16 | 1.1 | 添加设计理念、API 速查表、Wait/Confirm 使用模式 |
| 2025-12-16 | 1.2 | 更新边缘检测线程安全说明（现已支持多线程） |
| 2025-12-17 | 1.3 | 添加快捷方法（On/Off/Toggle）|
| 2025-12-20 | 1.4 | 收敛写 API（On/Off/Pulse）、补充线程安全与 Pulse 说明 |
| 2025-12-21 | 1.5 | 增加索引越界行为配置与错误记录说明 |
