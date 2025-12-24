using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EwanIO.Core.EdgeDetection;
using Xunit;

namespace EwanIO.Tests
{
    /// <summary>
    /// EdgeManager 边缘检测管理器测试
    ///
    /// 测试目的：
    /// EdgeManager 用于检测输入信号的上升沿（0→1）和下降沿（1→0）。
    /// 在工控场景中，边缘检测用于识别按钮按下/释放、传感器触发等瞬时事件。
    ///
    /// 主要测试内容：
    /// 1. 基本功能 - 上升沿/下降沿检测、首次更新不产生边沿
    /// 2. 边界条件 - 无效索引处理、null 数组处理
    /// 3. 线程安全 - 多线程并发读取、ReadAndClear 原子性
    /// 4. 清除功能 - ClearAll、Reset、单独清除
    ///
    /// 线程安全说明：
    /// - Update() 在 Tick 线程中调用
    /// - ReadAndClearRising/Falling 在业务线程中调用
    /// - 使用 Interlocked 和 Volatile 保证线程安全
    /// </summary>
    public class EdgeManagerTests
    {
        #region 基本功能测试 - 验证边缘检测的核心功能

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var manager = new EdgeManager(10);

            // Assert
            Assert.Equal(10, manager.InputCount);
        }

        [Fact]
        public void Update_FirstCall_ShouldNotGenerateEdges()
        {
            // Arrange
            var manager = new EdgeManager(4);
            bool[] states = { true, false, true, false };

            // Act
            manager.Update(states);

            // Assert - 首次调用不应产生边沿
            Assert.False(manager.PeekRising(0));
            Assert.False(manager.PeekRising(2));
            Assert.False(manager.PeekFalling(1));
            Assert.False(manager.PeekFalling(3));
        }

        [Fact]
        public void Update_RisingEdge_ShouldBeDetected()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act - 初始化
            manager.Update(new bool[] { false, false, false, false });

            // 产生上升沿
            manager.Update(new bool[] { true, false, true, false });

