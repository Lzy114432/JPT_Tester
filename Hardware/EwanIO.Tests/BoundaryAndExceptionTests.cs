using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Core.Data;
using EwanIO.Core.Interfaces;
using EwanIO.Core.Mapping;
using EwanIO.Core.Simulation;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    /// <summary>
    /// 异常处理、边界条件和特殊场景测试
    ///
    /// 测试目的：
    /// 验证系统在边界条件、异常输入和特殊场景下的健壮性。
    /// 确保系统不会因为无效输入而崩溃，同时验证并发访问的安全性。
    ///
    /// 主要测试内容：
    /// 1. IoContext Dispose - 释放后的操作应该安全处理
    /// 2. 索引边界 - 无效索引应该返回默认值而不是抛出异常
    /// 3. 各组件边界 - Command、Snapshot、MappingCache、SimManager 的边界处理
    /// 4. IoContextBuilder - 构建器的参数验证
    /// 5. 并发访问 - 多线程同时操作的安全性
    /// 6. 特殊场景 - 零 IO、大量 IO、快速 Tick 循环
    /// </summary>
    public class BoundaryAndExceptionTests
    {
        #region 测试 Layout - 用于测试的 IO 布局定义

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

        public class LargeLayout
        {
            [IO(0)]
            public InputSignal Input0 { get; set; }

            [IO(63)]
            public InputSignal Input63 { get; set; }

            [IO(0)]
            public OutputSignal Output0 { get; set; }

            [IO(63)]
            public OutputSignal Output63 { get; set; }
        }

        #endregion

        #region IoContext Dispose 测试 - 验证资源释放后的安全行为

        [Fact]
        public void IoContext_AfterDispose_TickShouldNotThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Dispose();

            // Act & Assert - Tick 不应抛异常，只是静默返回
            var ex = Record.Exception(() => ctx.Tick());
            Assert.Null(ex);
        }

        [Fact]
        public void IoContext_AfterDispose_OnShouldNotThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Dispose();

            // Act & Assert
            var ex = Record.Exception(() => ctx.On(0, now: true));
            Assert.Null(ex);
        }

        [Fact]
        public void IoContext_AfterDispose_FlushShouldNotThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Dispose();

            // Act & Assert
            var ex = Record.Exception(() => ctx.Flush());
            Assert.Null(ex);
        }

        [Fact]
        public void IoContext_DisposeMultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act & Assert
            ctx.Dispose();
            var ex = Record.Exception(() => ctx.Dispose());
            Assert.Null(ex);

            ex = Record.Exception(() => ctx.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void IoContext_Dispose_ShouldCancelPendingWaitOperations()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Tick();

            // 创建一个等待操作
            var op = ctx.Until(x => x.Input0, expected: true, timeout: TimeSpan.FromSeconds(10));

            // Act
            ctx.Dispose();

            // Assert - 等待操作应该被取消或完成
            // 给一点时间让取消操作完成
            Thread.Sleep(50);
            Assert.True(op.IsCompleted);
        }

        #endregion

        #region 索引边界测试 - 验证无效索引的安全处理

        [Fact]
        public void GetInput_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Tick();

            // Act & Assert
            Assert.False(ctx.GetInput(-1));
            Assert.False(ctx.GetInput(100));
        }

        [Fact]
        public void GetOutput_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Tick();

            // Act & Assert
            Assert.False(ctx.GetOutput(-1));
            Assert.False(ctx.GetOutput(100));

            ctx.Dispose();
        }

        [Fact]
        public void GetInput_WithInvalidIndex_ShouldRecordHealthError()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            string? lastEventType = null;
            string? lastMessage = null;
            ctx.HealthChanged += (sender, args) =>
            {
                lastEventType = args.EventType;
                lastMessage = args.Message;
            };

            // Act
            bool value = ctx.GetInput(-1);

            // Assert
            Assert.False(value);
            Assert.NotNull(ctx.Health.LastError);
            Assert.Equal("IndexOutOfRange", lastEventType);
            Assert.NotNull(lastMessage);
            Assert.Contains("Input index", lastMessage);

            ctx.Dispose();
        }

        [Fact]
        public void GetOutput_WithInvalidIndex_StrictMode_ShouldThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .WithIndexOutOfRangeBehavior(IndexOutOfRangeBehavior.Throw)
                .BuildAndConnect("test");

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => ctx.GetOutput(100));

            ctx.Dispose();
        }

        [Fact]
        public void On_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act & Assert - 设置无效索引不应抛异常
            var ex = Record.Exception(() => ctx.On(-1, now: true));
            Assert.Null(ex);

            ex = Record.Exception(() => ctx.Off(100, now: true));
            Assert.Null(ex);

            ctx.Dispose();
        }

        #endregion

        #region Command 边界测试 - 验证输出命令缓冲区的边界处理

        [Fact]
        public void Command_SetOutput_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var cmd = new Command(10);

            // Act & Assert
            var ex = Record.Exception(() => cmd.SetOutput(-1, true));
            Assert.Null(ex);

            ex = Record.Exception(() => cmd.SetOutput(10, true));
            Assert.Null(ex);

            ex = Record.Exception(() => cmd.SetOutput(100, true));
            Assert.Null(ex);
        }

        [Fact]
        public void Command_GetOutput_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var cmd = new Command(10);

            // Act & Assert
            Assert.False(cmd.GetOutput(-1));
            Assert.False(cmd.GetOutput(10));
            Assert.False(cmd.GetOutput(100));
        }

        [Fact]
        public void CommandOptimized_SetOutput_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var cmd = new CommandOptimized(64);

            // Act & Assert
            var ex = Record.Exception(() => cmd.SetOutput(-1, true));
            Assert.Null(ex);

            ex = Record.Exception(() => cmd.SetOutput(64, true));
            Assert.Null(ex);

            ex = Record.Exception(() => cmd.SetOutput(100, true));
            Assert.Null(ex);
        }

        [Fact]
        public void CommandOptimized_GetOutput_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var cmd = new CommandOptimized(64);

            // Act & Assert
            Assert.False(cmd.GetOutput(-1));
            Assert.False(cmd.GetOutput(64));
            Assert.False(cmd.GetOutput(100));
        }

        #endregion

        #region Snapshot 边界测试 - 验证输入输出快照的边界处理

        [Fact]
        public void Snapshot_GetInput_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var snapshot = new Snapshot(10, 10);

            // Act & Assert
            Assert.False(snapshot.GetInput(-1));
            Assert.False(snapshot.GetInput(10));
            Assert.False(snapshot.GetInput(100));
        }

        [Fact]
        public void Snapshot_GetOutput_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var snapshot = new Snapshot(10, 10);

            // Act & Assert
            Assert.False(snapshot.GetOutput(-1));
            Assert.False(snapshot.GetOutput(10));
            Assert.False(snapshot.GetOutput(100));
        }

        [Fact]
        public void Snapshot_SetInput_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var snapshot = new Snapshot(10, 10);

            // Act & Assert
            var ex = Record.Exception(() => snapshot.SetInput(-1, true));
            Assert.Null(ex);

            ex = Record.Exception(() => snapshot.SetInput(10, true));
            Assert.Null(ex);
        }

        [Fact]
        public void Snapshot_SetOutput_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var snapshot = new Snapshot(10, 10);

            // Act & Assert
            var ex = Record.Exception(() => snapshot.SetOutput(-1, true));
            Assert.Null(ex);

            ex = Record.Exception(() => snapshot.SetOutput(10, true));
            Assert.Null(ex);
        }

        #endregion

        #region MappingCache 边界测试 - 验证逻辑/物理映射的边界处理

        [Fact]
        public void MappingCache_GetInputPhysicalIndex_WithInvalidIndex_ShouldReturnSameIndex()
        {
            // Arrange
            var cache = new MappingCache(10, 10);

            // Act & Assert - 无效索引返回原值
            Assert.Equal(-1, cache.GetInputPhysicalIndex(-1));
            Assert.Equal(10, cache.GetInputPhysicalIndex(10));
        }

        [Fact]
        public void MappingCache_SetInputMapping_WithInvalidIndex_ShouldThrow()
        {
            // Arrange
            var cache = new MappingCache(10, 10);

            // Act & Assert - MappingCache 对无效索引会抛出异常
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.SetInputMapping(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.SetInputMapping(10, 0));
        }

        [Fact]
        public void MappingCache_IsInputNormallyClosed_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var cache = new MappingCache(10, 10);

            // Act & Assert
            Assert.False(cache.IsInputNormallyClosed(-1));
            Assert.False(cache.IsInputNormallyClosed(10));
        }

        #endregion

        #region SimManager 边界测试 - 验证模拟管理器的边界处理

        [Fact]
        public void SimManager_ForceOn_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var sim = new SimManager(10);

            // Act & Assert
            var ex = Record.Exception(() => sim.ForceOn(-1));
            Assert.Null(ex);

            ex = Record.Exception(() => sim.ForceOn(10));
            Assert.Null(ex);
        }

        [Fact]
        public void SimManager_ForceOff_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var sim = new SimManager(10);

            // Act & Assert
            var ex = Record.Exception(() => sim.ForceOff(-1));
            Assert.Null(ex);

            ex = Record.Exception(() => sim.ForceOff(10));
            Assert.Null(ex);
        }

        [Fact]
        public void SimManager_GetMode_WithInvalidIndex_ShouldReturnNone()
        {
            // Arrange
            var sim = new SimManager(10);

            // Act & Assert
            Assert.Equal(SimMode.None, sim.GetMode(-1));
            Assert.Equal(SimMode.None, sim.GetMode(10));
        }

        [Fact]
        public void SimManager_ApplySimulate_WithInvalidIndex_ShouldReturnOriginalValue()
        {
            // Arrange
            var sim = new SimManager(10);

            // Act & Assert
            Assert.True(sim.ApplySimulate(-1, true));
            Assert.False(sim.ApplySimulate(-1, false));
            Assert.True(sim.ApplySimulate(10, true));
            Assert.False(sim.ApplySimulate(10, false));
        }

        #endregion

        #region IoContextBuilder 边界测试 - 验证构建器的参数校验

        [Fact]
        public void IoContextBuilder_WithNullHardware_ShouldThrow()
        {
            // Act & Assert - 传入 null 会导致 Build 时抛出 InvalidOperationException
            // 因为 WithHardware(null) 不会真正设置硬件
            Assert.Throws<InvalidOperationException>(() =>
                IoContextBuilder.For<TestLayout>()
                    .WithHardware((IHardwareIO)null)
                    .Build());
        }

        [Fact]
        public void IoContextBuilder_WithoutHardware_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                IoContextBuilder.For<TestLayout>()
                    .Build());
        }

        [Fact]
        public void IoContextBuilder_WithNegativeTimeout_ShouldHandle()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);

            // Act - 负数超时应该被处理
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .WithConfirmTimeout(TimeSpan.FromMilliseconds(-100))
                .BuildAndConnect("test");

            // Assert - 应该能正常创建
            Assert.NotNull(ctx);

            ctx.Dispose();
        }

        #endregion

        #region InMemoryHardwareIO 边界测试 - 验证内存硬件的边界处理

        [Fact]
        public void InMemoryHardwareIO_ReadInBit_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var hw = new InMemoryHardwareIO(10, 10);
            hw.Connect("test");

            // Act & Assert
            Assert.False(hw.ReadInBit(-1));
            Assert.False(hw.ReadInBit(10));
            Assert.False(hw.ReadInBit(100));

            hw.Dispose();
        }

        [Fact]
        public void InMemoryHardwareIO_WriteOutBit_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var hw = new InMemoryHardwareIO(10, 10);
            hw.Connect("test");

            // Act & Assert
            Assert.False(hw.WriteOutBit(-1, true));
            Assert.False(hw.WriteOutBit(10, true));
            Assert.False(hw.WriteOutBit(100, true));

            hw.Dispose();
        }

        [Fact]
        public void InMemoryHardwareIO_SetInputBit_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var hw = new InMemoryHardwareIO(10, 10);
            hw.Connect("test");

            // Act & Assert
            var ex = Record.Exception(() => hw.SetInputBit(-1, true));
            Assert.Null(ex);

            ex = Record.Exception(() => hw.SetInputBit(10, true));
            Assert.Null(ex);

            hw.Dispose();
        }

        [Fact]
        public void InMemoryHardwareIO_OperationsWhenNotConnected_ShouldStillWork()
        {
            // Arrange
            var hw = new InMemoryHardwareIO(10, 10);
            // 不连接

            // Act & Assert - InMemoryHardwareIO 允许在未连接时读写
            // 这是设计上的简化，便于测试
            hw.SetInputBit(0, true);
            Assert.True(hw.ReadInBit(0));

            Assert.True(hw.WriteOutBit(0, true));
            Assert.True(hw.ReadOutBit(0));

            hw.Dispose();
        }

        [Fact]
        public void InMemoryHardwareIO_Disconnect_WhenNotConnected_ShouldReturnTrue()
        {
            // Arrange
            var hw = new InMemoryHardwareIO(10, 10);

            // Act & Assert
            Assert.True(hw.Disconnect());

            hw.Dispose();
        }

        #endregion

        #region 并发访问测试 - 验证多线程操作的安全性

        [Fact]
        public void IoContext_ConcurrentTickAndWrite_ShouldNotCorruptState()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(64, 64);
            var ctx = IoContextBuilder.For<LargeLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var cts = new CancellationTokenSource();
            int tickCount = 0;
            int writeCount = 0;
            Exception caughtException = null;

            // Act - 一个线程 Tick，多个线程写入
            var tickTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        ctx.Tick();
                        Interlocked.Increment(ref tickCount);
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            var writeTasks = new Task[4];
            for (int t = 0; t < 4; t++)
            {
                int threadId = t;
                writeTasks[t] = Task.Run(() =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            ctx.On(threadId, now: false);
                            ctx.Off(threadId, now: false);
                            Interlocked.Increment(ref writeCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                    }
                });
            }

            // 运行 200ms
            Thread.Sleep(200);
            cts.Cancel();

            Task.WaitAll(new[] { tickTask }.Concat(writeTasks).ToArray());

            // Assert - 应该没有异常发生，且完成了操作
            Assert.Null(caughtException);
            Assert.True(tickCount > 0, "Tick 应该执行了至少一次");
            Assert.True(writeCount > 0, "Write 应该执行了至少一次");

            ctx.Dispose();
        }

        [Fact]
        public void IoContext_ConcurrentEdgeRead_ShouldBeThreadSafe()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            ctx.Tick();

            // 产生上升沿
            hardware.SetInputBit(0, true);
            ctx.Tick();

            int successCount = 0;
            var barrier = new Barrier(10);

            // Act - 多线程同时读取边沿
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    if (ctx.Edge.R(0))
                    {
                        Interlocked.Increment(ref successCount);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert - 只有一个线程能成功读取边沿
            Assert.Equal(1, successCount);

            ctx.Dispose();
        }

        #endregion

        #region 特殊场景测试 - 验证极端和边界场景

        [Fact]
        public void IoContext_ZeroInputOutput_ShouldWork()
        {
            // Arrange - 0 输入输出的硬件
            var hardware = new InMemoryHardwareIO(0, 0);

            // Act & Assert - 应该能正常创建和操作
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            var ex = Record.Exception(() => ctx.Tick());
            Assert.Null(ex);

            ctx.Dispose();
        }

        [Fact]
        public void IoContext_LargeInputOutput_ShouldWork()
        {
            // Arrange - 大量输入输出
            var hardware = new InMemoryHardwareIO(256, 256);

            var ctx = IoContextBuilder.For<LargeLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act
            for (int i = 0; i < 256; i++)
            {
                hardware.SetInputBit(i, i % 2 == 0);
            }

            ctx.Tick();

            for (int i = 0; i < 256; i++)
            {
                if (i % 3 == 0)
                {
                    ctx.On(i, now: false);
                }
                else
                {
                    ctx.Off(i, now: false);
                }
            }

            ctx.Flush();

            // Assert
            Assert.True(ctx.GetInput(0));
            Assert.False(ctx.GetInput(1));
            Assert.True(hardware.ReadOutBit(0));
            Assert.False(hardware.ReadOutBit(1));

            ctx.Dispose();
        }

        [Fact]
        public void IoContext_RapidTickCycles_ShouldNotLeak()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(10, 10);
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            // Act - 快速 Tick 循环
            for (int i = 0; i < 10000; i++)
            {
                hardware.SetInputBit(0, i % 2 == 0);
                ctx.Tick();
                if (i % 3 == 0)
                {
                    ctx.On(0, now: true);
                }
                else
                {
                    ctx.Off(0, now: true);
                }
            }

            // Assert
            Assert.Equal(10000, ctx.TickCounter);
            Assert.Equal(10000, ctx.Health.TotalTicks);

            ctx.Dispose();
        }

        #endregion

        #region MappingConfigManager 边界测试 - 验证映射配置管理器的边界处理

        [Fact]
        public void MappingConfigManager_Load_WithInvalidPath_ShouldThrow()
        {
            // Act & Assert
            Assert.ThrowsAny<Exception>(() =>
                MappingConfigManager.Load("C:\\NonExistent\\Path\\file.json"));
        }

        [Fact]
        public void MappingConfigManager_GenerateDefault_WithZeroCounts_ShouldWork()
        {
            // Act
            var config = MappingConfigManager.GenerateDefault(0, 0, "Empty config");

            // Assert
            Assert.NotNull(config);
            Assert.Empty(config.Inputs);
            Assert.Empty(config.Outputs);
        }

        #endregion
    }
}
