# EwanAxis 使用指南

## 概述

EwanAxis 是一个**与 IO 系统完全解耦**的轴卡控制库。它只负责轴的运动控制，不包含任何通用 IO 操作。

## 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                      应用层                                  │
│                                                              │
│  ┌──────────────┐              ┌──────────────────────────┐ │
│  │ AxisManager  │              │ IoContext<MyLayout>      │ │
│  │ (轴管理)      │              │ (IO管理)                  │ │
│  └──────┬───────┘              └────────────┬─────────────┘ │
│         │                                    │               │
│         │      可选：应用层协调器              │               │
│         │   ┌──────────────────────┐        │               │
│         └──►│  SafetyController    │◄───────┘               │
│             │  (安全检查/联动控制)  │                        │
│             └──────────────────────┘                        │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────┐     ┌─────────────────────┐
│     EwanAxis        │     │      EwanIO         │
│  ┌───────────────┐  │     │  ┌───────────────┐  │
│  │  IAxisCard    │  │     │  │  IHardwareIO  │  │
│  │  IAxis        │  │     │  │  IoContext    │  │
│  └───────────────┘  │     │  └───────────────┘  │
│   无IO依赖          │     │   无轴依赖          │
└─────────────────────┘     └─────────────────────┘
```

## 初始化顺序

Axis 与 IO 是两套独立系统：可以只用其中一个。

```csharp
// ============ 示例1：通用 IO（如 IOC0640/PLC） + 轴卡 ============
var ioHardware = new IOC0640DriverWrapper();
ioHardware.Connect("192.168.1.100");

var io = new IoContextBuilder<MyIOLayout>()
    .WithHardware(ioHardware)
    .WithConfirmTimeout(TimeSpan.FromMilliseconds(3000))
    .Build();

// 启动IO刷新定时器（每10ms调用一次）
var ioTimer = new System.Threading.Timer(_ => io.Tick(), null, 0, 10);

var axisCard = new SMC606Card
{
    CardNo = 0,
    IpAddress = "192.168.1.100",
    ConnectType = 2,
    BaudRate = 115200
};
axisCard.Initialize("config/axis_config.json");
axisCard.Connect();

// ============ 示例2：SMC606 自带 IO（轴+IO 共卡） ============
// 只需要调用 Connect()；EwanAxis / EwanIO 通过 EwanSMC606.Smc606ConnectionPool 自动共享连接/互斥锁。
var smc606IoHardware = new IOSMC606DriverWrapper();
smc606IoHardware.Connect("192.168.1.100|card=0|baud=115200|input=40|output=34");

var smc606Io = new IoContextBuilder<MyIOLayout>()
    .WithHardware(smc606IoHardware)
    .WithConfirmTimeout(TimeSpan.FromMilliseconds(3000))
    .Build();
var smc606IoTimer = new System.Threading.Timer(_ => smc606Io.Tick(), null, 0, 10);

// ============ 可选：创建应用层协调器 ============
var safetyController = new SafetyController(axisCard, io);
```

## 基本使用

### 单独使用轴卡

```csharp
// 获取轴
var xAxis = axisCard[0];           // 通过索引
var yAxis = axisCard.GetAxisByName("Y轴");  // 通过名称

// 励磁
xAxis.ServoOn = true;

// 回原点
xAxis.Home();
while (!xAxis.HomeIsDown())
{
    Thread.Sleep(100);
}

// 绝对移动
xAxis.AbsMove(100.0);  // 移动到100mm
while (xAxis.IsBusy)
{
    Thread.Sleep(10);
}

// 点动
xAxis.Jog(10);   // 正向点动，速度10
xAxis.JogStop(); // 停止

// 紧急停止
xAxis.EmgStop();
```

### Async 便捷方法（推荐）

```csharp
// 需要 using System.Threading.Tasks;
await xAxis.HomeAsync(TimeSpan.FromSeconds(30));
await xAxis.AbsMoveAsync(100.0, TimeSpan.FromSeconds(10));
```

### 多卡/多轴（AxisManager）

```csharp
var axisManager = new AxisManager()
    .AddCard(axisCard)
    .AddCard(anotherAxisCard);

// 通过名字跨卡取轴
var zAxis = axisManager.GetAxisByName("Z轴");
await zAxis.AbsMoveAsync(50.0, TimeSpan.FromSeconds(10));
```

### 单独使用IO

```csharp
// 读取输入
bool doorClosed = io.R.DoorClosed;
bool sensor1 = io.GetInput(0);

// 控制输出
io.On(l => l.Cylinder1Extend);
io.Off(l => l.Cylinder1Extend);

