using System;
using System.Threading;
using System.Threading.Tasks;
using EwanIO.Core.Data;
using Xunit;

namespace EwanIO.Tests
{
    /// <summary>
    /// PulseOperation 脉冲操作测试
    ///
    /// 测试目的：
    /// PulseOperation 用于实现输出信号的脉冲控制（定时输出）。
    /// 在工控场景中，脉冲控制用于气缸动作、指示灯闪烁、蜂鸣器等。
    ///
    /// 主要测试内容：
    /// 1. 基本功能 - 构造函数、属性初始化、启动/完成/取消/重置
    /// 2. 边界条件 - 零持续时间、重复启动、强制启动
    /// 3. 时间计算 - ElapsedMs、RemainingMs、IsExpired
    /// 4. 回调机制 - 完成回调触发、回调异常处理
    /// 5. 线程安全 - 并发启动、并发完成检查
    ///
    /// 线程安全说明：
    /// - Start/ForceStart/TryComplete 使用 Interlocked 保证原子性
    /// - IsActive/DurationMs 使用 Volatile 保证可见性
    /// </summary>
    public class PulseOperationTests
    {
        #region 构造函数和属性测试 - 验证初始化状态

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var pulse = new PulseOperation(5);

            // Assert
            Assert.Equal(5, pulse.OutputIndex);
            Assert.Equal(0, pulse.DurationMs);
            Assert.False(pulse.EndValue);
            Assert.False(pulse.IsActive);
            Assert.Equal(0, pulse.ElapsedMs);
            Assert.Equal(0, pulse.RemainingMs);
            Assert.False(pulse.IsExpired);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void Constructor_WithDifferentIndices_ShouldSetOutputIndex(int index)
        {
            // Arrange & Act
            var pulse = new PulseOperation(index);

            // Assert
            Assert.Equal(index, pulse.OutputIndex);
        }

        #endregion

        #region Start 方法测试 - 验证脉冲启动逻辑

        [Fact]
        public void Start_WhenNotActive_ShouldReturnTrueAndActivate()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act
            bool result = pulse.Start(1000, true);

            // Assert
            Assert.True(result);
            Assert.True(pulse.IsActive);
            Assert.Equal(1000, pulse.DurationMs);
            Assert.True(pulse.EndValue);
        }

        [Fact]
        public void Start_WhenAlreadyActive_ShouldReturnFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(1000, true);

            // Act
            bool result = pulse.Start(2000, false);

