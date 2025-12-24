using System;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Core.Data;
using EwanIO.Core.EdgeDetection;
using EwanIO.Core.Mapping;
using EwanIO.Core.Metadata;
using EwanIO.Core.Simulation;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    public class HardwareBackendTests
    {
        #region Test Layout

        public class TestLayout
        {
            [IO(0)]
            public InputSignal Input0 { get; set; }

            [IO(1)]
            public InputSignal Input1 { get; set; }

            [IO(0)]
            public OutputSignal Output0 { get; set; }

            [IO(1)]
            public OutputSignal Output1 { get; set; }
        }

        #endregion

        #region Hardware Capabilities Tests

        [Fact]
        public void InMemoryHardware_ShouldReportFullySeparatedSyncCapability()
        {
            // Arrange & Act
            var hardware = new InMemoryHardwareIO(4, 4);

            // Assert
            Assert.True(hardware is IHardwareIOExtended);
            var extended = hardware as IHardwareIOExtended;
            Assert.NotNull(extended);
            Assert.True(extended.Capabilities.HasInputSync());
            Assert.True(extended.Capabilities.HasOutputSync());
            Assert.True(extended.Capabilities.HasFullySeparatedSync());
        }

        [Fact]
        public void InMemoryHardware_ShouldSupportInputSync()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(4, 4);
            hardware.Connect("test");
            hardware.SetInputBit(0, true);

            // Act
            (hardware as IHardwareIOExtended)?.InputSync();

            // Assert - 应该能正常调用（即使是空操作）
            Assert.True(hardware.ReadInBit(0));
        }

        [Fact]
        public void InMemoryHardware_ShouldSupportOutputSync()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(4, 4);
            hardware.Connect("test");
            hardware.WriteOutBit(0, true);

            // Act
            (hardware as IHardwareIOExtended)?.OutputSync();

            // Assert - 应该能正常调用（即使是空操作）
            Assert.True(hardware.ReadOutBit(0));
        }

        #endregion

        #region IoContext Integration Tests

        [Fact]
        public void IoContext_WithExtendedHardware_ShouldUseInputSyncInTick()
        {
            // Arrange
            var hardware = new MockExtendedHardware(4, 4);
            hardware.Connect("test");
            hardware.SetInputBit(0, true);

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act
            ctx.Tick();

            // Assert - InputSync 应该被调用
            Assert.True(hardware.InputSyncCalled);
            Assert.True(ctx.GetInput(0));
        }

        [Fact]
        public void IoContext_WithExtendedHardware_ShouldUseOutputSyncWhenFlushing()
        {
            // Arrange
            var hardware = new MockExtendedHardware(4, 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act - 写输出并立即下发
            ctx.On(0, now: true);

            // Assert - OutputSync 应该被调用
            Assert.True(hardware.OutputSyncCalled);
            Assert.True(hardware.ReadOutBit(0));
        }

        [Fact]
        public void IoContext_WithBasicHardware_ShouldFallbackToDataSync()
        {
            // Arrange
            var hardware = new MockBasicHardware(4, 4);
            hardware.Connect("test");
            hardware.SetInputBit(0, true);

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act
            ctx.Tick();

            // Assert - DataSync 应该被调用（fallback）
            Assert.True(hardware.DataSyncCalled);
            Assert.False(hardware.ExtendedMethodsCalled);
            Assert.True(ctx.GetInput(0));
        }

        [Fact]
        public void IoContext_MultipleTicksWithExtendedHardware_ShouldUseGranularSync()
        {
            // Arrange
            var hardware = new MockExtendedHardware(4, 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // 预先设置输出为 false，确保第一次设置 true 会产生变化
            ctx.Off(0, now: true);
            hardware.ResetCounters(); // 重置计数

            // Act - 多次 Tick，每次都改变值以确保触发 dirty
            for (int i = 0; i < 5; i++)
            {
                hardware.SetInputBit(0, i % 2 == 0);
                ctx.Tick();

                // 交替设置不同的值，确保每次都 dirty（从 true 开始交替）
                bool outputValue = i % 2 == 0; // i=0:true, i=1:false, i=2:true, ...
                if (outputValue)
                {
                    ctx.On(0, now: true);
                }
                else
                {
                    ctx.Off(0, now: true);
                }
            }

            // Assert - InputSync 应该被调用（每次 Tick）
            Assert.True(hardware.InputSyncCalled);
            Assert.Equal(5, hardware.InputSyncCallCount);

            // OutputSync 应该被调用（每次 SetOutput with now=true）
            Assert.True(hardware.OutputSyncCalled);
            Assert.Equal(5, hardware.OutputSyncCallCount);
        }

        [Fact]
        public void HardwareCapabilities_Flags_ShouldWorkCorrectly()
        {
            // Arrange & Act
            var none = HardwareCapabilities.None;
            var inputOnly = HardwareCapabilities.SeparateInputSync;
            var outputOnly = HardwareCapabilities.SeparateOutputSync;
            var both = HardwareCapabilities.FullySeparatedSync;

            // Assert
            Assert.False(none.HasInputSync());
            Assert.False(none.HasOutputSync());
            Assert.False(none.HasFullySeparatedSync());

            Assert.True(inputOnly.HasInputSync());
            Assert.False(inputOnly.HasOutputSync());
            Assert.False(inputOnly.HasFullySeparatedSync());

            Assert.False(outputOnly.HasInputSync());
            Assert.True(outputOnly.HasOutputSync());
            Assert.False(outputOnly.HasFullySeparatedSync());

            Assert.True(both.HasInputSync());
            Assert.True(both.HasOutputSync());
            Assert.True(both.HasFullySeparatedSync());
        }

        #endregion

        #region Mock Hardware Implementations

        /// <summary>
        /// Mock 扩展硬件 - 用于测试
        /// </summary>
        private class MockExtendedHardware : InMemoryHardwareIO, IHardwareIOExtended
        {
            private int _inputSyncCallCount;
            private int _outputSyncCallCount;

            public int InputSyncCallCount => _inputSyncCallCount;
            public int OutputSyncCallCount => _outputSyncCallCount;
            public bool InputSyncCalled => _inputSyncCallCount > 0;
            public bool OutputSyncCalled => _outputSyncCallCount > 0;

            HardwareCapabilities IHardwareIOExtended.Capabilities => HardwareCapabilities.FullySeparatedSync;

            public MockExtendedHardware(int inputCount, int outputCount)
                : base(inputCount, outputCount)
            {
            }

            public void ResetCounters()
            {
                _inputSyncCallCount = 0;
                _outputSyncCallCount = 0;
            }

            void IHardwareIOExtended.InputSync()
            {
                _inputSyncCallCount++;
            }

            void IHardwareIOExtended.OutputSync()
            {
                _outputSyncCallCount++;
            }
        }

        /// <summary>
        /// Mock 基础硬件 - 仅支持 DataSync
        /// </summary>
        private class MockBasicHardware : IHardwareIO
        {
            private readonly bool[] _inputs;
            private readonly bool[] _outputs;
            private bool _isConnected;

            public int DataSyncCallCount { get; private set; }
            public bool DataSyncCalled => DataSyncCallCount > 0;
            public bool ExtendedMethodsCalled { get; private set; }

            public string HardwareType => "MockBasic";
            public string ConnectionInfo { get; private set; } = "Mock";
            public bool IsConnected => _isConnected;
            public int InputCount { get; }
            public int OutputCount { get; }

            public MockBasicHardware(int inputCount, int outputCount)
            {
                InputCount = inputCount;
                OutputCount = outputCount;
                _inputs = new bool[inputCount];
                _outputs = new bool[outputCount];
            }

            public bool Connect(string connectionString)
            {
                ConnectionInfo = connectionString;
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
                DataSyncCallCount++;
            }

            public bool ReadInBit(int bit)
            {
                return bit >= 0 && bit < InputCount && _inputs[bit];
            }

            public bool ReadOutBit(int bit)
            {
                return bit >= 0 && bit < OutputCount && _outputs[bit];
            }

            public bool WriteOutBit(int bit, bool value)
            {
                if (bit >= 0 && bit < OutputCount)
                {
                    _outputs[bit] = value;
                    return true;
                }
                return false;
            }

            public void SetInputBit(int bit, bool value)
            {
                if (bit >= 0 && bit < InputCount)
                    _inputs[bit] = value;
            }

            public void Dispose()
            {
                Disconnect();
            }
        }

        #endregion
    }
}