// 等待确认
var result = await io.Confirm(
    output: l => l.Cylinder1Extend,
    value: true,
    confirm: l => l.Cylinder1ExtendSensor,
    expected: true,
    timeout: TimeSpan.FromSeconds(3)
);
```

### 应用层协调（轴+IO联动）

```csharp
public class SafetyController
{
    private readonly IAxisCard _axisCard;
    private readonly IoContext<MyIOLayout> _io;

    public SafetyController(IAxisCard axisCard, IoContext<MyIOLayout> io)
    {
        _axisCard = axisCard;
        _io = io;
    }

    /// <summary>
    /// 安全移动：先检查IO安全条件，再执行轴运动
    /// </summary>
    public async Task<bool> SafeMoveAsync(int axisIndex, double position)
    {
        // 1. 检查门是否关闭
        if (!_io.R.DoorClosed)
        {
            throw new SafetyException("安全门未关闭");
        }

        // 2. 检查急停按钮
        if (_io.R.EmergencyStop)
        {
            throw new SafetyException("急停按钮已按下");
        }

        // 3. 检查轴状态
        var axis = _axisCard[axisIndex];
        if (axis.IsAlarm)
        {
            throw new AxisAlarmException($"轴{axisIndex}处于报警状态");
        }

        // 4. 执行运动
        axis.AbsMove(position);

        // 5. 等待完成，同时持续检查安全条件
        while (axis.IsBusy)
        {
            if (_io.R.EmergencyStop)
            {
                axis.EmgStop();
                throw new SafetyException("运动过程中急停触发");
            }
            await Task.Delay(10);
        }

        return true;
    }

    /// <summary>
    /// 气缸动作+等待传感器
    /// </summary>
    public async Task<bool> CylinderExtendAsync()
    {
        // 使用IO系统控制气缸
        var result = await _io.Confirm(
            output: l => l.Cylinder1Extend,
            value: true,
            confirm: l => l.Cylinder1ExtendSensor,
            expected: true,
            timeout: TimeSpan.FromSeconds(3)
        );

        return result.Value;
    }
}
```

## 项目结构

```
EwanAxis/
├── EwanAxis.csproj
├── Core/
│   ├── Interfaces/
│   │   ├── IAxisCard.cs      # 轴卡接口
│   │   └── IAxis.cs          # 单轴接口
│   ├── Models/
│   │   ├── AxisParameter.cs  # 轴参数
│   │   ├── AxisIOState.cs    # 轴IO状态
│   │   └── AxisState.cs      # 轴状态枚举
│   ├── AxisCardBase.cs       # 轴卡抽象基类
│   └── AxisBase.cs           # 轴抽象基类
└── Hardware/
    └── SMC606/               # 具体硬件实现（待添加）
        ├── SMC606Card.cs
        └── SMC606Axis.cs
```

## 实现具体轴卡驱动

```csharp
// 继承 AxisCardBase 实现具体轴卡
public class SMC606Card : AxisCardBase
{
    private ushort _cardNo;

    public override int CardIndex => _cardNo;

    protected override IAxis CreateAxis(AxisParameter parameter)
    {
        return new SMC606Axis(this, parameter);
    }

    public override bool Connect()
    {
        // 调用SMC606 SDK连接
        short ret = LTSMC.smc_board_init(_cardNo, 2, IpAddress, 1000000);
        IsConnected = ret == 0;
        OnConnectionChanged(IsConnected);
        return IsConnected;
    }

    public override bool Disconnect()
    {
        short ret = LTSMC.smc_board_close(_cardNo);
        IsConnected = false;
        OnConnectionChanged(false);
        return ret == 0;
    }
}

// 继承 AxisBase 实现具体轴
public class SMC606Axis : AxisBase
{
    private readonly SMC606Card _card;

    public SMC606Axis(SMC606Card card, AxisParameter parameter) 
        : base(parameter)
    {
        _card = card;
    }

    public override double Position
    {
        get
        {
            double pos = 0;
            LTSMC.smc_get_position_unit(_card.CardNo, (ushort)AxisIndex, ref pos);
            return PulseToPosition(pos);
        }
        set => LTSMC.smc_set_position_unit(_card.CardNo, (ushort)AxisIndex, PositionToPulse(value));
    }

    // ... 实现其他抽象成员
}
```

## 优势

1. **解耦**：轴卡和IO系统完全独立，可以单独使用、测试、升级
2. **可测试**：可以在没有硬件的情况下对轴卡逻辑进行单元测试
3. **灵活**：不同项目可以选择不同的IO系统，或者根本不使用IO
4. **清晰**：职责分离，轴卡只管运动，IO只管信号，协调在应用层
