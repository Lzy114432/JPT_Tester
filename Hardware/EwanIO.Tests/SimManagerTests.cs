using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EwanIO.Core.Simulation;
using Xunit;

namespace EwanIO.Tests
{
    /// <summary>
    /// SimManager 模拟管理器测试
    ///
    /// 测试目的：
    /// SimManager 用于模拟覆盖输入信号（ForceOn/ForceOff）。
    /// 在调试场景中，可以强制某个输入为 ON 或 OFF，不受硬件实际状态影响。
    ///
    /// 主要测试内容：
    /// 1. 基本功能 - ForceOn/ForceOff/ClearSimulate
    /// 2. 边界条件 - 无效索引处理
    /// 3. 线程安全 - 多线程并发设置模拟
    /// 4. 计数器 - SimulatedCount 正确性
    /// </summary>
    public class SimManagerTests
    {
        #region 基本功能测试

        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            var manager = new SimManager(10);

            Assert.Equal(10, manager.Count);
            Assert.Equal(0, manager.SimulatedCount);
        }

        [Fact]
        public void ForceOn_ShouldSetModeToForceOn()
        {
            var manager = new SimManager(4);

            manager.ForceOn(0);

            Assert.Equal(SimMode.ForceOn, manager.GetMode(0));
            Assert.Equal(1, manager.SimulatedCount);
        }

        [Fact]
        public void ForceOff_ShouldSetModeToForceOff()
        {
            var manager = new SimManager(4);

            manager.ForceOff(1);

            Assert.Equal(SimMode.ForceOff, manager.GetMode(1));
            Assert.Equal(1, manager.SimulatedCount);
        }

        [Fact]
        public void ClearSimulate_ShouldSetModeToNone()
        {
            var manager = new SimManager(4);
            manager.ForceOn(0);
            Assert.Equal(1, manager.SimulatedCount);

            manager.ClearSimulate(0);

            Assert.Equal(SimMode.None, manager.GetMode(0));
            Assert.Equal(0, manager.SimulatedCount);
        }

        [Fact]
        public void ClearAll_ShouldClearAllSimulations()
        {
            var manager = new SimManager(4);
            manager.ForceOn(0);
            manager.ForceOff(1);
            manager.ForceOn(2);
            Assert.Equal(3, manager.SimulatedCount);

            manager.ClearAll();

            Assert.Equal(SimMode.None, manager.GetMode(0));
            Assert.Equal(SimMode.None, manager.GetMode(1));
            Assert.Equal(SimMode.None, manager.GetMode(2));
            Assert.Equal(0, manager.SimulatedCount);
        }

        [Fact]
        public void IsSimulated_ShouldReturnCorrectValue()
        {
            var manager = new SimManager(4);

            Assert.False(manager.IsSimulated(0));

            manager.ForceOn(0);
            Assert.True(manager.IsSimulated(0));

            manager.ClearSimulate(0);
            Assert.False(manager.IsSimulated(0));
        }

        [Fact]
        public void TryGetSimulatedValue_ShouldReturnCorrectValue()
        {
            var manager = new SimManager(4);

            // 无模拟时
            Assert.False(manager.TryGetSimulatedValue(0, out bool value));

            // ForceOn
            manager.ForceOn(0);
            Assert.True(manager.TryGetSimulatedValue(0, out value));
            Assert.True(value);

            // ForceOff
            manager.ForceOff(1);
            Assert.True(manager.TryGetSimulatedValue(1, out value));
            Assert.False(value);
        }

        [Fact]
        public void ApplySimulate_ShouldOverrideHardwareValue()
        {
            var manager = new SimManager(4);

            // 无模拟时，返回硬件值
            Assert.False(manager.ApplySimulate(0, false));
            Assert.True(manager.ApplySimulate(0, true));

            // ForceOn 强制为 true
            manager.ForceOn(0);
            Assert.True(manager.ApplySimulate(0, false));
            Assert.True(manager.ApplySimulate(0, true));

            // ForceOff 强制为 false
            manager.ForceOff(1);
            Assert.False(manager.ApplySimulate(1, false));
            Assert.False(manager.ApplySimulate(1, true));

            // 未模拟的索引不受影响
            Assert.True(manager.ApplySimulate(2, true));
            Assert.False(manager.ApplySimulate(2, false));
        }

