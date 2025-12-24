# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

**Current Version**: V2 Only (V1 has been completely removed)

This is EwanIO, a modern industrial IO control library implementing a high-performance, instance-based architecture.

## Build Commands

```bash
# Build all target frameworks
dotnet build

# Build specific framework
dotnet build -f net472

# Run tests (all 70 tests)
dotnet test EwanIO.Tests/EwanIO.Tests.csproj

# Clean build
dotnet clean && dotnet build
```

## Architecture Overview

EwanIO provides a complete IO control solution with:
- **IoContext**: Instance-based context (multiple instances per Layout type)
- **Double-buffered Snapshot**: Frame consistency + atomic swap
- **Command buffering**: Thread-safe writes + dirty tracking
- **Zero-allocation Tick**: Hot path doesn't allocate objects
- **Edge detection**: Rising/falling edge detection on logical inputs
- **Simulation**: Override inputs for testing without hardware
- **Mapping**: Logical-to-physical mapping with NO/NC support
- **Code generation**: Generate constants, accessors, and documentation

## Folder Structure

```
EwanIO/
├── Core/                       # Core functionality (按功能分组)
│   ├── Attributes/             # 特性定义
│   │   └── IOAttribute.cs      # [IO] attribute and signal types
│   ├── Context/                # 上下文核心
│   │   ├── IoContext.cs        # Main context class
│   │   ├── IoContextBuilder.cs # Fluent builder
│   │   ├── IoHealth.cs         # Health monitoring
│   │   └── IoOp.cs             # Async operation wrapper
│   ├── Data/                   # 数据结构
│   │   ├── Snapshot.cs         # Double-buffered snapshot
│   │   ├── Command.cs          # Output command buffer
│   │   └── CommandOptimized.cs # Port-level dirty tracking
│   ├── Mapping/                # 映射系统
│   │   ├── MappingCache.cs     # Fast lookup cache
│   │   └── MappingConfig.cs    # Config file I/O
│   ├── Metadata/               # 元数据
│   │   └── MetaManager.cs      # Layout metadata scanner
│   ├── Simulation/             # 模拟系统
│   │   └── SimManager.cs       # Input simulation
│   ├── EdgeDetection/          # 边缘检测
│   │   └── EdgeManager.cs      # Edge detection logic
│   ├── CodeGen/                # 代码生成
│   │   └── LayoutCodeGenerator.cs # Generate code from Layout
│   └── Interfaces/             # 硬件接口
│       ├── IHardwareIO.cs      # Basic hardware interface
│       └── IHardwareIOExtended.cs # Extended with InputSync/OutputSync
├── Hardware/                   # Hardware implementations
│   ├── Mitsubishi/             # Mitsubishi PLC (MC Protocol)
│   ├── IOC0640/                # IOC0640 board support
│   ├── SMC606IO/               # SMC606 board support
│   ├── Composite/              # Composite hardware (multiple boards)
│   └── InMemory/               # In-memory mock (for testing)
└── Documentation/              # Documentation files
    ├── CLAUDE.md               # This file
    └── REFACTOR_PLAN.md        # V2 implementation plan (completed)

EwanIO.Tests/                   # All tests (70 tests, all passing)
├── IoContextTests.cs           # Core context tests (20 tests)
├── IoHealthAndMappingTests.cs  # Health & mapping tests (17 tests)
├── HardwareBackendTests.cs     # Hardware backend tests (8 tests)
├── BulkWriteOptimizationTests.cs # Bulk write optimization (10 tests)
└── CodeGenerationTests.cs      # Code generation tests (15 tests)
```

## Key Features

### Instance-Based Architecture
- **Multiple Contexts**: Same Layout type can have multiple IoContext instances
- **Isolated State**: Each context has its own hardware, mapping, simulation
- **Example**: Two work stations with same Layout but different hardware/config

### Zero-Allocation Design
- **Hot Path**: Tick() method allocates zero objects in steady state
- **Performance**: Suitable for 10ms or faster control loops
- **Strategy**: Pre-allocated arrays, dirty tracking, atomic operations

### Frame Consistency
- **Snapshot**: All reads see the same frame (no tearing)
- **Atomic Swap**: Double-buffered snapshot updates atomically
- **Thread Safety**: Read-only snapshot can be shared across threads

### Hardware Abstraction
- **IHardwareIO**: Basic interface (DataSync, ReadInBit, WriteOutBit, etc.)
- **IHardwareIOExtended**: Extended interface with InputSync/OutputSync separation
- **Capability Detection**: Hardware reports capabilities (FullySeparatedSync, etc.)
- **Automatic Fallback**: IoContext uses granular sync if available, otherwise DataSync

## Usage Examples

### Basic Usage

```csharp
// Define Layout (properties = documentation)
public class StationLayout
{
    [IO(0)] public InputSignal 启动按钮 { get; set; }

    [IO(0)] public OutputSignal 运行灯 { get; set; }
}

// Build context
var ctx = IoContextBuilder.For<StationLayout>()
    .WithId("StationA")
    .WithHardware(h => h.UseInMemory(64, 64))
    .Build();

// Main loop (10ms tick)
void Tick10ms()
{
    ctx.Tick();  // Sync hardware + update snapshot + flush dirty outputs

    // Read (always from Snapshot)
    if (ctx.R.启动按钮)
    {
        ctx.On(x => x.运行灯);  // Default: wait next Tick
    }

    // Immediate write (now=true)
    ctx.Off(x => x.运行灯, now: true);
}

// Edge detection
if (ctx.Edge.R(x => x.启动按钮))  // Rising edge
{
    // ...
}

// Simulation
ctx.Sim.ForceOn(x => x.启动按钮);

// Wait for input (async)
var ok = await ctx.Until(x => x.启动按钮, expected: true, timeout: TimeSpan.FromSeconds(1));

// Confirm action (write + wait for feedback)
var result = await ctx.Confirm(
    output: w => w.运行灯,
    value: true,
    confirm: r => r.启动按钮,
    expected: true,
    timeout: TimeSpan.FromMilliseconds(500),
    now: true);
```

