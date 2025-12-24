using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    public class IoHealthAndMappingTests
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

        #region IoHealth Tests

        [Fact]
        public void IoHealth_InitialState_ShouldBeCorrect()
        {
            // Arrange & Act
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Assert
            Assert.True(ctx.Health.IsConnected);
            Assert.Equal(0, ctx.Health.TotalTicks);
            Assert.Null(ctx.Health.LastError);
            Assert.Null(ctx.Health.LastErrorTime);
            Assert.Equal(0, ctx.Health.TimeoutCount);
        }

        [Fact]
        public void IoHealth_AfterTick_ShouldRecordPerformance()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act
            ctx.Tick();
            Thread.Sleep(5); // 确保有时间流逝
            ctx.Tick();
            ctx.Tick();

            // Assert
            Assert.Equal(3, ctx.Health.TotalTicks);
            Assert.True(ctx.Health.LastTickMs >= 0);
            Assert.True(ctx.Health.AverageTickMs >= 0);
            Assert.True(ctx.Health.MaxTickMs >= 0);
        }

        [Fact]
        public void IoHealth_WhenTimeout_ShouldRecordTimeout()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            // 确保输入 0 为 false（不会满足条件）
            hardware.SetInputBit(0, false);

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .WithConfirmTimeout(TimeSpan.FromMilliseconds(50))
                .Build();

            // Act - 等待输入 0 变为 true，但它会一直是 false，所以会超时
            var op = ctx.UntilByIndex(0, expected: true, timeout: TimeSpan.FromMilliseconds(10));

            // 等待超时
            var startTime = DateTime.UtcNow;
            while (!op.IsCompleted && (DateTime.UtcNow - startTime).TotalMilliseconds < 100)
            {
                ctx.Tick();
                Thread.Sleep(5);
            }

            // Assert
            Assert.True(op.IsCompleted);
            bool success = op.TryGetResult(out var result);
            Assert.True(success); // TryGetResult 返回 true（操作已完成）
            Assert.False(result);  // 但结果是 false（超时）
            Assert.True(ctx.Health.TimeoutCount > 0);
        }

        [Fact]
        public void IoHealth_HealthChangedEvent_ShouldFire()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .WithConfirmTimeout(TimeSpan.FromMilliseconds(50))
                .Build();

            string? lastEventType = null;
            string? lastMessage = null;

            ctx.HealthChanged += (sender, args) =>
            {
                lastEventType = args.EventType;
                lastMessage = args.Message;
            };

            // Act - 触发超时事件
            var op = ctx.UntilByIndex(0, expected: true, timeout: TimeSpan.FromMilliseconds(10));

            var startTime = DateTime.UtcNow;
            while (!op.IsCompleted && (DateTime.UtcNow - startTime).TotalMilliseconds < 100)
            {
                ctx.Tick();
                Thread.Sleep(5);
            }

            // Assert
            Assert.Equal("Timeout", lastEventType);
            Assert.Contains("timed out", lastMessage);
        }

        [Fact]
        public void IoHealth_GetHealthReport_ShouldReturnFormattedString()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            ctx.Tick();
            ctx.Tick();

            // Act
            var report = ctx.Health.GetHealthReport();

            // Assert
            Assert.Contains("IoHealth Report", report);
            Assert.Contains("Connected: True", report);
            Assert.Contains("Total Ticks: 2", report);
        }

        [Fact]
        public void IoHealth_ResetStatistics_ShouldClearMetrics()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            ctx.Tick();
            ctx.Tick();
            ctx.Tick();

            // Act
            ctx.Health.ResetStatistics();

            // Assert
            Assert.Equal(0, ctx.Health.TotalTicks);
            Assert.Equal(0, ctx.Health.LastTickMs);
            Assert.Equal(0, ctx.Health.AverageTickMs);
            Assert.Equal(0, ctx.Health.MaxTickMs);
            Assert.Equal(0, ctx.Health.TimeoutCount);
            Assert.Null(ctx.Health.LastError);
            Assert.Null(ctx.Health.LastErrorTime);
        }

        [Fact]
        public void IoHealth_IsHealthy_ShouldReflectStatus()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act & Assert - 初始应该健康
            Assert.True(ctx.Health.IsHealthy);
            Assert.True(ctx.Health.IsConnected);
        }

        [Fact]
        public void IoHealth_SlidingAverage_ShouldUpdateCorrectly()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // Act - 多次 Tick
            for (int i = 0; i < 10; i++)
            {
                ctx.Tick();
                Thread.Sleep(1);
            }

            // Assert
            Assert.Equal(10, ctx.Health.TotalTicks);
            Assert.True(ctx.Health.AverageTickMs >= 0);
            Assert.True(ctx.Health.MaxTickMs >= ctx.Health.AverageTickMs);
        }

        #endregion

        #region Mapping Configuration Tests

        [Fact]
        public void MappingConfig_GenerateDefault_ShouldCreate1to1Mapping()
        {
            // Act
            var config = MappingConfigManager.GenerateDefault(4, 4, "Test mapping");

            // Assert
            Assert.Equal("1.0", config.Version);
            Assert.Equal("Test mapping", config.Description);
            Assert.Equal(4, config.Inputs.Count);
            Assert.Equal(4, config.Outputs.Count);

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(i, config.Inputs[i].LogicalIndex);
                Assert.Equal(i, config.Inputs[i].PhysicalIndex);
                Assert.False(config.Inputs[i].IsNormallyClosed);

                Assert.Equal(i, config.Outputs[i].LogicalIndex);
                Assert.Equal(i, config.Outputs[i].PhysicalIndex);
                Assert.False(config.Outputs[i].IsNormallyClosed);
            }
        }

        [Fact]
        public void MappingConfig_SaveAndLoad_ShouldRoundTrip()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var originalConfig = new MappingConfigFile
                {
                    Version = "1.0",
                    Description = "Test config"
                };

                originalConfig.Inputs.Add(new MappingEntry
                {
                    LogicalIndex = 0,
                    PhysicalIndex = 2,
                    IsNormallyClosed = true,
                    Comment = "Input 0 -> Physical 2 (NC)"
                });

                originalConfig.Outputs.Add(new MappingEntry
                {
                    LogicalIndex = 0,
                    PhysicalIndex = 1,
                    IsNormallyClosed = false,
                    Comment = "Output 0 -> Physical 1 (NO)"
                });

                // Act
                MappingConfigManager.Save(tempFile, originalConfig);
                var loadedConfig = MappingConfigManager.Load(tempFile);

                // Assert
                Assert.Equal(originalConfig.Version, loadedConfig.Version);
                Assert.Equal(originalConfig.Description, loadedConfig.Description);
                Assert.Single(loadedConfig.Inputs);
                Assert.Single(loadedConfig.Outputs);

                Assert.Equal(0, loadedConfig.Inputs[0].LogicalIndex);
                Assert.Equal(2, loadedConfig.Inputs[0].PhysicalIndex);
                Assert.True(loadedConfig.Inputs[0].IsNormallyClosed);
                Assert.Equal("Input 0 -> Physical 2 (NC)", loadedConfig.Inputs[0].Comment);

                Assert.Equal(0, loadedConfig.Outputs[0].LogicalIndex);
                Assert.Equal(1, loadedConfig.Outputs[0].PhysicalIndex);
                Assert.False(loadedConfig.Outputs[0].IsNormallyClosed);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void MappingConfig_ApplyToCache_ShouldUpdateMappings()
        {
            // Arrange
            var cache = new MappingCache(inputCount: 4, outputCount: 4);

            var config = new MappingConfigFile();
            config.Inputs.Add(new MappingEntry
            {
                LogicalIndex = 0,
                PhysicalIndex = 2,
                IsNormallyClosed = true
            });
            config.Outputs.Add(new MappingEntry
            {
                LogicalIndex = 1,
                PhysicalIndex = 3,
                IsNormallyClosed = false
            });

            // Act
            MappingConfigManager.ApplyToCache(config, cache);

            // Assert
            Assert.Equal(2, cache.GetInputPhysicalIndex(0));
            Assert.True(cache.IsInputNormallyClosed(0));

            Assert.Equal(3, cache.GetOutputPhysicalIndex(1));
            Assert.False(cache.IsOutputNormallyClosed(1));
        }

        [Fact]
        public void MappingConfig_ExportFromCache_ShouldReflectCurrentState()
        {
            // Arrange
            var cache = new MappingCache(inputCount: 2, outputCount: 2);
            cache.SetInputMapping(0, 1, isNormallyClosed: true);
            cache.SetOutputMapping(1, 0, isNormallyClosed: false);

            // Act
            var config = MappingConfigManager.ExportFromCache(cache);

            // Assert
            Assert.Equal(2, config.Inputs.Count);
            Assert.Equal(2, config.Outputs.Count);

            // Input 0 -> Physical 1 (NC)
            var input0 = config.Inputs.Find(e => e.LogicalIndex == 0);
            Assert.NotNull(input0);
            Assert.Equal(1, input0.PhysicalIndex);
            Assert.True(input0.IsNormallyClosed);

            // Output 1 -> Physical 0 (NO)
            var output1 = config.Outputs.Find(e => e.LogicalIndex == 1);
            Assert.NotNull(output1);
            Assert.Equal(0, output1.PhysicalIndex);
            Assert.False(output1.IsNormallyClosed);
        }

        [Fact]
        public void IoContext_MappingAccessor_LoadFromFile_ShouldApplyMappings()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Mapping file is a "full mapping table": keep physical indices 1:1.
                var config = MappingConfigManager.GenerateDefault(inputCount: 4, outputCount: 4, description: "Test mapping");

                var input0 = config.Inputs.Find(e => e.LogicalIndex == 0);
                Assert.NotNull(input0);
                input0.PhysicalIndex = 3;
                input0.IsNormallyClosed = true;
                input0.Comment = "Reversed input";

                // Swap logical 3 away from physical 3 to avoid conflicts (default mapping is 1:1).
                var input3 = config.Inputs.Find(e => e.LogicalIndex == 3);
                Assert.NotNull(input3);
                input3.PhysicalIndex = 0;
                input3.IsNormallyClosed = false;
                input3.Comment = "Swap for input0";

                MappingConfigManager.Save(tempFile, config);

                var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
                hardware.Connect("test");

                var ctx = IoContextBuilder.For<TestLayout>()
                    .WithId("TestCtx")
                    .WithHardware(hardware)
                    .Build();

                // 设置物理输入 3 = true
                hardware.SetInputBit(3, true);

                // Act - 加载映射（逻辑 0 -> 物理 3，NC）
                ctx.Mapping.Load(tempFile);
                ctx.Tick();

                // Assert - 逻辑输入 0 应该读到 false（因为 NC，物理 true -> 逻辑 false）
                Assert.False(ctx.GetInput(0));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void IoContext_MappingAccessor_SaveToFile_ShouldExportCurrentMappings()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
                hardware.Connect("test");

                var ctx = IoContextBuilder.For<TestLayout>()
                    .WithId("TestCtx")
                    .WithHardware(hardware)
                    .Build();

                // 修改映射
                ctx.Mapping.SetInputMapping(0, 2, isNormallyClosed: true);
                ctx.Mapping.SetInputMapping(2, 0, isNormallyClosed: false); // keep 1:1 mapping
                ctx.Mapping.SetOutputMapping(1, 3, isNormallyClosed: false);
                ctx.Mapping.SetOutputMapping(3, 1, isNormallyClosed: false); // keep 1:1 mapping

                // Act
                ctx.Mapping.Save(tempFile);

                // Assert - 重新加载并验证
                var loadedConfig = MappingConfigManager.Load(tempFile);
                Assert.NotEmpty(loadedConfig.Inputs);
                Assert.NotEmpty(loadedConfig.Outputs);

                var input0 = loadedConfig.Inputs.Find(e => e.LogicalIndex == 0);
                Assert.NotNull(input0);
                Assert.Equal(2, input0.PhysicalIndex);
                Assert.True(input0.IsNormallyClosed);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void IoContext_MappingAccessor_LoadConfig_WithDuplicateInputPhysical_ShouldThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            ctx.Mapping.SetInputMapping(0, 2, isNormallyClosed: false);
            ctx.Mapping.SetInputMapping(1, 3, isNormallyClosed: false);

            var config = new MappingConfigFile();
            config.Inputs.Add(new MappingEntry
            {
                LogicalIndex = 0,
                PhysicalIndex = 1,
                IsNormallyClosed = false
            });
            config.Inputs.Add(new MappingEntry
            {
                LogicalIndex = 1,
                PhysicalIndex = 1,
                IsNormallyClosed = false
            });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ctx.Mapping.LoadConfig(config));

            // Assert - mapping unchanged
            Assert.Equal(2, ctx.Mapping.GetInputPhysicalIndex(0));
            Assert.Equal(3, ctx.Mapping.GetInputPhysicalIndex(1));
        }

        [Fact]
        public void IoContext_MappingAccessor_LoadConfig_WithDuplicateOutputPhysical_ShouldThrow()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            ctx.Mapping.SetOutputMapping(0, 2, isNormallyClosed: false);
            ctx.Mapping.SetOutputMapping(1, 3, isNormallyClosed: false);

            var config = new MappingConfigFile();
            config.Outputs.Add(new MappingEntry
            {
                LogicalIndex = 0,
                PhysicalIndex = 1,
                IsNormallyClosed = false
            });
            config.Outputs.Add(new MappingEntry
            {
                LogicalIndex = 1,
                PhysicalIndex = 1,
                IsNormallyClosed = false
            });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ctx.Mapping.LoadConfig(config));

            // Assert - mapping unchanged
            Assert.Equal(2, ctx.Mapping.GetOutputPhysicalIndex(0));
            Assert.Equal(3, ctx.Mapping.GetOutputPhysicalIndex(1));
        }

        [Fact]
        public void IoContext_MappingAccessor_Save_WithDuplicateInputPhysical_ShouldThrow()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
                hardware.Connect("test");

                var ctx = IoContextBuilder.For<TestLayout>()
                    .WithId("TestCtx")
                    .WithHardware(hardware)
                    .Build();

                ctx.Mapping.SetInputMapping(0, 1, isNormallyClosed: false);
                ctx.Mapping.SetInputMapping(1, 1, isNormallyClosed: false);

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => ctx.Mapping.Save(tempFile));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void IoContext_MappingAccessor_Save_WithDuplicateOutputPhysical_ShouldThrow()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
                hardware.Connect("test");

                var ctx = IoContextBuilder.For<TestLayout>()
                    .WithId("TestCtx")
                    .WithHardware(hardware)
                    .Build();

                ctx.Mapping.SetOutputMapping(0, 1, isNormallyClosed: false);
                ctx.Mapping.SetOutputMapping(1, 1, isNormallyClosed: false);

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => ctx.Mapping.Save(tempFile));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void IoContext_MappingAccessor_GenerateDefault_ShouldCreate1to1()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .Build();

            // 先修改映射
            ctx.Mapping.SetInputMapping(0, 3, isNormallyClosed: true);

            // Act - 生成默认映射
            ctx.Mapping.GenerateDefaultMapping();
            ctx.Tick();

            // 设置物理输入 0 = true
            hardware.SetInputBit(0, true);
            ctx.Tick();

            // Assert - 现在应该是 1:1 映射，NO
            Assert.True(ctx.GetInput(0));
        }

        [Fact]
        public void IoContextBuilder_WithMappingFile_ShouldLoadOnBuild()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Mapping file is a "full mapping table": keep physical indices 1:1.
                var config = MappingConfigManager.GenerateDefault(inputCount: 4, outputCount: 4, description: "Test mapping");

                var input0 = config.Inputs.Find(e => e.LogicalIndex == 0);
                Assert.NotNull(input0);
                input0.PhysicalIndex = 1;
                input0.IsNormallyClosed = false;

                // Swap logical 1 away from physical 1 to avoid conflicts (default mapping is 1:1).
                var input1 = config.Inputs.Find(e => e.LogicalIndex == 1);
                Assert.NotNull(input1);
                input1.PhysicalIndex = 0;
                input1.IsNormallyClosed = false;

                MappingConfigManager.Save(tempFile, config);

                var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
                hardware.Connect("test");

                // Act - 在构建时加载映射
                var ctx = IoContextBuilder.For<TestLayout>()
                    .WithId("TestCtx")
                    .WithHardware(hardware)
                    .WithMapping(tempFile)
                    .Build();

                // 设置物理输入 1 = true
                hardware.SetInputBit(1, true);
                ctx.Tick();

                // Assert - 逻辑输入 0 应该读到 true（映射到物理 1）
                Assert.True(ctx.GetInput(0));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void IoContextBuilder_WithMappingConfigurator_ShouldApplyFluentConfig()
        {
            // Arrange
            var hardware = new InMemoryHardwareIO(inputCount: 4, outputCount: 4);
            hardware.Connect("test");

            // Act - 使用 Fluent API 配置映射
            var ctx = IoContextBuilder.For<TestLayout>()
                .WithId("TestCtx")
                .WithHardware(hardware)
                .WithMapping(m =>
                {
                    m.SetInput(0, 2, isNormallyClosed: true, comment: "Test input");
                    m.SetInput(2, 0, isNormallyClosed: false, comment: "Swap input");
                    m.SetOutput(1, 3, isNormallyClosed: false, comment: "Test output");
                    m.SetOutput(3, 1, isNormallyClosed: false, comment: "Swap output");
                })
                .Build();

            // 设置物理输入 2 = true
            hardware.SetInputBit(2, true);
            ctx.Tick();

            // Assert - 逻辑输入 0 应该读到 false（NC，物理 true -> 逻辑 false）
            Assert.False(ctx.GetInput(0));

            // 写逻辑输出 1 = true
            ctx.On(1, now: true);

            // Assert - 物理输出 3 应该是 true（NO）
            var physicalOutputs = hardware.GetAllOutputs();
            Assert.True(physicalOutputs[3]);
        }

        #endregion
    }
}