        [Fact]
        public void SetSimulate_SameModeTwice_ShouldNotChangeCount()
        {
            var manager = new SimManager(4);

            manager.ForceOn(0);
            Assert.Equal(1, manager.SimulatedCount);

            manager.ForceOn(0); // 再次设置相同模式
            Assert.Equal(1, manager.SimulatedCount);

            manager.SetSimulate(0, SimMode.ForceOff); // 切换模式
            Assert.Equal(1, manager.SimulatedCount);
        }

        #endregion

        #region 边界条件测试

        [Fact]
        public void GetMode_WithInvalidIndex_ShouldReturnNone()
        {
            var manager = new SimManager(4);

            Assert.Equal(SimMode.None, manager.GetMode(-1));
            Assert.Equal(SimMode.None, manager.GetMode(4));
            Assert.Equal(SimMode.None, manager.GetMode(100));
        }

        [Fact]
        public void ForceOn_WithInvalidIndex_ShouldNotThrow()
        {
            var manager = new SimManager(4);

            var ex = Record.Exception(() => manager.ForceOn(-1));
            Assert.Null(ex);

            ex = Record.Exception(() => manager.ForceOn(100));
            Assert.Null(ex);

            Assert.Equal(0, manager.SimulatedCount);
        }

        [Fact]
        public void IsSimulated_WithInvalidIndex_ShouldReturnFalse()
        {
            var manager = new SimManager(4);

            Assert.False(manager.IsSimulated(-1));
            Assert.False(manager.IsSimulated(100));
        }

        [Fact]
        public void TryGetSimulatedValue_WithInvalidIndex_ShouldReturnFalse()
        {
            var manager = new SimManager(4);

            Assert.False(manager.TryGetSimulatedValue(-1, out _));
            Assert.False(manager.TryGetSimulatedValue(100, out _));
        }

        [Fact]
        public void ApplySimulate_WithInvalidIndex_ShouldReturnHardwareValue()
        {
            var manager = new SimManager(4);
            manager.ForceOn(0); // 确保有模拟存在

            Assert.True(manager.ApplySimulate(-1, true));
            Assert.False(manager.ApplySimulate(-1, false));
            Assert.True(manager.ApplySimulate(100, true));
        }

        #endregion

        #region 线程安全测试

