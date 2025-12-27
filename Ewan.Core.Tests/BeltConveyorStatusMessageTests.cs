using Ewan.Model.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ewan.Core.Tests
{
    /// <summary>
    /// BeltConveyorStatusMessage 单元测试
    /// </summary>
    public class BeltConveyorStatusMessageTests
    {
        #region 构造函数测试

        [Fact]
        public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var sources = new[] { BeltConveyorControlSource.MaterialLoading };

            // Act
            var message = new BeltConveyorStatusMessage(
                isRunning: true,
                activeRequests: sources,
                reason: "测试原因",
                changeType: BeltStatusChangeType.Started,
                isSystemStopped: false,
                isModuleEnabled: true);

            // Assert
            Assert.True(message.IsRunning);
            Assert.Single(message.ActiveStopRequests);
            Assert.Contains(BeltConveyorControlSource.MaterialLoading, message.ActiveStopRequests);
            Assert.Equal("测试原因", message.LastChangeReason);
            Assert.Equal(BeltStatusChangeType.Started, message.ChangeType);
            Assert.False(message.IsSystemStopped);
            Assert.True(message.IsModuleEnabled);
        }

        [Fact]
        public void Constructor_WithDefaultParameters_SetsDefaultsCorrectly()
        {
            // Act
            var message = new BeltConveyorStatusMessage(isRunning: false);

            // Assert
            Assert.False(message.IsRunning);
            Assert.Empty(message.ActiveStopRequests);
            Assert.Equal(string.Empty, message.LastChangeReason);
            Assert.Equal(BeltStatusChangeType.StatusUpdate, message.ChangeType);
            Assert.False(message.IsSystemStopped);
            Assert.True(message.IsModuleEnabled);
        }

        [Fact]
        public void Constructor_WithNullActiveRequests_CreatesEmptyList()
        {
            // Act
            var message = new BeltConveyorStatusMessage(
                isRunning: true,
                activeRequests: null);

            // Assert
            Assert.NotNull(message.ActiveStopRequests);
            Assert.Empty(message.ActiveStopRequests);
        }

        [Fact]
        public void Constructor_SetsTimestamp()
        {
            // Arrange
            var before = DateTimeOffset.Now;

            // Act
            var message = new BeltConveyorStatusMessage(isRunning: true);

            // Assert
            var after = DateTimeOffset.Now;
            Assert.True(message.Timestamp >= before);
            Assert.True(message.Timestamp <= after);
        }

        #endregion

        #region 工厂方法测试

        [Fact]
        public void Started_CreatesCorrectMessage()
        {
            // Act
            var message = BeltConveyorStatusMessage.Started("测试启动");

            // Assert
            Assert.True(message.IsRunning);
            Assert.Empty(message.ActiveStopRequests);
            Assert.Equal("测试启动", message.LastChangeReason);
            Assert.Equal(BeltStatusChangeType.Started, message.ChangeType);
            Assert.False(message.IsSystemStopped);
            Assert.True(message.IsModuleEnabled);
        }

        [Fact]
        public void Started_WithNullReason_UsesDefaultReason()
        {
            // Act
            var message = BeltConveyorStatusMessage.Started();

            // Assert
            Assert.Equal("皮带启动", message.LastChangeReason);
        }

        [Fact]
        public void Stopped_CreatesCorrectMessage()
        {
            // Arrange
            var sources = new List<BeltConveyorControlSource>
            {
                BeltConveyorControlSource.MaterialLoading,
                BeltConveyorControlSource.MaterialUnloading
            };

            // Act
            var message = BeltConveyorStatusMessage.Stopped(sources, "测试停止");

            // Assert
            Assert.False(message.IsRunning);
            Assert.Equal(2, message.ActiveStopRequests.Count);
            Assert.Contains(BeltConveyorControlSource.MaterialLoading, message.ActiveStopRequests);
            Assert.Contains(BeltConveyorControlSource.MaterialUnloading, message.ActiveStopRequests);
            Assert.Equal("测试停止", message.LastChangeReason);
            Assert.Equal(BeltStatusChangeType.Stopped, message.ChangeType);
        }

        [Fact]
        public void Stopped_WithNullReason_UsesDefaultReason()
        {
            // Act
            var message = BeltConveyorStatusMessage.Stopped(
                Enumerable.Empty<BeltConveyorControlSource>());

            // Assert
            Assert.Equal("皮带停止", message.LastChangeReason);
        }

        [Fact]
        public void SystemStopped_CreatesCorrectMessage()
        {
            // Act
            var message = BeltConveyorStatusMessage.SystemStopped("紧急停止");

            // Assert
            Assert.False(message.IsRunning);
            Assert.Empty(message.ActiveStopRequests);
            Assert.Equal("紧急停止", message.LastChangeReason);
            Assert.Equal(BeltStatusChangeType.SystemStopped, message.ChangeType);
            Assert.True(message.IsSystemStopped);
            Assert.True(message.IsModuleEnabled);
        }

        [Fact]
        public void ModuleDisabled_CreatesCorrectMessage()
        {
            // Act
            var message = BeltConveyorStatusMessage.ModuleDisabled();

            // Assert
            Assert.False(message.IsRunning);
            Assert.Empty(message.ActiveStopRequests);
            Assert.Equal("模块已禁用", message.LastChangeReason);
            Assert.Equal(BeltStatusChangeType.ModuleDisabled, message.ChangeType);
            Assert.False(message.IsSystemStopped);
            Assert.False(message.IsModuleEnabled);
        }

        #endregion

        #region GetStatusDescription 测试

        [Fact]
        public void GetStatusDescription_WhenModuleDisabled_ReturnsModuleDisabled()
        {
            // Arrange
            var message = BeltConveyorStatusMessage.ModuleDisabled();

            // Act
            var description = message.GetStatusDescription();

            // Assert
            Assert.Equal("模块禁用", description);
        }

        [Fact]
        public void GetStatusDescription_WhenSystemStopped_ReturnsSystemStopWithReason()
        {
            // Arrange
            var message = BeltConveyorStatusMessage.SystemStopped("紧急停止触发");

            // Act
            var description = message.GetStatusDescription();

            // Assert
            Assert.Equal("系统停止: 紧急停止触发", description);
        }

        [Fact]
        public void GetStatusDescription_WhenRunning_ReturnsRunning()
        {
            // Arrange
            var message = BeltConveyorStatusMessage.Started();

            // Act
            var description = message.GetStatusDescription();

            // Assert
            Assert.Equal("运行中", description);
        }

        [Fact]
        public void GetStatusDescription_WhenStoppedWithRequests_ReturnsStoppedWithSources()
        {
            // Arrange
            var sources = new[] { BeltConveyorControlSource.MaterialLoading };
            var message = BeltConveyorStatusMessage.Stopped(sources);

            // Act
            var description = message.GetStatusDescription();

            // Assert
            Assert.Contains("停止", description);
            Assert.Contains("MaterialLoading", description);
        }

        [Fact]
        public void GetStatusDescription_WhenStoppedWithoutRequests_ReturnsStopped()
        {
            // Arrange
            var message = new BeltConveyorStatusMessage(
                isRunning: false,
                activeRequests: null,
                reason: "测试");

            // Act
            var description = message.GetStatusDescription();

            // Assert
            Assert.Equal("已停止", description);
        }

        #endregion

        #region IMessage 接口测试

        [Fact]
        public void Timestamp_CanBeSetAndGet()
        {
            // Arrange
            var message = new BeltConveyorStatusMessage(isRunning: true);
            var newTimestamp = DateTimeOffset.Now.AddMinutes(-5);

            // Act
            message.Timestamp = newTimestamp;

            // Assert
            Assert.Equal(newTimestamp, message.Timestamp);
        }

        #endregion

        #region 不可变性测试

        [Fact]
        public void ActiveStopRequests_IsReadOnly()
        {
            // Arrange
            var sources = new List<BeltConveyorControlSource>
            {
                BeltConveyorControlSource.MaterialLoading
            };
            var message = BeltConveyorStatusMessage.Stopped(sources);

            // Act & Assert - 验证返回的是只读集合
            Assert.IsAssignableFrom<IReadOnlyList<BeltConveyorControlSource>>(message.ActiveStopRequests);
        }

        [Fact]
        public void Constructor_CopiesSourceList_ModifyingOriginalDoesNotAffectMessage()
        {
            // Arrange
            var sources = new List<BeltConveyorControlSource>
            {
                BeltConveyorControlSource.MaterialLoading
            };

            // Act
            var message = BeltConveyorStatusMessage.Stopped(sources);
            sources.Add(BeltConveyorControlSource.MaterialUnloading);
            sources.Clear();

            // Assert - 原始列表的修改不影响消息
            Assert.Single(message.ActiveStopRequests);
            Assert.Contains(BeltConveyorControlSource.MaterialLoading, message.ActiveStopRequests);
        }

        #endregion

        #region BeltStatusChangeType 枚举测试

        [Theory]
        [InlineData(BeltStatusChangeType.StatusUpdate, 0)]
        [InlineData(BeltStatusChangeType.Started, 1)]
        [InlineData(BeltStatusChangeType.Stopped, 2)]
        [InlineData(BeltStatusChangeType.SystemStopped, 3)]
        [InlineData(BeltStatusChangeType.ModuleDisabled, 4)]
        public void BeltStatusChangeType_HasExpectedValues(BeltStatusChangeType type, int expectedValue)
        {
            // Assert
            Assert.Equal(expectedValue, (int)type);
        }

        #endregion
    }
}