            // Assert
            Assert.True(manager.PeekRising(0));
            Assert.False(manager.PeekRising(1));
            Assert.True(manager.PeekRising(2));
            Assert.False(manager.PeekRising(3));
        }

        [Fact]
        public void Update_FallingEdge_ShouldBeDetected()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act - 初始化为 true
            manager.Update(new bool[] { true, true, true, true });

            // 产生下降沿
            manager.Update(new bool[] { false, true, false, true });

            // Assert
            Assert.True(manager.PeekFalling(0));
            Assert.False(manager.PeekFalling(1));
            Assert.True(manager.PeekFalling(2));
            Assert.False(manager.PeekFalling(3));
        }

        [Fact]
        public void ReadAndClearRising_ShouldClearAfterRead()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { false, false, false, false });
            manager.Update(new bool[] { true, false, false, false });

            // Act & Assert
            Assert.True(manager.ReadAndClearRising(0));  // 第一次读取
            Assert.False(manager.ReadAndClearRising(0)); // 第二次读取，已清除
        }

        [Fact]
        public void ReadAndClearFalling_ShouldClearAfterRead()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { true, true, true, true });
            manager.Update(new bool[] { false, true, true, true });

            // Act & Assert
            Assert.True(manager.ReadAndClearFalling(0));  // 第一次读取
            Assert.False(manager.ReadAndClearFalling(0)); // 第二次读取，已清除
        }

        [Fact]
        public void PeekRising_ShouldNotClear()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { false, false, false, false });
            manager.Update(new bool[] { true, false, false, false });

            // Act & Assert
            Assert.True(manager.PeekRising(0));
            Assert.True(manager.PeekRising(0));  // 多次 Peek 不清除
            Assert.True(manager.PeekRising(0));
        }

        [Fact]
        public void PeekFalling_ShouldNotClear()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { true, true, true, true });
            manager.Update(new bool[] { false, true, true, true });

            // Act & Assert
            Assert.True(manager.PeekFalling(0));
            Assert.True(manager.PeekFalling(0));  // 多次 Peek 不清除
            Assert.True(manager.PeekFalling(0));
        }

        #endregion

        #region 边界条件测试 - 验证无效索引和异常输入的处理

        [Fact]
        public void ReadAndClearRising_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            Assert.False(manager.ReadAndClearRising(-1));
            Assert.False(manager.ReadAndClearRising(4));
            Assert.False(manager.ReadAndClearRising(100));
        }

        [Fact]
        public void ReadAndClearFalling_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            Assert.False(manager.ReadAndClearFalling(-1));
            Assert.False(manager.ReadAndClearFalling(4));
            Assert.False(manager.ReadAndClearFalling(100));
        }

        [Fact]
        public void PeekRising_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            Assert.False(manager.PeekRising(-1));
            Assert.False(manager.PeekRising(4));
            Assert.False(manager.PeekRising(100));
        }

        [Fact]
        public void PeekFalling_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            Assert.False(manager.PeekFalling(-1));
            Assert.False(manager.PeekFalling(4));
            Assert.False(manager.PeekFalling(100));
        }

        [Fact]
        public void ClearRising_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            var ex = Record.Exception(() => manager.ClearRising(-1));
            Assert.Null(ex);

            ex = Record.Exception(() => manager.ClearRising(100));
            Assert.Null(ex);
        }

        [Fact]
        public void ClearFalling_WithInvalidIndex_ShouldNotThrow()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            var ex = Record.Exception(() => manager.ClearFalling(-1));
            Assert.Null(ex);

            ex = Record.Exception(() => manager.ClearFalling(100));
            Assert.Null(ex);
        }

        [Fact]
        public void Update_WithNullArray_ShouldNotThrow()
        {
            // Arrange
            var manager = new EdgeManager(4);

            // Act & Assert
            var ex = Record.Exception(() => manager.Update((bool[])null));
            Assert.Null(ex);
        }

        [Fact]
        public void Update_WithSmallerArray_ShouldNotThrow()
        {
            // Arrange
            var manager = new EdgeManager(10);

            // Act & Assert - 数组比 inputCount 小
            var ex = Record.Exception(() => manager.Update(new bool[] { true, false }));
            Assert.Null(ex);
        }

        [Fact]
        public void Update_WithFuncDelegate_ShouldWork()
        {
            // Arrange
            var manager = new EdgeManager(4);
            bool[] states = { false, true, false, true };

            // Act
            manager.Update(i => states[i]);
            states = new bool[] { true, false, true, false };
            manager.Update(i => states[i]);

            // Assert
            Assert.True(manager.PeekRising(0));
            Assert.True(manager.PeekRising(2));
            Assert.True(manager.PeekFalling(1));
            Assert.True(manager.PeekFalling(3));
        }

        #endregion

        #region ClearAll 和 Reset 测试 - 验证边沿清除和重置功能

        [Fact]
        public void ClearAll_ShouldClearAllEdges()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { false, true, false, true });
            manager.Update(new bool[] { true, false, true, false });

            // 确认有边沿
            Assert.True(manager.PeekRising(0));
            Assert.True(manager.PeekRising(2));
            Assert.True(manager.PeekFalling(1));
            Assert.True(manager.PeekFalling(3));

            // Act
            manager.ClearAll();

            // Assert
            Assert.False(manager.PeekRising(0));
            Assert.False(manager.PeekRising(2));
            Assert.False(manager.PeekFalling(1));
            Assert.False(manager.PeekFalling(3));
        }

        [Fact]
        public void Reset_ShouldResetToInitialState()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { false, true, false, true });
            manager.Update(new bool[] { true, false, true, false });

            // Act
            manager.Reset();

            // 再次更新应该不产生边沿（因为重置了初始化状态）
            manager.Update(new bool[] { true, true, true, true });

            // Assert - Reset 后第一次 Update 不产生边沿
            Assert.False(manager.PeekRising(0));
            Assert.False(manager.PeekRising(1));
        }

        [Fact]
        public void ClearRising_ShouldClearSpecificEdge()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { false, false, false, false });
            manager.Update(new bool[] { true, true, false, false });

            // Act
            manager.ClearRising(0);

            // Assert
            Assert.False(manager.PeekRising(0));  // 已清除
            Assert.True(manager.PeekRising(1));   // 未清除
        }

        [Fact]
        public void ClearFalling_ShouldClearSpecificEdge()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { true, true, true, true });
            manager.Update(new bool[] { false, false, true, true });

            // Act
            manager.ClearFalling(0);

            // Assert
            Assert.False(manager.PeekFalling(0));  // 已清除
            Assert.True(manager.PeekFalling(1));   // 未清除
        }

        #endregion

        #region 线程安全测试 - 验证多线程并发访问的安全性

        [Fact]
        public void ReadAndClearRising_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var manager = new EdgeManager(100);
            manager.Update(new bool[100]);  // 初始化

            // 产生上升沿
            bool[] allTrue = new bool[100];
            for (int i = 0; i < 100; i++) allTrue[i] = true;
            manager.Update(allTrue);

            int successCount = 0;

            // Act - 多线程并发读取并清除
            Parallel.For(0, 100, i =>
            {
                if (manager.ReadAndClearRising(i))
                {
                    Interlocked.Increment(ref successCount);
                }
            });

            // Assert - 每个边沿只能被读取一次
            Assert.Equal(100, successCount);

            // 再次读取应该全部为 false
            for (int i = 0; i < 100; i++)
            {
                Assert.False(manager.PeekRising(i));
            }
        }

        [Fact]
        public void ReadAndClearFalling_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Arrange
            var manager = new EdgeManager(100);
            bool[] allTrue = new bool[100];
            for (int i = 0; i < 100; i++) allTrue[i] = true;
            manager.Update(allTrue);  // 初始化为全 true

            // 产生下降沿
            manager.Update(new bool[100]);

            int successCount = 0;

            // Act - 多线程并发读取并清除
            Parallel.For(0, 100, i =>
            {
                if (manager.ReadAndClearFalling(i))
                {
                    Interlocked.Increment(ref successCount);
                }
            });

            // Assert - 每个边沿只能被读取一次
            Assert.Equal(100, successCount);

            // 再次读取应该全部为 false
            for (int i = 0; i < 100; i++)
            {
                Assert.False(manager.PeekFalling(i));
            }
        }

        [Fact]
        public void ConcurrentUpdateAndRead_ShouldNotCorruptState()
        {
            // Arrange
            var manager = new EdgeManager(10);
            manager.Update(new bool[10]);  // 初始化

            var cts = new CancellationTokenSource();
            int updateCount = 0;
            int readCount = 0;
            Exception caughtException = null;

            // Act - 一个线程更新，多个线程读取
            var updateTask = Task.Run(() =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        bool[] states = new bool[10];
                        for (int i = 0; i < 10; i++)
                        {
                            states[i] = (updateCount % 2) == 0;
                        }
                        manager.Update(states);
                        Interlocked.Increment(ref updateCount);
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            var readTasks = new Task[4];
            for (int t = 0; t < 4; t++)
            {
                readTasks[t] = Task.Run(() =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            for (int i = 0; i < 10; i++)
                            {
                                manager.PeekRising(i);
                                manager.PeekFalling(i);
                                manager.ReadAndClearRising(i);
                                manager.ReadAndClearFalling(i);
                            }
                            Interlocked.Increment(ref readCount);
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

            Task.WaitAll(new[] { updateTask }.Concat(readTasks).ToArray());

            // Assert - 应该没有异常发生，且完成了更新和读取
            Assert.Null(caughtException);
            Assert.True(updateCount > 0, "Update 应该执行了至少一次");
            Assert.True(readCount > 0, "Read 应该执行了至少一次");
        }

        [Fact]
        public void MultipleThreadsReadingSameEdge_OnlyOneShouldSucceed()
        {
            // Arrange
            var manager = new EdgeManager(1);
            manager.Update(new bool[] { false });
            manager.Update(new bool[] { true });  // 产生一个上升沿

            int successCount = 0;
            var barrier = new Barrier(10);

            // Act - 10 个线程同时尝试读取同一个边沿
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();  // 同步所有线程
                    if (manager.ReadAndClearRising(0))
                    {
                        Interlocked.Increment(ref successCount);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert - 只有一个线程能成功读取
            Assert.Equal(1, successCount);
        }

        #endregion

        #region 连续边沿测试 - 验证快速状态切换的边沿检测

        [Fact]
        public void RapidToggle_ShouldDetectAllEdges()
        {
            // Arrange
            var manager = new EdgeManager(1);
            manager.Update(new bool[] { false });  // 初始化

            int risingCount = 0;
            int fallingCount = 0;

            // Act - 快速切换
            for (int i = 0; i < 10; i++)
            {
                manager.Update(new bool[] { true });
                if (manager.ReadAndClearRising(0)) risingCount++;

                manager.Update(new bool[] { false });
                if (manager.ReadAndClearFalling(0)) fallingCount++;
            }

            // Assert
            Assert.Equal(10, risingCount);
            Assert.Equal(10, fallingCount);
        }

        [Fact]
        public void NoChange_ShouldNotGenerateEdges()
        {
            // Arrange
            var manager = new EdgeManager(4);
            manager.Update(new bool[] { true, false, true, false });

            // Act - 多次更新相同状态
            for (int i = 0; i < 10; i++)
            {
                manager.Update(new bool[] { true, false, true, false });
            }

            // Assert - 不应产生新边沿
            Assert.False(manager.PeekRising(0));
            Assert.False(manager.PeekRising(1));
            Assert.False(manager.PeekFalling(0));
            Assert.False(manager.PeekFalling(1));
        }

        #endregion
    }
}
