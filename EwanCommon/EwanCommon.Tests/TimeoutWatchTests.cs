using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EwanCore.StateMachine;
using Xunit;

namespace EwanCommon.Tests
{
    /// <summary>
    /// TimeoutWatch 超时监视单元测试
    /// </summary>
    public class TimeoutWatchTests
    {
        #region StartWatch 测试

        [Fact]
        public void StartWatch_ShouldCreateNewWatch()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            watch.StartWatch("test");

            // Assert
            Assert.True(watch.IsExit("test"));
        }

        [Fact]
        public void StartWatch_SameNameTwice_ShouldNotReset()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");
            Thread.Sleep(50);
            var firstTime = watch.GetTimeSpan("test");

            // Act
            watch.StartWatch("test"); // 再次调用不应该重置
            var secondTime = watch.GetTimeSpan("test");

            // Assert
            Assert.True(secondTime >= firstTime);
        }

        [Fact]
        public void StartWatch_NullOrWhitespace_ShouldBeIgnored()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            watch.StartWatch(null!);
            watch.StartWatch("");
            watch.StartWatch("   ");

            // Assert
            Assert.False(watch.IsExit(null!));
            Assert.False(watch.IsExit(""));
        }

        [Fact]
        public void StartWatch_MultipleNames_ShouldTrackIndependently()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            watch.StartWatch("watch1");
            Thread.Sleep(20);
            watch.StartWatch("watch2");
            Thread.Sleep(20);

            // Assert
            var time1 = watch.GetTimeSpan("watch1");
            var time2 = watch.GetTimeSpan("watch2");
            Assert.True(time1 > time2); // watch1 启动更早，所以时间更长
        }

        #endregion

        #region StartCheckIsTimeout 测试

        [Fact]
        public void StartCheckIsTimeout_FirstCall_ShouldReturnFalseAndStartWatch()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            var result = watch.StartCheckIsTimeout("test", 1000);

            // Assert
            Assert.False(result); // 第一次调用应返回 false
            Assert.True(watch.IsExit("test")); // 并且启动计时
        }

        [Fact]
        public void StartCheckIsTimeout_BeforeTimeout_ShouldReturnFalse()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartCheckIsTimeout("test", 1000);

            // Act - 立即检查，不应超时
            var result = watch.StartCheckIsTimeout("test", 1000);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void StartCheckIsTimeout_AfterTimeout_ShouldReturnTrue()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartCheckIsTimeout("test", 50);

            // Act - 等待超时
            Thread.Sleep(100);
            var result = watch.StartCheckIsTimeout("test", 50);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void StartCheckIsTimeout_NullOrWhitespace_ShouldReturnFalse()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act & Assert
            Assert.False(watch.StartCheckIsTimeout(null!, 1000));
            Assert.False(watch.StartCheckIsTimeout("", 1000));
            Assert.False(watch.StartCheckIsTimeout("   ", 1000));
        }

        [Fact]
        public void StartCheckIsTimeout_ZeroOrNegativeTimeout_ShouldReturnFalse()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act & Assert
            Assert.False(watch.StartCheckIsTimeout("test1", 0));
            Assert.False(watch.StartCheckIsTimeout("test2", -1));
        }

        #endregion

        #region StopWatch 测试

        [Fact]
        public void StopWatch_ShouldRemoveWatch()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");
            Assert.True(watch.IsExit("test"));

            // Act
            watch.StopWatch("test");

            // Assert
            Assert.False(watch.IsExit("test"));
        }

        [Fact]
        public void StopWatch_NonExisting_ShouldNotThrow()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            var ex = Record.Exception(() => watch.StopWatch("nonexistent"));

            // Assert
            Assert.Null(ex);
        }

        [Fact]
        public void StopWatch_NullOrWhitespace_ShouldBeIgnored()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");

            // Act
            watch.StopWatch(null!);
            watch.StopWatch("");
            watch.StopWatch("   ");

            // Assert
            Assert.True(watch.IsExit("test")); // 原有的 watch 不受影响
        }

        #endregion

        #region StopAllWatch 测试

        [Fact]
        public void StopAllWatch_ShouldRemoveAllWatches()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("watch1");
            watch.StartWatch("watch2");
            watch.StartWatch("watch3");

            // Act
            watch.StopAllWatch();

            // Assert
            Assert.False(watch.IsExit("watch1"));
            Assert.False(watch.IsExit("watch2"));
            Assert.False(watch.IsExit("watch3"));
        }

        [Fact]
        public void StopAllWatch_EmptyWatch_ShouldNotThrow()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            var ex = Record.Exception(() => watch.StopAllWatch());

            // Assert
            Assert.Null(ex);
        }

        #endregion

        #region IsExit 测试

        [Fact]
        public void IsExit_ExistingName_ShouldReturnTrue()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");

            // Assert
            Assert.True(watch.IsExit("test"));
        }

        [Fact]
        public void IsExit_NonExistingName_ShouldReturnFalse()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Assert
            Assert.False(watch.IsExit("nonexistent"));
        }

        [Fact]
        public void IsExit_NullOrWhitespace_ShouldReturnFalse()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Assert
            Assert.False(watch.IsExit(null!));
            Assert.False(watch.IsExit(""));
            Assert.False(watch.IsExit("   "));
        }

        #endregion

        #region GetTimeSpan 测试

        [Fact]
        public void GetTimeSpan_ExistingWatch_ShouldReturnElapsedTime()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");
            Thread.Sleep(50);

            // Act
            var elapsed = watch.GetTimeSpan("test");

            // Assert
            Assert.True(elapsed >= 50);
        }

        [Fact]
        public void GetTimeSpan_NonExistingWatch_ShouldReturnZero()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            var elapsed = watch.GetTimeSpan("nonexistent");

            // Assert
            Assert.Equal(0, elapsed);
        }

        [Fact]
        public void GetTimeSpan_NullOrWhitespace_ShouldReturnZero()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Assert
            Assert.Equal(0, watch.GetTimeSpan(null!));
            Assert.Equal(0, watch.GetTimeSpan(""));
            Assert.Equal(0, watch.GetTimeSpan("   "));
        }

        [Fact]
        public void GetTimeSpan_ShouldIncreaseOverTime()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");

            // Act
            var time1 = watch.GetTimeSpan("test");
            Thread.Sleep(50);
            var time2 = watch.GetTimeSpan("test");

            // Assert
            Assert.True(time2 > time1);
        }

        #endregion

        #region IEnumerable 测试

        [Fact]
        public void Enumerable_ShouldReturnAllWatches()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("watch1");
            watch.StartWatch("watch2");
            watch.StartWatch("watch3");
            Thread.Sleep(10);

            // Act
            var items = watch.ToList();

            // Assert
            Assert.Equal(3, items.Count);
            Assert.Contains(items, x => x.Key == "watch1");
            Assert.Contains(items, x => x.Key == "watch2");
            Assert.Contains(items, x => x.Key == "watch3");
        }

        [Fact]
        public void Enumerable_ShouldReturnSnapshotWithTimeSpan()
        {
            // Arrange
            var watch = new TimeoutWatch();
            watch.StartWatch("test");
            Thread.Sleep(50);

            // Act
            var items = watch.ToList();

            // Assert
            Assert.Single(items);
            Assert.Equal("test", items[0].Key);
            Assert.True(items[0].Value.TotalMilliseconds >= 50);
        }

        [Fact]
        public void Enumerable_EmptyWatch_ShouldReturnEmpty()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act
            var items = watch.ToList();

            // Assert
            Assert.Empty(items);
        }

        #endregion

        #region 线程安全测试

        [Fact]
        public async Task TimeoutWatch_ShouldBeThreadSafe()
        {
            // Arrange
            var watch = new TimeoutWatch();
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act
            for (int i = 0; i < 10; i++)
            {
                var threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            watch.StartWatch($"watch_{threadId}_{j}");
                            watch.StartCheckIsTimeout($"timeout_{threadId}_{j}", 100);
                            var _ = watch.GetTimeSpan($"watch_{threadId}_{j}");
                            watch.StopWatch($"watch_{threadId}_{j}");
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.Empty(exceptions);
        }

        [Fact]
        public async Task TimeoutWatch_ConcurrentStartAndStop_ShouldNotThrow()
        {
            // Arrange
            var watch = new TimeoutWatch();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var exceptions = new List<Exception>();

            // Act
            var startTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            watch.StartWatch($"concurrent_{i}");
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) exceptions.Add(ex);
                    }
                }
            });

            var stopTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            watch.StopWatch($"concurrent_{i}");
                        }
                        watch.StopAllWatch();
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) exceptions.Add(ex);
                    }
                }
            });

            var readTask = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            var _ = watch.GetTimeSpan($"concurrent_{i}");
                            var __ = watch.IsExit($"concurrent_{i}");
                        }
                        var ___ = watch.ToList();
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) exceptions.Add(ex);
                    }
                }
            });

            await Task.WhenAll(startTask, stopTask, readTask);

            // Assert
            Assert.Empty(exceptions);
        }

        #endregion

        #region 使用场景测试

        [Fact]
        public void UseCase_StepTimeout_ShouldDetectSlowStep()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act - 模拟步骤执行
            // 第一次检查，开始计时
            Assert.False(watch.StartCheckIsTimeout("Step1", 100));

            // 模拟步骤执行时间过长
            Thread.Sleep(150);

            // Assert - 检测到超时
            Assert.True(watch.StartCheckIsTimeout("Step1", 100));
        }

        [Fact]
        public void UseCase_MultipleStepsWithDifferentTimeouts()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act - 启动多个步骤计时
            watch.StartCheckIsTimeout("FastStep", 50);
            watch.StartCheckIsTimeout("SlowStep", 500);

            Thread.Sleep(100);

            // Assert - FastStep 超时，SlowStep 未超时
            Assert.True(watch.StartCheckIsTimeout("FastStep", 50));
            Assert.False(watch.StartCheckIsTimeout("SlowStep", 500));
        }

        [Fact]
        public void UseCase_ResetTimeoutAfterCompletion()
        {
            // Arrange
            var watch = new TimeoutWatch();

            // Act - 步骤1开始
            watch.StartCheckIsTimeout("CurrentStep", 100);
            Thread.Sleep(50);

            // 步骤1完成，清除计时
            watch.StopWatch("CurrentStep");

            // 步骤2开始（重用名称）
            Assert.False(watch.StartCheckIsTimeout("CurrentStep", 100)); // 应该是新的计时

            // Assert
            var elapsed = watch.GetTimeSpan("CurrentStep");
            Assert.True(elapsed < 50); // 应该是新开始的计时
        }

        #endregion
    }
}