        [Fact]
        public void ConcurrentSetSimulate_ShouldBeThreadSafe()
        {
            var manager = new SimManager(100);
            Exception? caughtException = null;

            // 100 个线程同时设置不同索引
            Parallel.For(0, 100, i =>
            {
                try
                {
                    manager.ForceOn(i);
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            Assert.Null(caughtException);
            Assert.Equal(100, manager.SimulatedCount);
        }

        [Fact]
        public void ConcurrentSetAndClear_ShouldMaintainCorrectCount()
        {
            var manager = new SimManager(10);
            var cts = new CancellationTokenSource();
            int setCount = 0;
            int clearCount = 0;
            Exception? caughtException = null;

            // 多线程设置和清除
            var tasks = new Task[4];

            // 设置线程
            for (int t = 0; t < 2; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        var random = new Random();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            int idx = random.Next(10);
                            manager.ForceOn(idx);
                            Interlocked.Increment(ref setCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                    }
                });
            }

            // 清除线程
            for (int t = 2; t < 4; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    try
                    {
                        var random = new Random();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            int idx = random.Next(10);
                            manager.ClearSimulate(idx);
                            Interlocked.Increment(ref clearCount);
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
            Task.WaitAll(tasks);

            Assert.Null(caughtException);
            Assert.True(setCount > 0);
            Assert.True(clearCount > 0);
            // SimulatedCount 应该在 0-10 之间
            Assert.InRange(manager.SimulatedCount, 0, 10);
        }

        [Fact]
        public void ConcurrentReadAndWrite_ShouldNotCorruptState()
        {
            var manager = new SimManager(10);
            var cts = new CancellationTokenSource();
            int readCount = 0;
            int writeCount = 0;
            Exception? caughtException = null;

            // 写线程
            var writeTask = Task.Run(() =>
            {
                try
                {
                    var random = new Random();
                    while (!cts.Token.IsCancellationRequested)
                    {
                        int idx = random.Next(10);
                        var mode = (SimMode)random.Next(3);
                        manager.SetSimulate(idx, mode);
                        Interlocked.Increment(ref writeCount);
                    }
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
            });

            // 读线程
            var readTasks = new Task[3];
            for (int t = 0; t < 3; t++)
            {
                readTasks[t] = Task.Run(() =>
                {
                    try
                    {
                        var random = new Random();
                        while (!cts.Token.IsCancellationRequested)
                        {
                            int idx = random.Next(10);
                            _ = manager.GetMode(idx);
                            _ = manager.IsSimulated(idx);
                            _ = manager.TryGetSimulatedValue(idx, out _);
                            _ = manager.ApplySimulate(idx, true);
                            _ = manager.SimulatedCount;
                            Interlocked.Increment(ref readCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                    }
                });
            }

            Thread.Sleep(200);
            cts.Cancel();
            Task.WaitAll(new[] { writeTask }.Concat(readTasks).ToArray());

            Assert.Null(caughtException);
            Assert.True(writeCount > 0);
            Assert.True(readCount > 0);
        }

        [Fact]
        public void MultipleThreadsSetSameIndex_ShouldNotCorruptCount()
        {
            // 测试多线程竞争同一索引时计数器的正确性
            for (int round = 0; round < 10; round++)
            {
                var manager = new SimManager(1);
                var barrier = new Barrier(10);

                // 10 个线程同时设置索引 0
                var tasks = new Task[10];
                for (int i = 0; i < 10; i++)
                {
                    int threadId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        if (threadId % 2 == 0)
                            manager.ForceOn(0);
                        else
                            manager.ForceOff(0);
                    });
                }

                Task.WaitAll(tasks);

                // 最终计数应该是 0 或 1
                Assert.InRange(manager.SimulatedCount, 0, 1);

                // 如果有模拟，计数应该是 1
                if (manager.GetMode(0) != SimMode.None)
                {
                    Assert.Equal(1, manager.SimulatedCount);
                }
                else
                {
                    Assert.Equal(0, manager.SimulatedCount);
                }
            }
        }

        #endregion

        #region 快路径测试

        [Fact]
        public void ApplySimulate_NoSimulation_ShouldUseFastPath()
        {
            var manager = new SimManager(1000);

            // 无模拟时，快路径直接返回硬件值
            for (int i = 0; i < 1000; i++)
            {
                Assert.True(manager.ApplySimulate(i, true));
                Assert.False(manager.ApplySimulate(i, false));
            }
        }

        [Fact]
        public void ApplySimulate_WithSimulation_ShouldCheckMode()
        {
            var manager = new SimManager(1000);
            manager.ForceOn(500);

            // 有模拟时，需要检查每个索引的模式
            Assert.True(manager.ApplySimulate(500, false)); // ForceOn 覆盖
            Assert.False(manager.ApplySimulate(499, false)); // 无覆盖
        }

        #endregion

        #region 集成测试 - SimManager + EdgeManager 协作

        /// <summary>
        /// 验证 SimManager 的模拟值变化能被 EdgeManager 正确检测到边沿
        /// 这是修复后的关键测试：SimManager 不再自己做边缘检测，而是由 EdgeManager 统一处理
        /// </summary>
        [Fact]
        public void SimManager_WithEdgeManager_ShouldGenerateEdges()
        {
            // Arrange
            var simManager = new SimManager(4);
            var edgeManager = new EwanIO.Core.EdgeDetection.EdgeManager(4);

            // 模拟 Tick 循环中的数据流：硬件值 → SimManager → EdgeManager
            bool[] hardwareValues = { false, false, false, false };

            // 初始化 EdgeManager（首次 Update 不产生边沿）
            bool[] logicalValues = new bool[4];
            for (int i = 0; i < 4; i++)
                logicalValues[i] = simManager.ApplySimulate(i, hardwareValues[i]);
            edgeManager.Update(logicalValues);

            // Act 1: ForceOn 产生上升沿
            simManager.ForceOn(0);
            for (int i = 0; i < 4; i++)
                logicalValues[i] = simManager.ApplySimulate(i, hardwareValues[i]);
            edgeManager.Update(logicalValues);

            // Assert 1: 应检测到上升沿
            Assert.True(edgeManager.ReadAndClearRising(0), "ForceOn 应产生上升沿");
            Assert.False(edgeManager.PeekFalling(0), "ForceOn 不应产生下降沿");

            // Act 2: ForceOff 产生下降沿
            simManager.ForceOff(0);
            for (int i = 0; i < 4; i++)
                logicalValues[i] = simManager.ApplySimulate(i, hardwareValues[i]);
            edgeManager.Update(logicalValues);

            // Assert 2: 应检测到下降沿
            Assert.True(edgeManager.ReadAndClearFalling(0), "ForceOff 应产生下降沿");
            Assert.False(edgeManager.PeekRising(0), "ForceOff 不应产生上升沿");

            // Act 3: ClearSimulate 回到硬件值（false），不应产生新边沿（已经是 false）
            simManager.ClearSimulate(0);
            for (int i = 0; i < 4; i++)
                logicalValues[i] = simManager.ApplySimulate(i, hardwareValues[i]);
            edgeManager.Update(logicalValues);

            // Assert 3: 状态未变化，不应产生边沿
            Assert.False(edgeManager.PeekRising(0), "ClearSimulate 后状态未变，不应产生上升沿");
            Assert.False(edgeManager.PeekFalling(0), "ClearSimulate 后状态未变，不应产生下降沿");
        }

        /// <summary>
        /// 验证 SimManager 快速切换时 EdgeManager 能检测到所有边沿
        /// </summary>
        [Fact]
        public void SimManager_RapidToggle_EdgeManagerShouldDetectAllEdges()
        {
            var simManager = new SimManager(1);
            var edgeManager = new EwanIO.Core.EdgeDetection.EdgeManager(1);

            bool[] logicalValues = new bool[1];

            // 初始化
            logicalValues[0] = simManager.ApplySimulate(0, false);
            edgeManager.Update(logicalValues);

            int risingCount = 0;
            int fallingCount = 0;

            // 快速切换 10 次
            for (int i = 0; i < 10; i++)
            {
                // ON
                simManager.ForceOn(0);
                logicalValues[0] = simManager.ApplySimulate(0, false);
                edgeManager.Update(logicalValues);
                if (edgeManager.ReadAndClearRising(0)) risingCount++;

                // OFF
                simManager.ForceOff(0);
                logicalValues[0] = simManager.ApplySimulate(0, false);
                edgeManager.Update(logicalValues);
                if (edgeManager.ReadAndClearFalling(0)) fallingCount++;
            }

            Assert.Equal(10, risingCount);
            Assert.Equal(10, fallingCount);
        }

        /// <summary>
        /// 验证 ClearSimulate 不会清除已经产生的边沿
        /// 边沿一旦产生，只能通过 EdgeManager.ReadAndClear 或 ClearAll 清除
        /// </summary>
        [Fact]
        public void SimManager_ClearSimulate_ShouldNotClearExistingEdges()
        {
            var simManager = new SimManager(1);
            var edgeManager = new EwanIO.Core.EdgeDetection.EdgeManager(1);

            bool[] logicalValues = new bool[1];

            // 初始化
            logicalValues[0] = simManager.ApplySimulate(0, false);
            edgeManager.Update(logicalValues);

            // ForceOn 产生上升沿
            simManager.ForceOn(0);
            logicalValues[0] = simManager.ApplySimulate(0, false);
            edgeManager.Update(logicalValues);

            // 确认上升沿存在
            Assert.True(edgeManager.PeekRising(0), "ForceOn 后应有上升沿");

            // 清除模拟（不调用 Tick/Update）
            simManager.ClearSimulate(0);

            // 边沿应该仍然存在（因为还没有新的 Update 调用）
            Assert.True(edgeManager.PeekRising(0), "ClearSimulate 不应清除已存在的边沿");

            // 只有 ReadAndClear 才会清除
            Assert.True(edgeManager.ReadAndClearRising(0), "ReadAndClear 应返回 true");
            Assert.False(edgeManager.PeekRising(0), "ReadAndClear 后边沿应被清除");
        }

        /// <summary>
        /// 验证 ClearSimulate 后的下一次 Tick 可能产生新边沿（取决于硬件值）
        /// </summary>
        [Fact]
        public void SimManager_ClearSimulate_MayGenerateNewEdge_DependingOnHardwareValue()
        {
            var simManager = new SimManager(1);
            var edgeManager = new EwanIO.Core.EdgeDetection.EdgeManager(1);

            bool[] logicalValues = new bool[1];

            // 场景 1: 硬件值=false, ForceOn, ClearSimulate → 产生下降沿
            logicalValues[0] = simManager.ApplySimulate(0, false);
            edgeManager.Update(logicalValues); // 初始化: false

            simManager.ForceOn(0);
            logicalValues[0] = simManager.ApplySimulate(0, false);
            edgeManager.Update(logicalValues); // 变为 true，产生上升沿
            edgeManager.ReadAndClearRising(0); // 清除上升沿

            simManager.ClearSimulate(0);
            logicalValues[0] = simManager.ApplySimulate(0, false); // 回到硬件值 false
            edgeManager.Update(logicalValues); // true → false，产生下降沿

            Assert.True(edgeManager.ReadAndClearFalling(0), "ClearSimulate 后应产生下降沿（true→false）");

            // 场景 2: 硬件值=true, ForceOn, ClearSimulate → 不产生边沿
            // 需要 Reset 而非 ClearAll，因为 ClearAll 不会重置 _initialized
            edgeManager.Reset();
            simManager.ClearAll();

            bool hardwareValue = true;
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue);
            edgeManager.Update(logicalValues); // 初始化: true（首次 Update 不产生边沿）

            simManager.ForceOn(0);
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue);
            edgeManager.Update(logicalValues); // 仍为 true，无边沿

            Assert.False(edgeManager.PeekRising(0), "ForceOn 但值未变，不应产生边沿");

            simManager.ClearSimulate(0);
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue); // 仍为 true
            edgeManager.Update(logicalValues);

            Assert.False(edgeManager.PeekRising(0), "ClearSimulate 后值仍为 true，不应产生边沿");
            Assert.False(edgeManager.PeekFalling(0));
        }

        /// <summary>
        /// 验证当硬件值变化时，SimManager 覆盖仍然有效
        /// </summary>
        [Fact]
        public void SimManager_HardwareChange_ShouldNotAffectForcedValue()
        {
            var simManager = new SimManager(1);
            var edgeManager = new EwanIO.Core.EdgeDetection.EdgeManager(1);

            bool[] logicalValues = new bool[1];
            bool hardwareValue = false;

            // 初始化
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue);
            edgeManager.Update(logicalValues);

            // 强制为 ON
            simManager.ForceOn(0);
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue);
            edgeManager.Update(logicalValues);
            Assert.True(edgeManager.ReadAndClearRising(0));

            // 硬件值变为 true，但 ForceOn 仍覆盖，逻辑值不变
            hardwareValue = true;
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue);
            edgeManager.Update(logicalValues);
            Assert.False(edgeManager.PeekRising(0), "硬件值变化但被覆盖，不应产生边沿");
            Assert.False(edgeManager.PeekFalling(0));

            // 硬件值变为 false，ForceOn 仍覆盖
            hardwareValue = false;
            logicalValues[0] = simManager.ApplySimulate(0, hardwareValue);
            edgeManager.Update(logicalValues);
            Assert.False(edgeManager.PeekRising(0));
            Assert.False(edgeManager.PeekFalling(0));
        }

        #endregion
    }
}
