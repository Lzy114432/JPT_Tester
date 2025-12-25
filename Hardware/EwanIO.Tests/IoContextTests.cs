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
            context.Pulse(x => x.运行灯, durationTicks: 1, now: false);

            // Assert - 在 Tick 前不应该写到硬件
            Assert.False(hardware.ReadOutBit(0));

            context.Tick();
            Assert.True(hardware.ReadOutBit(0));

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
            context.Pulse(x => x.运行灯, durationTicks: 1, now: true, value: false);

            // Assert - 先 OFF，再恢复 ON
            Assert.False(hardware.ReadOutBit(0));

            context.Tick();
            Assert.False(hardware.ReadOutBit(0));

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

            // Act
            context.Pulse(x => x.运行灯, durationTicks: 2, now: true);
            context.Pulse(x => x.运行灯, durationTicks: 5, now: true);

            // Assert - 只按首次脉冲结束
            Assert.True(hardware.ReadOutBit(0));

            context.Tick();
            Assert.True(hardware.ReadOutBit(0));

            context.Tick();
            Assert.True(hardware.ReadOutBit(0));

            context.Tick();
            Assert.False(hardware.ReadOutBit(0));

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
