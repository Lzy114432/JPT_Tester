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
    public class BulkWriteOptimizationTests
    {
        #region Test Layout

        public class TestLayout
        {
            [IO(0)]
            public OutputSignal Out0 { get; set; }

            [IO(1)]
            public OutputSignal Out1 { get; set; }

            [IO(2)]
            public OutputSignal Out2 { get; set; }

            [IO(31)]
            public OutputSignal Out31 { get; set; }

            [IO(32)]
            public OutputSignal Out32 { get; set; }

            [IO(63)]
            public OutputSignal Out63 { get; set; }
        }

        #endregion

        #region CommandOptimized Tests

        [Fact]
        public void CommandOptimized_SetOutput_ShouldMarkPortDirty()
        {
            // Arrange
            var cmd = new CommandOptimized(64);

            // Act - 设置第 0 个输出
            cmd.SetOutput(0, true);

            // Assert - 端口 0 应该被标记为 dirty
            Assert.True(cmd.HasDirty);
            Assert.Equal(1, cmd.DirtyPortCount);
            Assert.True(cmd.IsPortDirty(0));
            Assert.False(cmd.IsPortDirty(1));
        }

        [Fact]
        public void CommandOptimized_SetMultipleOutputsInSamePort_ShouldMarkOnlyOnePort()
        {
            // Arrange
            var cmd = new CommandOptimized(64);

            // Act - 设置同一个端口（0-31）内的多个输出
            cmd.SetOutput(0, true);
            cmd.SetOutput(5, true);
            cmd.SetOutput(31, false);

            // Assert - 只有一个端口被标记为 dirty
            Assert.Equal(1, cmd.DirtyPortCount);
            Assert.True(cmd.IsPortDirty(0));
        }

        [Fact]
        public void CommandOptimized_SetOutputsInDifferentPorts_ShouldMarkMultiplePorts()
        {
            // Arrange
            var cmd = new CommandOptimized(64);

            // Act - 设置不同端口的输出
            cmd.SetOutput(0, true);   // 端口 0
            cmd.SetOutput(32, true);  // 端口 1

            // Assert - 两个端口都应该被标记为 dirty
            Assert.Equal(2, cmd.DirtyPortCount);
            Assert.True(cmd.IsPortDirty(0));
            Assert.True(cmd.IsPortDirty(1));
        }

        [Fact]
        public void CommandOptimized_FlushDirtyPorts_ShouldWriteAllDirtyPorts()
        {
            // Arrange
            var cmd = new CommandOptimized(64);
            cmd.SetOutput(0, true);
            cmd.SetOutput(5, true);
            cmd.SetOutput(32, false);
            cmd.SetOutput(35, true);

            int callCount = 0;
            var portValues = new System.Collections.Generic.Dictionary<int, uint>();

            // Act
            cmd.FlushDirtyPorts((portIndex, portValue) =>
            {
                callCount++;
                portValues[portIndex] = portValue;
            });

            // Assert - 应该写入两个端口
            Assert.Equal(2, callCount);
            Assert.True(portValues.ContainsKey(0));
            Assert.True(portValues.ContainsKey(1));

            // 验证端口 0 的值（bit 0 和 bit 5 应该为 1）
            uint expectedPort0 = (1U << 0) | (1U << 5);
            Assert.Equal(expectedPort0, portValues[0]);

            // 验证端口 1 的值（bit 3 应该为 1，bit 35-32=3）
            uint expectedPort1 = (1U << 3);
            Assert.Equal(expectedPort1, portValues[1]);

            // Flush 后 dirty 应该被清除
            Assert.False(cmd.HasDirty);
        }

        [Fact]
        public void CommandOptimized_UnpackPort_ShouldRestoreValues()
        {
            // Arrange
            var cmd = new CommandOptimized(64);
            uint portValue = (1U << 0) | (1U << 5) | (1U << 15);

            // Act
            cmd.UnpackPort(0, portValue);

            // Assert
            Assert.True(cmd.GetOutput(0));
            Assert.False(cmd.GetOutput(1));
            Assert.True(cmd.GetOutput(5));
            Assert.False(cmd.GetOutput(6));
            Assert.True(cmd.GetOutput(15));
            Assert.False(cmd.GetOutput(16));
        }

        [Fact]
        public void CommandOptimized_GetDirtyPorts_ShouldReturnAllDirtyPortInfo()
        {
            // Arrange
            var cmd = new CommandOptimized(96);
            cmd.SetOutput(0, true);
            cmd.SetOutput(32, true);
            cmd.SetOutput(64, true);

            // Act
            var dirtyPorts = cmd.GetDirtyPorts();

            // Assert
            Assert.Equal(3, dirtyPorts.Count);
        }

        #endregion

        #region IoContext Bulk Write Integration Tests

        [Fact]
        public void IoContext_WithBulkWriteHardware_ShouldUseBulkWrite()
        {
            // Arrange
            var hardware = new MockBulkWriteHardware(64, 64);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act - 写多个输出
            ctx.On(0, now: true);
            ctx.On(5, now: true);
            ctx.On(32, now: true);

            // Assert - 应该调用批量写入
            Assert.True(hardware.BulkWriteCalled);
            Assert.True(hardware.BulkWriteCallCount >= 1);
        }

        [Fact]
        public void IoContext_BulkWriteVsBitWrite_ShouldReduceCallCount()
        {
            // Arrange - 使用批量写入硬件
            var bulkHw = new MockBulkWriteHardware(64, 64);
            bulkHw.Connect("test");

            var bulkCtx = IoContextBuilder.For<TestLayout>()
                .WithId("BulkCtx")
                .WithHardware(bulkHw)
                .Build();

            // Arrange - 使用逐位写入硬件
            var bitHw = new MockBasicHardware(64, 64);
            bitHw.Connect("test");

            var bitCtx = IoContextBuilder.For<TestLayout>()
                .WithId("BitCtx")
                .WithHardware(bitHw)
                .Build();

            // Act - 在同一个端口内设置多个输出
            for (int i = 0; i < 10; i++)
            {
                bulkCtx.On(i, now: false);
                bitCtx.On(i, now: false);
            }

            bulkCtx.Flush();
            bitCtx.Flush();

            // Assert - 批量写入应该只调用 1 次，逐位写入调用 10 次
            Assert.Equal(1, bulkHw.BulkWriteCallCount);
            Assert.Equal(10, bitHw.BitWriteCallCount);
        }

        [Fact]
        public void IoContext_BulkWrite_AcrossMultiplePorts_ShouldWriteAllPorts()
        {
            // Arrange
            var hardware = new MockBulkWriteHardware(96, 96);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act - 跨越 3 个端口设置输出
            ctx.On(0, now: false);   // Port 0
            ctx.On(32, now: false);  // Port 1
            ctx.On(64, now: false);  // Port 2
            ctx.Flush();

            // Assert - 应该写入 3 个端口
            Assert.Equal(3, hardware.BulkWriteCallCount);
            Assert.Contains(0, hardware.PortsWritten);
            Assert.Contains(1, hardware.PortsWritten);
            Assert.Contains(2, hardware.PortsWritten);
        }

        [Fact]
        public void IoContext_BulkWrite_ShouldProduceCorrectHardwareOutput()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(64, 64);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act - 设置多个输出
            ctx.On(0, now: true);
            ctx.On(5, now: true);
            ctx.Off(10, now: true);

            // Assert - 硬件输出应该正确
            Assert.True(hardware.ReadOutBit(0));
            Assert.True(hardware.ReadOutBit(5));
            Assert.False(hardware.ReadOutBit(10));
        }

        #endregion

        #region Mock Hardware

        /// <summary>
        /// Mock 硬件 - 支持批量写入
        /// </summary>
        private class MockBulkWriteHardware : InMemoryHardwareIO, IHardwareIOExtended
        {
            public int BulkWriteCallCount { get; private set; }
            public bool BulkWriteCalled => BulkWriteCallCount > 0;
            public System.Collections.Generic.HashSet<int> PortsWritten { get; } =
                new System.Collections.Generic.HashSet<int>();

            HardwareCapabilities IHardwareIOExtended.Capabilities =>
                HardwareCapabilities.FullySeparatedSync | HardwareCapabilities.BulkOutputWrite;

            public MockBulkWriteHardware(int inputCount, int outputCount)
                : base(inputCount, outputCount)
            {
            }

            void IHardwareIOExtended.InputSync()
            {
            }

            void IHardwareIOExtended.OutputSync()
            {
            }

            void IHardwareIOExtended.WriteBulkOutputPort(int portIndex, uint portValue)
            {
                BulkWriteCallCount++;
                PortsWritten.Add(portIndex);

                // 直接调用基类的 WriteBulkOutputPort 实现
                base.WriteBulkOutputPort(portIndex, portValue);
            }
        }

        /// <summary>
        /// Mock 基础硬件 - 不支持批量写入
        /// </summary>
        private class MockBasicHardware : IHardwareIO
        {
            private readonly bool[] _outputs;
            public int BitWriteCallCount { get; private set; }

            public string HardwareType => "MockBasic";
            public string ConnectionInfo { get; private set; } = "Mock";
            public bool IsConnected { get; private set; }
            public int InputCount { get; }
            public int OutputCount { get; }

            public MockBasicHardware(int inputCount, int outputCount)
            {
                InputCount = inputCount;
                OutputCount = outputCount;
                _outputs = new bool[outputCount];
            }

            public bool Connect(string connectionString)
            {
                ConnectionInfo = connectionString;
                IsConnected = true;
                return true;
            }

            public bool Disconnect()
            {
                IsConnected = false;
                return true;
            }

            public void DataSync() { }

            public bool ReadInBit(int bit) => false;

            public bool ReadOutBit(int bit)
            {
                return bit >= 0 && bit < OutputCount && _outputs[bit];
            }

            public bool WriteOutBit(int bit, bool value)
            {
                if (bit >= 0 && bit < OutputCount)
                {
                    _outputs[bit] = value;
                    BitWriteCallCount++;
                    return true;
                }
                return false;
            }

            public void Dispose()
            {
                Disconnect();
            }
        }

        #endregion
    }
}