### Code Generation

```csharp
var generator = new LayoutCodeGenerator(typeof(StationLayout));

// Generate constants
string constants = generator.GenerateConstants();
// → public const int 启动按钮 = 0;

// Generate accessors (extension methods)
string accessors = generator.GenerateAccessors();
// → public static bool Get启动按钮(this IoContext<StationLayout> ctx)

// Generate documentation
string markdown = generator.GenerateMarkdownDoc();

// Generate all files
generator.GenerateAll(outputDirectory: "Generated/");
```

## Thread Safety

- **Snapshot (R)**: Thread-safe, read-only; don't cache references across ticks
- **On/Off/Pulse**: Thread-safe (internal lock)
- **Tick()**: NOT thread-safe, call from one thread only
- **Edge**: Thread-safe for read/clear (Interlocked/Volatile)

## Test Coverage

- **Total**: 70 tests, all passing ✅
- **P0 Tests** (20): Core functionality (Snapshot, Command, On/Off/Pulse, Edge, Sim)
- **P1 Tests** (17): Health monitoring, mapping, Until/Confirm, hardware backend
- **P2 Tests** (25): Bulk write optimization (10) + Code generation (15)

## Performance

- **Tick Time**: <1ms for typical workloads (64 I/O points)
- **Memory**: Zero allocation during Tick (steady state)
- **Hardware Calls**: Minimized via bulk operations and dirty tracking

## Migration from V1

V1 has been completely removed. If you have old code using V1 namespaces:
- ~~`using EwanIO.Core.V2;`~~ → Use specific namespaces (Context, Data, Mapping, etc.)
- ~~`IOModelBase<T>.Instance`~~ → `IoContextBuilder.For<T>().Build()`
- ~~`LayeredIO`~~ → `IoContext<T>`
- ~~`IOEntry<T>`~~ → `IoContext<T>`

### Real-world Migration Example: MCIOMonitor

**MCIOMonitor** (MC PLC IO monitoring application) has been successfully migrated from V1 to V2:

**Before (V1)**:
```csharp
// Static Model with all methods
public class MCPlcIOModel
{
    private static LayeredIO? _layeredIO;
    public static void Initialize(LayeredIO io) { ... }
    public static void Refresh() => _layeredIO.DataSync();
    public static bool GetInputValue(int i) => ...;
    public static void SetOutputValue(int i, bool v) => ...;
}

// In Form1.cs
layeredIO = LayeredIOBuilder.Create()
    .WithMitsubishiPLC(mcPlc, 64)
    .Build();
MCPlcIOModel.Initialize(layeredIO);
MCPlcIOModel.Refresh();
```

**After (V2)**:
```csharp
// Pure Layout class (properties only)
public class MCPlcIOModel
{
    [IO(0)] public InputSignal 安全门 { get; set; }
    [IO(0)] public OutputSignal Y0 { get; set; }
    // ... 64 inputs + 64 outputs
}

// In Form1.cs
var mcPlc = new MCPlc(new MCProtocolPlc(), 64);
context = IoContextBuilder.For<MCPlcIOModel>()
    .WithId("MCPlcIO")
    .WithHardware(mcPlc)
    .WithMapping("Config/IO/io_config.json")
    .Build();
context.Tick();  // Refresh
var state = context.GetInput(0);  // Read
context.On(0, now: true);  // Write
```

**Migration Stats**:
- Files changed: 3 (MCPlcIOModel.cs, Form1.cs, IOMappingConfigForm.cs)
- Lines changed: ~230
- Build result: 0 errors, all 70 tests passing
- Time: ~1 hour

This migration demonstrates that V2 provides cleaner, more flexible code with better separation of concerns.

## Namespace Guide

| Old (V1/V2) | New | Content |
|-------------|-----|---------|
| `EwanIO.Core.V2` | `EwanIO.Core.Attributes` | IOAttribute, InputSignal, OutputSignal |
| `EwanIO.Core.V2` | `EwanIO.Core.Context` | IoContext, IoContextBuilder, IoHealth, IoOp |
| `EwanIO.Core.V2` | `EwanIO.Core.Data` | Snapshot, Command, CommandOptimized |
| `EwanIO.Core.V2` | `EwanIO.Core.Mapping` | MappingCache, MappingConfig |
| `EwanIO.Core.V2` | `EwanIO.Core.Metadata` | MetaManager, IoMeta |
| `EwanIO.Core.V2` | `EwanIO.Core.Simulation` | SimManager |
| `EwanIO.Core.V2` | `EwanIO.Core.EdgeDetection` | EdgeManager |
| `EwanIO.Core.V2.CodeGen` | `EwanIO.Core.CodeGen` | LayoutCodeGenerator |

## Best Practices

1. **Use properties for documentation**: Chinese property names are fully supported
2. **Don't read hardware directly**: Always read from ctx.R (Snapshot)
3. **Default to buffered writes**: Use `now=false` unless you need immediate effect
4. **External Tick**: Call Tick() from your own 10ms/20ms timer (don't rely on library threads)
5. **One Tick per context**: Each IoContext needs its own Tick call
6. **Confirm for feedback**: Use Confirm() for actions that need sensor confirmation (vacuum, gripper, etc.)
7. **Simulation for testing**: Use ctx.Sim to test logic without hardware
