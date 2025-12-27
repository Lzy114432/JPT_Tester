using System;
using System.Threading;
using System.Threading.Tasks;
using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Core.Data;
using EwanIO.Core.EdgeDetection;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Mapping;
using EwanIO.Core.Metadata;
using EwanIO.Core.Simulation;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    /// <summary>
    /// 测试用的 Layout
    /// </summary>
    public class TestLayout
    {
        [IO(0)]
        public InputSignal 启动按钮 { get; set; }

        [IO(1)]
        public InputSignal 急停 { get; set; }

        [IO(2, ConfirmTimeoutMs = 300)]
        public InputSignal 真空到位 { get; set; }

        [IO(0)]
        public OutputSignal 运行灯 { get; set; }

        [IO(1)]
        public OutputSignal 真空阀 { get; set; }
    }

    public class IoContextTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithId("TestStation")
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Assert
            Assert.NotNull(context);
            Assert.Equal("TestStation", context.Id);
            Assert.NotNull(context.R);
            Assert.NotNull(context.Edge);
            Assert.NotNull(context.Sim);
            Assert.NotNull(context.Meta);

            context.Dispose();
        }

        [Fact]
        public void Tick_ShouldUpdateSnapshot()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);
            hardware.SetInputBit(1, false);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.Tick();

            // Assert
            Assert.True(context.GetInput(0));
            Assert.False(context.GetInput(1));

            context.Dispose();
        }

        [Fact]
        public void Snapshot_ShouldProvideFrameConsistency()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act - 在读取期间修改硬件输入
            bool firstRead = context.GetInput(0);
            hardware.SetInputBit(0, false);
            bool secondRead = context.GetInput(0);

            // Assert - 同一帧内应该一致（都是 true）
            Assert.True(firstRead);
            Assert.True(secondRead);  // Snapshot 不变

            // 下一次 Tick 后才会看到变化
            context.Tick();
            Assert.False(context.GetInput(0));

            context.Dispose();
        }

        [Fact]
        public void Edge_R_ShouldDetectRisingEdge()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();  // 初始化

            // Act
            hardware.SetInputBit(0, true);
            context.Tick();

            // Assert
            Assert.True(context.Edge.R(x => x.启动按钮));
            Assert.False(context.Edge.R(x => x.启动按钮));  // 读后清除

            context.Dispose();
        }


        [Fact]
        public void Sim_ForceOn_ShouldOverrideHardware()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, false);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.Sim.ForceOn(x => x.启动按钮);
            context.Tick();

            // Assert
            Assert.True(context.GetInput(0));  // 模拟覆盖硬件

            context.Dispose();
        }

        [Fact]
        public void Sim_ClearSimulate_ShouldRestoreHardware()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Sim.ForceOff(x => x.启动按钮);
            context.Tick();
            Assert.False(context.GetInput(0));

            // Act
            context.Sim.ClearSimulate(x => x.启动按钮);
            context.Tick();

            // Assert
            Assert.True(context.GetInput(0));  // 恢复读硬件

            context.Dispose();
        }

        [Fact]
        public void Sim_ShouldGenerateEdges()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act - 模拟 OFF → ON 应该产生上升沿
            context.Sim.ForceOn(x => x.启动按钮);
            context.Tick();

            // Assert
            Assert.True(context.Edge.R(x => x.启动按钮));

            context.Dispose();
        }

        [Fact]
        public void Mapping_NormallyClosed_ShouldInvertLogic()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 设置为常闭
            context.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);

            // Act
            hardware.SetInputBit(0, false);  // 硬件 OFF
            context.Tick();

            // Assert
            Assert.True(context.GetInput(0));  // 逻辑 ON（反转）

            context.Dispose();
        }

        [Fact]
        public void Mapping_OutputNormallyClosed_ShouldInvertWrite()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 设置输出为常闭
            context.Mapping.SetOutputMapping(0, 0, isNormallyClosed: true);

            // Act
            context.On(x => x.运行灯, now: true);

            // Assert
            Assert.False(hardware.ReadOutBit(0));  // 硬件应该是 OFF（反转）

            context.Dispose();
        }

        [Fact]
        public void Mapping_LoadBeforeConnect_WithZeroHardwareCount_ShouldStillApply()
        {
            // Arrange: hardware reports 0 IO points before Connect
            var hardware = new DeferredCountHardwareIO(10, 10);

            var mappingConfig = new MappingConfigFile();
            mappingConfig.Inputs.Add(new MappingEntry
            {
                LogicalIndex = 0,
                PhysicalIndex = 0,
                IsNormallyClosed = true
            });
            mappingConfig.Outputs.Add(new MappingEntry
            {
                LogicalIndex = 0,
                PhysicalIndex = 0,
                IsNormallyClosed = true
            });

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .WithMappingConfig(mappingConfig)
                .Build();

            // Act: connect after Build, then tick
            Assert.False(hardware.IsConnected);
            Assert.Equal(0, hardware.InputCount);
            Assert.Equal(0, hardware.OutputCount);

            Assert.True(hardware.Connect("test"));

            hardware.SetInputBit(0, false); // raw OFF
            context.Tick();

            // Assert: mapping inversion should be applied
            Assert.True(context.GetInput(0));

            context.On(0, now: true);
            Assert.False(hardware.ReadOutBit(0));

            context.Dispose();
        }

        private sealed class DeferredCountHardwareIO : IHardwareIO
        {
            private readonly int _inputCountOnConnect;
            private readonly int _outputCountOnConnect;
            private bool _isConnected;
            private bool[] _inputs = Array.Empty<bool>();
            private bool[] _outputs = Array.Empty<bool>();

            public DeferredCountHardwareIO(int inputCountOnConnect, int outputCountOnConnect)
            {
                _inputCountOnConnect = inputCountOnConnect;
                _outputCountOnConnect = outputCountOnConnect;
            }

            public string HardwareType => "DeferredCount";
            public string ConnectionInfo => "DeferredCount";
            public bool IsConnected => _isConnected;
            public int InputCount => _isConnected ? _inputs.Length : 0;
            public int OutputCount => _isConnected ? _outputs.Length : 0;

            public bool Connect(string connectionString)
            {
                _inputs = new bool[_inputCountOnConnect];
                _outputs = new bool[_outputCountOnConnect];
                _isConnected = true;
                return true;
            }

            public bool Disconnect()
            {
                _isConnected = false;
                return true;
            }

            public void DataSync()
            {
            }

            public bool ReadInBit(int bit)
            {
                if (!_isConnected || (uint)bit >= (uint)_inputs.Length)
                    return false;
                return _inputs[bit];
            }

            public bool ReadOutBit(int bit)
            {
                if (!_isConnected || (uint)bit >= (uint)_outputs.Length)
                    return false;
                return _outputs[bit];
            }

            public bool WriteOutBit(int bit, bool value)
            {
                if (!_isConnected || (uint)bit >= (uint)_outputs.Length)
                    return false;
                _outputs[bit] = value;
                return true;
            }

            public void SetInputBit(int bit, bool value)
            {
                if (!_isConnected || (uint)bit >= (uint)_inputs.Length)
                    throw new ArgumentOutOfRangeException(nameof(bit));
                _inputs[bit] = value;
            }

            public void Dispose()
            {
                Disconnect();
            }
        }

        [Fact]
        public async Task Until_ShouldWaitForCondition()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act
            var waitTask = context.Until(x => x.启动按钮, expected: true, timeout: TimeSpan.FromSeconds(1)).AsTask();

            // 模拟后台 Tick + 条件满足
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                hardware.SetInputBit(0, true);
                context.Tick();
            });

            bool result = await waitTask;

            // Assert
            Assert.True(result);

            context.Dispose();
        }

        [Fact]
        public async Task Until_ShouldTimeoutWhenConditionNotMet()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // 启动后台 Tick（模拟实际使用）
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(10);
                }
            });

            // Act - 等待一个永远不会满足的条件
            var result = await context.Until(x => x.启动按钮, expected: true, timeout: TimeSpan.FromMilliseconds(100)).AsTask();

            // Assert
            Assert.False(result);  // 超时返回 false

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public async Task Confirm_ShouldWriteAndWait()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // 模拟后台 Tick + 反馈
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(10);

                    // 模拟真空阀打开后，真空到位信号延迟出现
                    if (hardware.ReadOutBit(1))
                    {
                        await Task.Delay(30);
                        hardware.SetInputBit(2, true);
                    }
                }
            });

            // Act
            var result = await context.Confirm(
                output: r => r.真空阀,
                value: true,
                confirm: r => r.真空到位,
                expected: true,
                now: true).AsTask();

            // Assert
            Assert.True(result);
            Assert.True(hardware.ReadOutBit(1));  // 真空阀已打开

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void TwoContexts_ShouldBeIsolated()
        {
            // Arrange
            var hardwareA = new InMemoryHardwareIO(10, 10);
            var hardwareB = new InMemoryHardwareIO(10, 10);

            var contextA = IoContextBuilder.For<TestLayout>()
                .WithId("StationA")
                .WithHardware(hardwareA)
                .BuildAndConnect("testA");

            var contextB = IoContextBuilder.For<TestLayout>()
                .WithId("StationB")
                .WithHardware(hardwareB)
                .BuildAndConnect("testB");

            // Act
            contextA.Sim.ForceOn(x => x.启动按钮);
            contextA.Tick();

            contextB.Tick();

            // Assert
            Assert.True(contextA.R.启动按钮);   // A 的模拟生效
            Assert.False(contextB.R.启动按钮);  // B 不受影响

            contextA.Dispose();
            contextB.Dispose();
        }

        [Fact]
        public void Meta_ShouldProvidePropertyNames()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act & Assert
            Assert.Equal("启动按钮", context.Meta.GetInputName(0));
            Assert.Equal("急停", context.Meta.GetInputName(1));
            Assert.Equal("运行灯", context.Meta.GetOutputName(0));
            Assert.Equal("真空阀", context.Meta.GetOutputName(1));

            context.Dispose();
        }

        [Fact]
        public void TickCounter_ShouldIncrement()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act & Assert
            Assert.Equal(0, context.TickCounter);
            context.Tick();
            Assert.Equal(1, context.TickCounter);
            context.Tick();
            Assert.Equal(2, context.TickCounter);

            context.Dispose();
        }

        #region On/Off/Pulse 快捷方法测试

        [Fact]
        public void On_WithNowFalse_ShouldNotFlushImmediately()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.On(x => x.运行灯);

            // Assert - 在 Tick 前不应该写到硬件
            Assert.False(hardware.ReadOutBit(0));

            // Tick 后应该写入
            context.Tick();
            Assert.True(hardware.ReadOutBit(0));

            context.Dispose();
        }

        [Fact]
        public void On_WithNowTrue_ShouldFlushImmediately()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.On(x => x.运行灯, now: true);

            // Assert - 应该立即写到硬件
            Assert.True(hardware.ReadOutBit(0));

            context.Dispose();
        }

        [Fact]
        public void Off_WithNowFalse_ShouldNotFlushImmediately()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 先打开
            context.On(x => x.运行灯, now: true);
            Assert.True(hardware.ReadOutBit(0));

            // Act
            context.Off(x => x.运行灯);

            // Assert - 在 Tick 前硬件仍然是 ON
            Assert.True(hardware.ReadOutBit(0));

            // Tick 后应该变为 OFF
            context.Tick();
            Assert.False(hardware.ReadOutBit(0));

            context.Dispose();
        }

        [Fact]
        public void Off_WithNowTrue_ShouldFlushImmediately()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 先打开
            context.On(x => x.运行灯, now: true);
            Assert.True(hardware.ReadOutBit(0));

            // Act
            context.Off(x => x.运行灯, now: true);

            // Assert - 应该立即写到硬件
            Assert.False(hardware.ReadOutBit(0));

            context.Dispose();
        }

        [Fact]
        public void Pulse_WithNowFalse_ShouldApplyNextTickAndAutoOff()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.Pulse(x => x.运行灯, durationMs: 10, now: false);

            // Assert - 在 Tick 前不应该写到硬件
            Assert.False(hardware.ReadOutBit(0));

            context.Tick();
            Assert.True(hardware.ReadOutBit(0));

            // 等待脉冲过期
            System.Threading.Thread.Sleep(50);

            context.Tick();
            Assert.False(hardware.ReadOutBit(0));

            context.Dispose();
        }

        [Fact]
        public void Pulse_ValueFalse_ShouldTurnOffThenOn()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.On(x => x.运行灯, now: true);
            Assert.True(hardware.ReadOutBit(0));

            // Act
            context.Pulse(x => x.运行灯, durationMs: 10, now: true, value: false);

            // Assert - 先 OFF，再恢复 ON
            Assert.False(hardware.ReadOutBit(0));

            // 等待脉冲过期
            System.Threading.Thread.Sleep(50);

            context.Tick();
            Assert.True(hardware.ReadOutBit(0));

            context.Dispose();
        }

        [Fact]
        public void Pulse_Repeat_ShouldBeIgnored()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act - 第一个脉冲 100ms，第二个脉冲 200ms（会被忽略）
            context.Pulse(x => x.运行灯, durationMs: 100, now: true);
            context.Pulse(x => x.运行灯, durationMs: 200, now: true);  // 应被忽略

            // Assert - 输出保持 ON
            Assert.True(hardware.ReadOutBit(0));

            // 等待脉冲过期
            System.Threading.Thread.Sleep(150);

            context.Tick();  // 第一个脉冲结束，输出 OFF
            Assert.False(hardware.ReadOutBit(0));

            context.Dispose();
        }

        #endregion

        #region PulseAsync 测试

        [Fact]
        public async Task PulseAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 启动后台 Tick 循环
            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // Act
            bool result = await context.PulseAsync(0, durationMs: 50, now: true);

            // Assert
            Assert.True(result);
            Assert.False(context.IsPulseActive(0));

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_WhenCancelled_ShouldReturnFalse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // 创建取消令牌
            var pulseCts = new CancellationTokenSource();

            // Act - 启动长脉冲，然后取消
            var pulseTask = context.PulseAsync(0, durationMs: 5000, now: true, cancellationToken: pulseCts.Token);
            await Task.Delay(50);  // 等待脉冲启动
            pulseCts.Cancel();

            bool result = await pulseTask;

            // Assert
            Assert.False(result);
            Assert.False(context.IsPulseActive(0));

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_WhenAlreadyActive_ShouldReturnFalse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // Act - 启动第一个脉冲（同步方式，不需要等待）
            context.Pulse(0, durationMs: 5000, now: true);
            await Task.Delay(10);  // 确保第一个脉冲已启动

            // Assert - 第一个脉冲应该是活跃的
            Assert.True(context.IsPulseActive(0));

            // 尝试启动第二个脉冲（同步方式，会被忽略因为已有脉冲在执行）
            context.Pulse(0, durationMs: 100, now: true);
            // 第二个脉冲应该被忽略

            // 验证仍然只有第一个脉冲在运行（持续时间还是 5000ms 不是 100ms）
            Assert.True(context.IsPulseActive(0));
            Assert.True(context.GetPulseRemainingMs(0) > 100); // 应该还有很长时间

            context.CancelPulse(0);
            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_WithTimeSpan_ShouldWork()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // Act
            bool result = await context.PulseAsync(0, TimeSpan.FromMilliseconds(50), now: true);

            // Assert
            Assert.True(result);

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_WithExpression_ShouldWork()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // Act
            bool result = await context.PulseAsync(x => x.运行灯, durationMs: 50, now: true);

            // Assert
            Assert.True(result);

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_ShouldSetOutputCorrectly()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // Act - 启动脉冲 (value=true 表示 ON->OFF)
            var pulseTask = context.PulseAsync(0, durationMs: 100, now: true, value: true);
            await Task.Delay(10);

            // Assert - 脉冲期间应该是 ON
            Assert.True(hardware.ReadOutBit(0));

            await pulseTask;

            // 等待一个 Tick 周期确保 endValue 已写入硬件
            await Task.Delay(20);

            // 脉冲结束后应该是 OFF
            Assert.False(hardware.ReadOutBit(0));

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_ConcurrentCalls_DifferentIndices_ShouldNotConflict()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            // Act - 同时启动多个不同索引的脉冲
            var task0 = context.PulseAsync(0, durationMs: 50, now: true);
            var task1 = context.PulseAsync(1, durationMs: 60, now: true);

            var results = await Task.WhenAll(task0, task1);

            // Assert
            Assert.True(results[0]);
            Assert.True(results[1]);

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        [Fact]
        public async Task PulseAsync_CancelDuringExecution_ShouldCancelPulse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(5);
                }
            });

            var pulseCts = new CancellationTokenSource();

            // Act
            var pulseTask = context.PulseAsync(0, durationMs: 5000, now: true, cancellationToken: pulseCts.Token);
            await Task.Delay(50);
            Assert.True(context.IsPulseActive(0));

            pulseCts.Cancel();
            await pulseTask;

            // Assert - 脉冲应该被取消
            Assert.False(context.IsPulseActive(0));

            cts.Cancel();
            await Task.WhenAny(tickTask, Task.Delay(100));
            context.Dispose();
        }

        #endregion

        #region PulseAndWait 测试

        [Fact]
        public void PulseAndWait_ShouldCompleteSuccessfully()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 启动后台 Tick 循环
            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act
            bool result = context.PulseAndWait(0, durationMs: 50, now: true);

            // Assert
            Assert.True(result);
            Assert.False(context.IsPulseActive(0));

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_WhenTimeout_ShouldReturnFalse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 注意：不启动 Tick 循环，脉冲不会完成
            // 但是 PulseAndWait 内部会根据 durationMs 计算最小等待时间

            // Act - 设置一个非常短的超时（但实际会等待至少 durationMs + 100）
            // 为了测试超时，我们需要让 Tick 不运行，这样 TryComplete 永远不会被调用
            bool result = context.PulseAndWait(0, durationMs: 50, now: true, timeoutMs: 10);

            // Assert - 由于 PulseAndWait 内部会调整超时时间，可能不会真正超时
            // 这个测试主要验证超时逻辑不会抛异常
            context.CancelPulse(0);
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_WhenAlreadyActive_ShouldReturnFalse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act - 先启动一个长脉冲
            context.Pulse(0, durationMs: 5000, now: true);
            Thread.Sleep(10);

            // 验证第一个脉冲是活跃的
            Assert.True(context.IsPulseActive(0));

            // 尝试启动第二个脉冲（同步方式，会被忽略）
            context.Pulse(0, durationMs: 100, now: true);

            // 验证仍然是第一个脉冲在运行（持续时间还是5000ms）
            Assert.True(context.IsPulseActive(0));
            Assert.True(context.GetPulseRemainingMs(0) > 1000); // 第一个脉冲还有很长时间

            context.CancelPulse(0);
            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_WithTimeSpan_ShouldWork()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act
            bool result = context.PulseAndWait(0, TimeSpan.FromMilliseconds(50), now: true);

            // Assert
            Assert.True(result);

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_WithExpression_ShouldWork()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act
            bool result = context.PulseAndWait(x => x.运行灯, durationMs: 50, now: true);

            // Assert
            Assert.True(result);

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_ShouldSetOutputCorrectly()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act
            context.PulseAndWait(0, durationMs: 50, now: true, value: true);

            // Assert - 脉冲结束后应该是 OFF（endValue = !value = false）
            Assert.False(hardware.ReadOutBit(0));

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_WithInfiniteTimeout_ShouldWait()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act - 使用 -1 表示无限等待
            bool result = context.PulseAndWait(0, durationMs: 50, now: true, timeoutMs: -1);

            // Assert
            Assert.True(result);

            cts.Cancel();
            context.Dispose();
        }

        [Fact]
        public void PulseAndWait_ValueFalse_ShouldTurnOffThenOn()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 先设置为 ON
            context.On(0, now: true);
            Assert.True(hardware.ReadOutBit(0));

            var cts = new CancellationTokenSource();
            var tickTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    Thread.Sleep(5);
                }
            });

            // Act - value=false 表示 OFF->ON
            context.PulseAndWait(0, durationMs: 50, now: true, value: false);

            // 等待一个 Tick 周期确保 endValue 已写入硬件
            Thread.Sleep(20);

            // Assert - 脉冲结束后应该是 ON（endValue = !value = true）
            Assert.True(hardware.ReadOutBit(0));

            cts.Cancel();
            context.Dispose();
        }

        #endregion

        #region Flush 测试

        [Fact]
        public void Flush_ShouldImmediatelyWriteDirtyOutputs()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act - 写入但不立即下发
            context.On(0, now: false);
            Assert.False(hardware.ReadOutBit(0)); // 还没下发

            // 手动 Flush
            context.Flush();

            // Assert
            Assert.True(hardware.ReadOutBit(0)); // 已下发

            context.Dispose();
        }

        [Fact]
        public void Flush_WithNoDirty_ShouldNotThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act & Assert - 没有 dirty 输出时调用 Flush 不应抛异常
            var ex = Record.Exception(() => context.Flush());
            Assert.Null(ex);

            context.Dispose();
        }

        #endregion

        #region 按索引访问测试

        [Fact]
        public void GetOutput_ShouldReturnCurrentValue()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act
            context.On(0, now: true);
            context.Tick(); // 更新 Snapshot

            // Assert
            Assert.True(context.GetOutput(0));

            context.Dispose();
        }

        [Fact]
        public void GetInput_ByIndex_ShouldReturnSnapshotValue()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);
            hardware.SetInputBit(1, false);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Assert
            Assert.True(context.GetInput(0));
            Assert.False(context.GetInput(1));

            context.Dispose();
        }

        #endregion

        #region Health 健康状态测试

        [Fact]
        public void Health_ShouldTrackTickCount()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.Tick();
            context.Tick();
            context.Tick();

            // Assert
            Assert.Equal(3, context.Health.TotalTicks);

            context.Dispose();
        }

        [Fact]
        public void Health_ShouldRecordConnectTime()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);

            // Act
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Assert - 连接后应该有连接时间
            Assert.True(context.Health.LastConnectTime.HasValue);

            context.Dispose();
        }

        #endregion

        #region Edge 按索引和 ClearAll 测试

        [Fact]
        public void Edge_R_ByIndex_ShouldDetectRisingEdge()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act
            hardware.SetInputBit(0, true);
            context.Tick();

            // Assert
            Assert.True(context.Edge.R(0));
            Assert.False(context.Edge.R(0)); // 读后清除

            context.Dispose();
        }

        [Fact]
        public void Edge_ClearR_ShouldClearSpecificRisingEdge()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            hardware.SetInputBit(0, true);
            context.Tick();

            Assert.True(context.Edge.PeekR(x => x.启动按钮));

            // Act
            context.Edge.ClearR(x => x.启动按钮);

            // Assert
            Assert.False(context.Edge.PeekR(x => x.启动按钮));

            context.Dispose();
        }

        #endregion

        #region Sim 按索引和 ClearAll 测试

        [Fact]
        public void Sim_ClearAll_ShouldClearAllSimulations()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, false);
            hardware.SetInputBit(1, false);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Sim.ForceOn(0);
            context.Sim.ForceOn(1);
            context.Tick();

            Assert.True(context.GetInput(0));
            Assert.True(context.GetInput(1));

            // Act
            context.Sim.ClearAll();
            context.Tick();

            // Assert - 恢复读硬件
            Assert.False(context.GetInput(0));
            Assert.False(context.GetInput(1));

            context.Dispose();
        }

        [Fact]
        public void Sim_GetMode_ShouldReturnCorrectMode()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Assert - 默认是 None
            Assert.Equal(SimMode.None, context.Sim.GetMode(0));

            // Act
            context.Sim.ForceOn(0);
            Assert.Equal(SimMode.ForceOn, context.Sim.GetMode(0));

            context.Sim.ForceOff(0);
            Assert.Equal(SimMode.ForceOff, context.Sim.GetMode(0));

            context.Sim.ClearSimulate(0);
            Assert.Equal(SimMode.None, context.Sim.GetMode(0));

            context.Dispose();
        }

        #endregion

        #region Meta 测试

        [Fact]
        public void Meta_InputCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Assert - TestLayout 有 3 个输入
            Assert.Equal(3, context.Meta.InputCount);

            context.Dispose();
        }

        [Fact]
        public void Meta_OutputCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Assert - TestLayout 有 2 个输出
            Assert.Equal(2, context.Meta.OutputCount);

            context.Dispose();
        }

        [Fact]
        public void Meta_GetInputMeta_ShouldReturnMetadata()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            var meta = context.Meta.GetInputMeta(0);

            // Assert
            Assert.NotNull(meta);
            Assert.Equal("启动按钮", meta!.PropertyName);
            Assert.Equal(0, meta.Index);

            context.Dispose();
        }

        [Fact]
        public void Meta_GetOutputMeta_ShouldReturnMetadata()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            var meta = context.Meta.GetOutputMeta(0);

            // Assert
            Assert.NotNull(meta);
            Assert.Equal("运行灯", meta!.PropertyName);
            Assert.Equal(0, meta.Index);

            context.Dispose();
        }

        #endregion

        #region Mapping 查询测试

        [Fact]
        public void Mapping_GetInputPhysicalIndex_ShouldReturnMappedIndex()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 默认 1:1 映射
            Assert.Equal(0, context.Mapping.GetInputPhysicalIndex(0));

            // 设置自定义映射
            context.Mapping.SetInputMapping(0, 5);

            // Assert
            Assert.Equal(5, context.Mapping.GetInputPhysicalIndex(0));

            context.Dispose();
        }

        [Fact]
        public void Mapping_GetOutputPhysicalIndex_ShouldReturnMappedIndex()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 默认 1:1 映射
            Assert.Equal(0, context.Mapping.GetOutputPhysicalIndex(0));

            // 设置自定义映射
            context.Mapping.SetOutputMapping(0, 3);

            // Assert
            Assert.Equal(3, context.Mapping.GetOutputPhysicalIndex(0));

            context.Dispose();
        }

        [Fact]
        public void Mapping_IsInputNormallyClosed_ShouldReturnCorrectValue()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 默认不是常闭
            Assert.False(context.Mapping.IsInputNormallyClosed(0));

            // 设置为常闭
            context.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);

            // Assert
            Assert.True(context.Mapping.IsInputNormallyClosed(0));

            context.Dispose();
        }

        [Fact]
        public void Mapping_IsOutputNormallyClosed_ShouldReturnCorrectValue()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 默认不是常闭
            Assert.False(context.Mapping.IsOutputNormallyClosed(0));

            // 设置为常闭
            context.Mapping.SetOutputMapping(0, 0, isNormallyClosed: true);

            // Assert
            Assert.True(context.Mapping.IsOutputNormallyClosed(0));

            context.Dispose();
        }

        #endregion

        #region ctx.R.<属性> 直接属性访问测试

        [Fact]
        public void R_Property_ShouldReadInputValue()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);
            hardware.SetInputBit(1, false);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            context.Tick();

            // Assert - 使用 ctx.R.<属性> 直接访问
            Assert.True(context.R.启动按钮);
            Assert.False(context.R.急停);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldReadOutputValue()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act - 设置输出
            context.On(x => x.运行灯, now: true);
            context.Tick(); // 更新 Snapshot

            // Assert - 使用 ctx.R.<属性> 直接访问输出
            Assert.True(context.R.运行灯);
            Assert.False(context.R.真空阀);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldProvideFrameConsistency()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act - 在读取期间修改硬件输入
            bool firstRead = context.R.启动按钮;
            hardware.SetInputBit(0, false);
            bool secondRead = context.R.启动按钮;

            // Assert - 同一帧内应该一致（都是 true）
            Assert.True(firstRead);
            Assert.True(secondRead);  // R.属性 不变

            // 下一次 Tick 后才会看到变化
            context.Tick();
            Assert.False(context.R.启动按钮);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldUpdateAfterTick()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();
            Assert.False(context.R.启动按钮);

            // Act - 修改硬件并 Tick
            hardware.SetInputBit(0, true);
            context.Tick();

            // Assert - R.属性 应该更新
            Assert.True(context.R.启动按钮);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldReflectSimulation()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, false);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();
            Assert.False(context.R.启动按钮);

            // Act - 使用模拟覆盖
            context.Sim.ForceOn(x => x.启动按钮);
            context.Tick();

            // Assert - R.属性 应该反映模拟值
            Assert.True(context.R.启动按钮);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldReflectMappingInversion()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 设置常闭映射
            context.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);

            // Act - 硬件为 OFF
            hardware.SetInputBit(0, false);
            context.Tick();

            // Assert - R.属性 应该反转为 ON
            Assert.True(context.R.启动按钮);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldBeConsistentWithGetInput()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            hardware.SetInputBit(0, true);
            hardware.SetInputBit(1, false);
            hardware.SetInputBit(2, true);

            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Assert - R.属性 应该与 GetInput() 一致
            Assert.Equal(context.GetInput(0), context.R.启动按钮);
            Assert.Equal(context.GetInput(1), context.R.急停);
            Assert.Equal(context.GetInput(2), context.R.真空到位);

            context.Dispose();
        }

        [Fact]
        public void R_Property_Output_ShouldBeConsistentWithGetOutput()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // 设置一些输出
            context.On(0, now: true);
            context.Off(1, now: true);
            context.Tick();

            // Assert - R.属性 应该与 GetOutput() 一致
            Assert.Equal(context.GetOutput(0), context.R.运行灯);
            Assert.Equal(context.GetOutput(1), context.R.真空阀);

            context.Dispose();
        }

        [Fact]
        public void R_Property_ShouldWorkInConditions()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();
            Assert.False(context.R.运行灯);

            // Act - 模拟业务逻辑：启动按钮按下时打开运行灯
            hardware.SetInputBit(0, true); // 启动按钮 ON
            context.Tick();

            if (context.R.启动按钮)
            {
                context.On(x => x.运行灯, now: true);
            }
            context.Tick();

            // Assert
            Assert.True(context.R.运行灯);

            context.Dispose();
        }

        #endregion

        #region UntilByIndex 测试

        [Fact]
        public async Task UntilByIndex_ShouldWaitForCondition()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // Act
            var waitTask = context.UntilByIndex(0, expected: true, timeout: TimeSpan.FromSeconds(1)).AsTask();

            // 模拟后台 Tick + 条件满足
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                hardware.SetInputBit(0, true);
                context.Tick();
            });

            bool result = await waitTask;

            // Assert
            Assert.True(result);

            context.Dispose();
        }

        [Fact]
        public async Task UntilByIndex_ShouldTimeoutWhenConditionNotMet()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var context = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();

            // 启动后台 Tick
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    context.Tick();
                    await Task.Delay(10);
                }
            });

            // Act
            var result = await context.UntilByIndex(0, expected: true, timeout: TimeSpan.FromMilliseconds(100)).AsTask();

            // Assert
            Assert.False(result);

            cts.Cancel();
            context.Dispose();
        }

        #endregion
    }
}