            // Assert
            Assert.False(result);
            Assert.True(pulse.IsActive);
            Assert.Equal(1000, pulse.DurationMs);  // 保持原来的值
            Assert.True(pulse.EndValue);  // 保持原来的值
        }

        [Fact]
        public void Start_WithTimeSpan_ShouldConvertToMilliseconds()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            var duration = TimeSpan.FromSeconds(2.5);

            // Act
            bool result = pulse.Start(duration, false);

            // Assert
            Assert.True(result);
            Assert.Equal(2500, pulse.DurationMs);
            Assert.False(pulse.EndValue);
        }

        [Fact]
        public void Start_WithCallback_ShouldStoreCallback()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            int callbackIndex = -1;

            // Act
            pulse.Start(10, true, idx => callbackIndex = idx);

            // 等待脉冲完成
            Thread.Sleep(50);
            pulse.TryComplete(out _);

            // Assert
            Assert.Equal(0, callbackIndex);
        }

        [Fact]
        public void Start_WithZeroDuration_ShouldActivateAndExpireImmediately()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act
            pulse.Start(0, true);

            // Assert
            Assert.True(pulse.IsActive);
            Assert.True(pulse.IsExpired);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Start_WithDifferentEndValues_ShouldSetEndValue(bool endValue)
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act
            pulse.Start(100, endValue);

            // Assert
            Assert.Equal(endValue, pulse.EndValue);
        }

        #endregion

        #region ForceStart 方法测试 - 验证强制启动逻辑

        [Fact]
        public void ForceStart_WhenNotActive_ShouldActivate()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act
            pulse.ForceStart(1000, true);

            // Assert
            Assert.True(pulse.IsActive);
            Assert.Equal(1000, pulse.DurationMs);
            Assert.True(pulse.EndValue);
        }

        [Fact]
        public void ForceStart_WhenAlreadyActive_ShouldOverride()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(1000, true);

            // Act
            pulse.ForceStart(2000, false);

            // Assert
            Assert.True(pulse.IsActive);
            Assert.Equal(2000, pulse.DurationMs);  // 被覆盖
            Assert.False(pulse.EndValue);  // 被覆盖
        }

        [Fact]
        public void ForceStart_ShouldResetStopwatch()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(1000, true);
            Thread.Sleep(50);  // 等待一段时间

            // Act
            pulse.ForceStart(1000, true);

            // Assert - 计时器应该重新开始
            Assert.True(pulse.ElapsedMs < 50);
        }

        #endregion

        #region TryComplete 方法测试 - 验证完成检查逻辑

        [Fact]
        public void TryComplete_WhenNotActive_ShouldReturnFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act
            bool result = pulse.TryComplete(out bool endValue);

            // Assert
            Assert.False(result);
            Assert.False(endValue);
        }

        [Fact]
        public void TryComplete_WhenNotExpired_ShouldReturnFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10000, true);  // 10秒，不会过期

            // Act
            bool result = pulse.TryComplete(out bool endValue);

            // Assert
            Assert.False(result);
            Assert.False(endValue);
            Assert.True(pulse.IsActive);  // 仍然活跃
        }

        [Fact]
        public void TryComplete_WhenExpired_ShouldReturnTrueAndDeactivate()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true);  // 10ms
            Thread.Sleep(50);  // 等待过期

            // Act
            bool result = pulse.TryComplete(out bool endValue);

            // Assert
            Assert.True(result);
            Assert.True(endValue);
            Assert.False(pulse.IsActive);  // 已经不活跃
        }

        [Fact]
        public void TryComplete_WhenExpiredWithEndValueFalse_ShouldOutputFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, false);
            Thread.Sleep(50);

            // Act
            pulse.TryComplete(out bool endValue);

            // Assert
            Assert.False(endValue);
        }

        [Fact]
        public void TryComplete_ShouldTriggerCallback()
        {
            // Arrange
            var pulse = new PulseOperation(3);
            int callbackIndex = -1;
            pulse.Start(10, true, idx => callbackIndex = idx);
            Thread.Sleep(50);

            // Act
            pulse.TryComplete(out _);

            // Assert
            Assert.Equal(3, callbackIndex);
        }

        [Fact]
        public void TryComplete_WithThrowingCallback_ShouldNotThrow()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true, _ => throw new InvalidOperationException("Test exception"));
            Thread.Sleep(50);

            // Act & Assert - 不应该抛出异常
            var exception = Record.Exception(() => pulse.TryComplete(out _));
            Assert.Null(exception);
        }

        [Fact]
        public void TryComplete_CalledTwice_SecondCallShouldReturnFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true);
            Thread.Sleep(50);

            // Act
            bool first = pulse.TryComplete(out _);
            bool second = pulse.TryComplete(out _);

            // Assert
            Assert.True(first);
            Assert.False(second);
        }

        #endregion

        #region Cancel 方法测试 - 验证取消逻辑

        [Fact]
        public void Cancel_WhenActive_ShouldDeactivate()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10000, true);

            // Act
            pulse.Cancel();

            // Assert
            Assert.False(pulse.IsActive);
        }

        [Fact]
        public void Cancel_WhenNotActive_ShouldDoNothing()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act & Assert - 不应该抛出异常
            var exception = Record.Exception(() => pulse.Cancel());
            Assert.Null(exception);
            Assert.False(pulse.IsActive);
        }

        [Fact]
        public void Cancel_ShouldNotTriggerCallback()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            bool callbackTriggered = false;
            pulse.Start(10000, true, _ => callbackTriggered = true);

            // Act
            pulse.Cancel();

            // Assert
            Assert.False(callbackTriggered);
        }

        [Fact]
        public void Cancel_ShouldClearCallback()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            bool callbackTriggered = false;
            pulse.Start(10, true, _ => callbackTriggered = true);

            // Act
            pulse.Cancel();
            Thread.Sleep(50);  // 等待原本应该完成的时间
            pulse.TryComplete(out _);  // 尝试完成（应该无效）

            // Assert
            Assert.False(callbackTriggered);
        }

        #endregion

        #region Reset 方法测试 - 验证重置逻辑

        [Fact]
        public void Reset_ShouldClearAllState()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(1000, true);

            // Act
            pulse.Reset();

            // Assert
            Assert.False(pulse.IsActive);
            Assert.Equal(0, pulse.DurationMs);
            Assert.False(pulse.EndValue);
            Assert.Equal(0, pulse.ElapsedMs);
        }

        [Fact]
        public void Reset_WhenNotActive_ShouldWork()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Act & Assert - 不应该抛出异常
            var exception = Record.Exception(() => pulse.Reset());
            Assert.Null(exception);
        }

        [Fact]
        public void Reset_AfterExpiry_ShouldClearState()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true);
            Thread.Sleep(50);

            // Act
            pulse.Reset();

            // Assert
            Assert.False(pulse.IsActive);
            Assert.False(pulse.IsExpired);
        }

        #endregion

        #region 时间属性测试 - 验证 ElapsedMs、RemainingMs、IsExpired

        [Fact]
        public void ElapsedMs_ShouldIncreaseOverTime()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(1000, true);

            // Act
            Thread.Sleep(50);
            long elapsed = pulse.ElapsedMs;

            // Assert
            Assert.True(elapsed >= 40);  // 允许一些误差
        }

        [Fact]
        public void RemainingMs_ShouldDecreaseOverTime()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(1000, true);
            long initialRemaining = pulse.RemainingMs;

            // Act
            Thread.Sleep(50);
            long laterRemaining = pulse.RemainingMs;

            // Assert
            Assert.True(laterRemaining < initialRemaining);
        }

        [Fact]
        public void RemainingMs_ShouldNotBeNegative()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true);
            Thread.Sleep(100);

            // Act
            long remaining = pulse.RemainingMs;

            // Assert
            Assert.True(remaining >= 0);
        }

        [Fact]
        public void IsExpired_WhenNotActive_ShouldBeFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);

            // Assert
            Assert.False(pulse.IsExpired);
        }

        [Fact]
        public void IsExpired_BeforeTimeout_ShouldBeFalse()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10000, true);

            // Assert
            Assert.False(pulse.IsExpired);
        }

        [Fact]
        public void IsExpired_AfterTimeout_ShouldBeTrue()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true);
            Thread.Sleep(50);

            // Assert
            Assert.True(pulse.IsExpired);
        }

        #endregion

        #region 线程安全测试 - 验证并发访问的安全性

        [Fact]
        public void Start_ConcurrentCalls_OnlyOneShouldSucceed()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            int successCount = 0;
            var start = new ManualResetEventSlim(false);

            // Act - 10 个线程同时尝试启动
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    start.Wait();
                    if (pulse.Start(1000, true))
                    {
                        Interlocked.Increment(ref successCount);
                    }
                });
            }

            start.Set();
            Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(5)));

            // Assert - 只有一个线程能成功
            Assert.Equal(1, successCount);
        }

        [Fact]
        public void TryComplete_ConcurrentCalls_OnlyOneShouldSucceed()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10, true);
            Thread.Sleep(50);  // 等待过期

            int successCount = 0;
            var start = new ManualResetEventSlim(false);

            // Act - 10 个线程同时尝试完成
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    start.Wait();
                    if (pulse.TryComplete(out _))
                    {
                        Interlocked.Increment(ref successCount);
                    }
                });
            }

            start.Set();
            Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(5)));

            // Assert - 只有一个线程能成功
            Assert.Equal(1, successCount);
        }

        [Fact]
        public void ConcurrentStartAndComplete_ShouldNotCorruptState()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            var cts = new CancellationTokenSource();
            int startCount = 0;
            int completeCount = 0;
            Exception? caughtException = null;

            // Act - 一个线程不断启动，另一个线程不断尝试完成
            var startTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        pulse.Start(5, true);
                        Interlocked.Increment(ref startCount);
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            var completeTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (pulse.TryComplete(out _))
                        {
                            Interlocked.Increment(ref completeCount);
                        }
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            // 运行 200ms
            Thread.Sleep(200);
            cts.Cancel();
            Assert.True(Task.WaitAll(new[] { startTask, completeTask }, TimeSpan.FromSeconds(5)));

            // Assert
            Assert.Null(caughtException);
            Assert.True(startCount > 0, "Start 应该执行了至少一次");
        }

        [Fact]
        public void ConcurrentCancel_ShouldNotThrow()
        {
            // Arrange
            var pulse = new PulseOperation(0);
            pulse.Start(10000, true);

            // Act - 10 个线程同时尝试取消
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => pulse.Cancel());
            }

            Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(5)));

            // Assert
            Assert.False(pulse.IsActive);
        }

        #endregion
    }

    /// <summary>
    /// PulseManager 脉冲管理器测试
    ///
    /// 测试目的：
    /// PulseManager 用于管理多个输出通道的脉冲操作。
    /// 在工控场景中，一个 IO 模块可能有多个输出需要独立的脉冲控制。
    ///
    /// 主要测试内容：
    /// 1. 基本功能 - 构造函数、索引器、Start、Cancel、Update
    /// 2. 边界条件 - 无效索引处理
    /// 3. 活跃计数 - ActiveCount、HasActivePulse
    /// 4. 批量操作 - CancelAll、Update 批量检查
    /// 5. 线程安全 - 并发操作多个通道
    /// </summary>
    public class PulseManagerTests
    {
        #region 构造函数和属性测试

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var manager = new PulseManager(10);

            // Assert
            Assert.Equal(10, manager.OutputCount);
            Assert.Equal(0, manager.ActiveCount);
            Assert.False(manager.HasActivePulse);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Constructor_WithDifferentCounts_ShouldWork(int count)
        {
            // Arrange & Act
            var manager = new PulseManager(count);

            // Assert
            Assert.Equal(count, manager.OutputCount);
        }

        [Fact]
        public void Indexer_ShouldReturnCorrectPulseOperation()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act
            var pulse = manager[5];

            // Assert
            Assert.NotNull(pulse);
            Assert.Equal(5, pulse.OutputIndex);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10)]
        [InlineData(100)]
        public void Indexer_WithInvalidIndex_ShouldThrow(int index)
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => manager[index]);
        }

        #endregion

        #region Start 方法测试

        [Fact]
        public void Start_WithValidIndex_ShouldStartPulse()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act
            bool result = manager.Start(5, 1000, true);

            // Assert
            Assert.True(result);
            Assert.True(manager[5].IsActive);
            Assert.Equal(1, manager.ActiveCount);
            Assert.True(manager.HasActivePulse);
        }

        [Fact]
        public void Start_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert
            Assert.False(manager.Start(-1, 1000, true));
            Assert.False(manager.Start(10, 1000, true));
            Assert.False(manager.Start(100, 1000, true));
        }

        [Fact]
        public void Start_MultiplePulses_ShouldIncrementActiveCount()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act
            manager.Start(0, 1000, true);
            manager.Start(1, 1000, true);
            manager.Start(2, 1000, true);

            // Assert
            Assert.Equal(3, manager.ActiveCount);
        }

        [Fact]
        public void Start_SamePulseTwice_ShouldNotIncrementActiveCount()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);

            // Act
            bool result = manager.Start(0, 2000, false);

            // Assert
            Assert.False(result);
            Assert.Equal(1, manager.ActiveCount);
        }

        [Fact]
        public void Start_WithTimeSpan_ShouldWork()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act
            bool result = manager.Start(0, TimeSpan.FromSeconds(2), true);

            // Assert
            Assert.True(result);
            Assert.Equal(2000, manager[0].DurationMs);
        }

        [Fact]
        public void Start_WithCallback_ShouldWork()
        {
            // Arrange
            var manager = new PulseManager(10);
            int callbackIndex = -1;

            // Act
            manager.Start(3, 10, true, idx => callbackIndex = idx);
            Thread.Sleep(50);
            manager.Update((idx, val) => { });

            // Assert
            Assert.Equal(3, callbackIndex);
        }

        #endregion

        #region ForceStart 方法测试

        [Fact]
        public void ForceStart_WhenNotActive_ShouldStartAndIncrementCount()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act
            manager.ForceStart(0, 1000, true);

            // Assert
            Assert.True(manager[0].IsActive);
            Assert.Equal(1, manager.ActiveCount);
        }

        [Fact]
        public void ForceStart_WhenAlreadyActive_ShouldOverrideButNotIncrementCount()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);

            // Act
            manager.ForceStart(0, 2000, false);

            // Assert
            Assert.True(manager[0].IsActive);
            Assert.Equal(2000, manager[0].DurationMs);
            Assert.Equal(1, manager.ActiveCount);  // 不应该增加
        }

        [Fact]
        public void ForceStart_WithInvalidIndex_ShouldDoNothing()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert - 不应该抛出异常
            var exception = Record.Exception(() => manager.ForceStart(-1, 1000, true));
            Assert.Null(exception);

            exception = Record.Exception(() => manager.ForceStart(100, 1000, true));
            Assert.Null(exception);
        }

        #endregion

        #region IsPulseActive 和 GetRemainingMs 测试

        [Fact]
        public void IsPulseActive_WhenActive_ShouldReturnTrue()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(5, 1000, true);

            // Act & Assert
            Assert.True(manager.IsPulseActive(5));
        }

        [Fact]
        public void IsPulseActive_WhenNotActive_ShouldReturnFalse()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert
            Assert.False(manager.IsPulseActive(5));
        }

        [Fact]
        public void IsPulseActive_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert
            Assert.False(manager.IsPulseActive(-1));
            Assert.False(manager.IsPulseActive(100));
        }

        [Fact]
        public void GetRemainingMs_ShouldReturnCorrectValue()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);

            // Act
            long remaining = manager.GetRemainingMs(0);

            // Assert
            Assert.True(remaining > 0 && remaining <= 1000);
        }

        [Fact]
        public void GetRemainingMs_WithInvalidIndex_ShouldReturnZero()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert
            Assert.Equal(0, manager.GetRemainingMs(-1));
            Assert.Equal(0, manager.GetRemainingMs(100));
        }

        #endregion

        #region Cancel 方法测试

        [Fact]
        public void Cancel_ShouldDeactivateAndDecrementCount()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);
            manager.Start(1, 1000, true);

            // Act
            manager.Cancel(0);

            // Assert
            Assert.False(manager[0].IsActive);
            Assert.True(manager[1].IsActive);
            Assert.Equal(1, manager.ActiveCount);
        }

        [Fact]
        public void Cancel_WhenNotActive_ShouldNotDecrementCount()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);

            // Act
            manager.Cancel(1);  // 取消未激活的脉冲

            // Assert
            Assert.Equal(1, manager.ActiveCount);  // 计数不变
        }

        [Fact]
        public void Cancel_WithInvalidIndex_ShouldDoNothing()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert - 不应该抛出异常
            var exception = Record.Exception(() => manager.Cancel(-1));
            Assert.Null(exception);

            exception = Record.Exception(() => manager.Cancel(100));
            Assert.Null(exception);
        }

        #endregion

        #region CancelAll 方法测试

        [Fact]
        public void CancelAll_ShouldCancelAllActivePulses()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);
            manager.Start(3, 1000, true);
            manager.Start(7, 1000, true);

            // Act
            manager.CancelAll();

            // Assert
            Assert.False(manager[0].IsActive);
            Assert.False(manager[3].IsActive);
            Assert.False(manager[7].IsActive);
            Assert.Equal(0, manager.ActiveCount);
            Assert.False(manager.HasActivePulse);
        }

        [Fact]
        public void CancelAll_WhenNoActivePulses_ShouldDoNothing()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act & Assert - 不应该抛出异常
            var exception = Record.Exception(() => manager.CancelAll());
            Assert.Null(exception);
            Assert.Equal(0, manager.ActiveCount);
        }

        #endregion

        #region Update 方法测试

        [Fact]
        public void Update_ShouldCompleteExpiredPulses()
        {
            // Arrange
            var manager = new PulseManager(10);
            bool output0Set = false;
            bool output0Value = false;
            bool output3Set = false;
            bool output3Value = false;

            manager.Start(0, 10, true);
            manager.Start(3, 10, false);
            Thread.Sleep(50);

            // Act
            manager.Update((idx, val) =>
            {
                if (idx == 0) { output0Set = true; output0Value = val; }
                if (idx == 3) { output3Set = true; output3Value = val; }
            });

            // Assert
            Assert.True(output0Set);
            Assert.True(output0Value);
            Assert.True(output3Set);
            Assert.False(output3Value);
            Assert.Equal(0, manager.ActiveCount);
        }

        [Fact]
        public void Update_WhenNoActivePulses_ShouldDoNothing()
        {
            // Arrange
            var manager = new PulseManager(10);
            int callCount = 0;

            // Act
            manager.Update((idx, val) => callCount++);

            // Assert
            Assert.Equal(0, callCount);
        }

        [Fact]
        public void Update_ShouldOnlyCompleteExpiredPulses()
        {
            // Arrange
            var manager = new PulseManager(10);
            int callCount = 0;

            manager.Start(0, 10, true);    // 会过期
            manager.Start(1, 10000, true); // 不会过期
            Thread.Sleep(50);

            // Act
            manager.Update((idx, val) => callCount++);

            // Assert
            Assert.Equal(1, callCount);  // 只有一个脉冲完成
            Assert.Equal(1, manager.ActiveCount);  // 还有一个活跃
        }

        [Fact]
        public void Update_ShouldDecrementActiveCount()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 10, true);
            manager.Start(1, 10, true);
            Thread.Sleep(50);

            // Act
            manager.Update((idx, val) => { });

            // Assert
            Assert.Equal(0, manager.ActiveCount);
        }

        #endregion

        #region 线程安全测试

        [Fact]
        public void ConcurrentStart_DifferentIndices_ShouldAllSucceed()
        {
            // Arrange
            var manager = new PulseManager(100);
            int successCount = 0;

            // Act
            Parallel.For(0, 100, i =>
            {
                if (manager.Start(i, 1000, true))
                {
                    Interlocked.Increment(ref successCount);
                }
            });

            // Assert
            Assert.Equal(100, successCount);
            Assert.Equal(100, manager.ActiveCount);
        }

        [Fact]
        public void ConcurrentStartAndUpdate_ShouldNotCorruptState()
        {
            // Arrange
            var manager = new PulseManager(10);
            var cts = new CancellationTokenSource();
            Exception? caughtException = null;

            // Act
            var startTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            manager.Start(i, 5, true);
                        }
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            var updateTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        manager.Update((idx, val) => { });
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            Thread.Sleep(200);
            cts.Cancel();
            Assert.True(Task.WaitAll(new[] { startTask, updateTask }, TimeSpan.FromSeconds(5)));

            // Assert
            Assert.Null(caughtException);
        }

        [Fact]
        public void ConcurrentCancelAll_ShouldNotThrow()
        {
            // Arrange
            var manager = new PulseManager(10);
            for (int i = 0; i < 10; i++)
            {
                manager.Start(i, 10000, true);
            }

            // Act
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => manager.CancelAll());
            }

            Assert.True(Task.WaitAll(tasks, TimeSpan.FromSeconds(5)));

            // Assert
            Assert.Equal(0, manager.ActiveCount);
        }

        #endregion

        #region 边界条件测试

        [Fact]
        public void ActiveCount_ShouldNeverBeNegative()
        {
            // Arrange
            var manager = new PulseManager(10);
            manager.Start(0, 1000, true);

            // Act - 多次取消同一个脉冲
            manager.Cancel(0);
            manager.Cancel(0);
            manager.Cancel(0);

            // Assert
            Assert.True(manager.ActiveCount >= 0);
        }

        [Fact]
        public void StartCancelRepeatedly_ShouldMaintainCorrectCount()
        {
            // Arrange
            var manager = new PulseManager(10);

            // Act
            for (int i = 0; i < 100; i++)
            {
                manager.Start(0, 1000, true);
                Assert.Equal(1, manager.ActiveCount);
                manager.Cancel(0);
                Assert.Equal(0, manager.ActiveCount);
            }

            // Assert
            Assert.Equal(0, manager.ActiveCount);
        }

        #endregion
    }
}
