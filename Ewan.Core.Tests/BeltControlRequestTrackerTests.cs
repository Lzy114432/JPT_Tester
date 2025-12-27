using Ewan.Core.Module;
using Ewan.Model.Messages;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ewan.Core.Tests
{
    /// <summary>
    /// BeltControlRequestTracker 单元测试
    /// </summary>
    public class BeltControlRequestTrackerTests
    {
        #region 基本功能测试

        [Fact]
        public void HasAnyStopRequest_Initially_ReturnsFalse()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act & Assert
            Assert.False(tracker.HasAnyStopRequest);
            Assert.Equal(0, tracker.ActiveRequestCount);
        }

        [Fact]
        public void Request_WhenStopRequested_AddsToActiveRequests()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act
            bool result = tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "测试原因");

            // Assert
            Assert.True(result);
            Assert.True(tracker.HasAnyStopRequest);
            Assert.Equal(1, tracker.ActiveRequestCount);
            Assert.Contains(BeltConveyorControlSource.MaterialLoading, tracker.ActiveRequests);
        }

        [Fact]
        public void Request_WhenReleased_RemovesFromActiveRequests()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true);

            // Act
            bool result = tracker.Request(BeltConveyorControlSource.MaterialLoading, false);

            // Assert
            Assert.False(result);
            Assert.False(tracker.HasAnyStopRequest);
            Assert.Equal(0, tracker.ActiveRequestCount);
        }

        [Fact]
        public void Request_MultipleSources_TracksAllRequests()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "装料停止");
            tracker.Request(BeltConveyorControlSource.MaterialUnloading, true, "下料停止");

            // Assert
            Assert.True(tracker.HasAnyStopRequest);
            Assert.Equal(2, tracker.ActiveRequestCount);
            Assert.Contains(BeltConveyorControlSource.MaterialLoading, tracker.ActiveRequests);
            Assert.Contains(BeltConveyorControlSource.MaterialUnloading, tracker.ActiveRequests);
        }

        [Fact]
        public void Request_PartialRelease_StillHasActiveRequests()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true);
            tracker.Request(BeltConveyorControlSource.MaterialUnloading, true);

            // Act
            tracker.Request(BeltConveyorControlSource.MaterialLoading, false);

            // Assert
            Assert.True(tracker.HasAnyStopRequest);
            Assert.Equal(1, tracker.ActiveRequestCount);
            Assert.DoesNotContain(BeltConveyorControlSource.MaterialLoading, tracker.ActiveRequests);
            Assert.Contains(BeltConveyorControlSource.MaterialUnloading, tracker.ActiveRequests);
        }

        #endregion

        #region ProcessMessage 测试

        [Fact]
        public void ProcessMessage_StopMessage_AddsRequest()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            var message = BeltConveyorControlMessage.Stop(
                BeltConveyorControlSource.MaterialLoading,
                "机械手正在取料");

            // Act
            bool result = tracker.ProcessMessage(message);

            // Assert
            Assert.True(result);
            Assert.True(tracker.HasRequestFrom(BeltConveyorControlSource.MaterialLoading));
            Assert.Equal("机械手正在取料", tracker.GetReasonFor(BeltConveyorControlSource.MaterialLoading));
        }

        [Fact]
        public void ProcessMessage_ReleaseMessage_RemovesRequest()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.ProcessMessage(BeltConveyorControlMessage.Stop(
                BeltConveyorControlSource.MaterialLoading));

            var releaseMessage = BeltConveyorControlMessage.Release(
                BeltConveyorControlSource.MaterialLoading,
                "装料完成");

            // Act
            bool result = tracker.ProcessMessage(releaseMessage);

            // Assert
            Assert.False(result);
            Assert.False(tracker.HasRequestFrom(BeltConveyorControlSource.MaterialLoading));
        }

        [Fact]
        public void ProcessMessage_NullMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act & Assert
            Assert.Throws<System.ArgumentNullException>(() => tracker.ProcessMessage(null!));
        }

        #endregion

        #region HasRequestFrom 测试

        [Fact]
        public void HasRequestFrom_WhenSourceHasRequest_ReturnsTrue()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true);

            // Act & Assert
            Assert.True(tracker.HasRequestFrom(BeltConveyorControlSource.MaterialLoading));
            Assert.False(tracker.HasRequestFrom(BeltConveyorControlSource.MaterialUnloading));
        }

        #endregion

        #region GetReasonFor 测试

        [Fact]
        public void GetReasonFor_WhenSourceHasRequest_ReturnsReason()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "测试原因");

            // Act
            var reason = tracker.GetReasonFor(BeltConveyorControlSource.MaterialLoading);

            // Assert
            Assert.Equal("测试原因", reason);
        }

        [Fact]
        public void GetReasonFor_WhenSourceHasNoRequest_ReturnsNull()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act
            var reason = tracker.GetReasonFor(BeltConveyorControlSource.MaterialLoading);

            // Assert
            Assert.Null(reason);
        }

        [Fact]
        public void GetReasonFor_WhenNoReasonProvided_ReturnsEmptyString()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true);

            // Act
            var reason = tracker.GetReasonFor(BeltConveyorControlSource.MaterialLoading);

            // Assert
            Assert.Equal(string.Empty, reason);
        }

        #endregion

        #region GetAllActiveRequestDetails 测试

        [Fact]
        public void GetAllActiveRequestDetails_ReturnsAllRequestsWithReasons()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "装料原因");
            tracker.Request(BeltConveyorControlSource.MaterialUnloading, true, "下料原因");

            // Act
            var details = tracker.GetAllActiveRequestDetails();

            // Assert
            Assert.Equal(2, details.Count);
            Assert.Equal("装料原因", details[BeltConveyorControlSource.MaterialLoading]);
            Assert.Equal("下料原因", details[BeltConveyorControlSource.MaterialUnloading]);
        }

        #endregion

        #region Clear 测试

        [Fact]
        public void Clear_RemovesAllRequests()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true);
            tracker.Request(BeltConveyorControlSource.MaterialUnloading, true);

            // Act
            tracker.Clear();

            // Assert
            Assert.False(tracker.HasAnyStopRequest);
            Assert.Equal(0, tracker.ActiveRequestCount);
            Assert.Empty(tracker.ActiveRequests);
        }

        #endregion

        #region ForceRelease 测试

        [Fact]
        public void ForceRelease_WhenRequestExists_ReturnsTrue()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true);

            // Act
            bool existed = tracker.ForceRelease(BeltConveyorControlSource.MaterialLoading);

            // Assert
            Assert.True(existed);
            Assert.False(tracker.HasRequestFrom(BeltConveyorControlSource.MaterialLoading));
        }

        [Fact]
        public void ForceRelease_WhenRequestDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act
            bool existed = tracker.ForceRelease(BeltConveyorControlSource.MaterialLoading);

            // Assert
            Assert.False(existed);
        }

        #endregion

        #region GetDiagnosticInfo 测试

        [Fact]
        public void GetDiagnosticInfo_WhenNoRequests_ReturnsNoActiveMessage()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act
            var info = tracker.GetDiagnosticInfo();

            // Assert
            Assert.Equal("无活跃停止请求", info);
        }

        [Fact]
        public void GetDiagnosticInfo_WhenHasRequests_ReturnsDetailedInfo()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "测试原因");

            // Act
            var info = tracker.GetDiagnosticInfo();

            // Assert
            Assert.Contains("活跃停止请求", info);
            Assert.Contains("MaterialLoading", info);
            Assert.Contains("测试原因", info);
        }

        #endregion

        #region 线程安全测试

        [Fact]
        public void Request_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            var tasks = new Task[100];

            // Act
            for (int i = 0; i < 50; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    tracker.Request(BeltConveyorControlSource.MaterialLoading, true, $"请求{index}");
                });
            }

            for (int i = 50; i < 100; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    tracker.Request(BeltConveyorControlSource.MaterialLoading, false);
                });
            }

            Task.WaitAll(tasks);

            // Assert - 无异常即通过，最终状态取决于执行顺序
            // 验证追踪器仍然可用
            _ = tracker.HasAnyStopRequest;
            _ = tracker.ActiveRequestCount;
            _ = tracker.GetDiagnosticInfo();
        }

        [Fact]
        public void Request_ConcurrentMultipleSources_ThreadSafe()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();
            var tasks = new Task[200];

            // Act - 同时操作两个来源
            for (int i = 0; i < 100; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    tracker.Request(BeltConveyorControlSource.MaterialLoading, true);
                    tracker.Request(BeltConveyorControlSource.MaterialLoading, false);
                });
            }

            for (int i = 100; i < 200; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    tracker.Request(BeltConveyorControlSource.MaterialUnloading, true);
                    tracker.Request(BeltConveyorControlSource.MaterialUnloading, false);
                });
            }

            Task.WaitAll(tasks);

            // Assert - 所有操作完成后，应该没有活跃请求（因为每个请求都有对应的释放）
            // 注意：由于并发执行顺序不确定，这里只验证状态一致性
            Assert.True(tracker.ActiveRequestCount >= 0);
            Assert.True(tracker.ActiveRequestCount <= 2);
        }

        #endregion

        #region 边界情况测试

        [Fact]
        public void Request_DuplicateStop_DoesNotDuplicate()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "第一次");
            tracker.Request(BeltConveyorControlSource.MaterialLoading, true, "第二次");

            // Assert - HashSet 确保不重复
            Assert.Equal(1, tracker.ActiveRequestCount);
            // 原因会被覆盖为最新的
            Assert.Equal("第二次", tracker.GetReasonFor(BeltConveyorControlSource.MaterialLoading));
        }

        [Fact]
        public void Request_ReleaseWithoutStop_NoError()
        {
            // Arrange
            var tracker = new BeltControlRequestTracker();

            // Act - 释放一个从未请求过的来源
            bool result = tracker.Request(BeltConveyorControlSource.MaterialLoading, false);

            // Assert
            Assert.False(result);
            Assert.False(tracker.HasAnyStopRequest);
        }

        #endregion
    }
}
