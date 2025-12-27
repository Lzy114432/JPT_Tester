using Ewan.Core.IO;
using Ewan.Core.Logic;
using Ewan.Core.Module;
using EwanCore.StateMachine;
using System;
using System.Threading;
using Xunit;

namespace Ewan.Core.Tests.Logic
{
    /// <summary>
    /// HomeLogic 单元测试
    /// 验证复位流程状态机的状态转换和非阻塞行为
    /// </summary>
    public class HomeLogicTests
    {
        #region 构造函数测试

        [Fact]
        public void Constructor_InitializesWithDefaultState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();

            // Act
            var logic = new HomeLogic(sharedState, binElevator);

            // Assert
            Assert.Equal("初始状态", logic.SwitchIndex);
            Assert.False(logic.IsFinish);
        }

        #endregion

        #region 状态转换测试

        [Fact]
        public void Handler_FromInitialState_TransitionsToStopOn()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act
            logic.Handler();

            // Assert
            Assert.Equal("停止ON", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_FromStopOn_TransitionsToStopOnWait()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act - 进入 "停止ON" 状态
            logic.Handler(); // 初始状态 -> 停止ON
            Assert.Equal("停止ON", logic.SwitchIndex);

            logic.Handler(); // 停止ON -> 停止ON等待

            // Assert
            Assert.Equal("停止ON等待", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_StopOnWait_TransitionsAfterTimeout()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // 进入等待状态
            logic.Handler(); // 初始状态 -> 停止ON
            logic.Handler(); // 停止ON -> 停止ON等待
            Assert.Equal("停止ON等待", logic.SwitchIndex);

            // 第一次调用 StartCheckIsTimeout 会启动计时器
            logic.Handler(); // 应该仍在等待
            Assert.Equal("停止ON等待", logic.SwitchIndex);

            // 等待足够长时间让超时触发（STOP_ON_DELAY = 500ms）
            Thread.Sleep(1000);
            logic.Handler();

            // Assert
            Assert.Equal("停止OFF", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_StopOff_TransitionsToStopOffWait()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // 使用辅助方法推进到 "停止OFF" 状态
            RunToState(logic, "停止OFF");
            Assert.Equal("停止OFF", logic.SwitchIndex);

            // Act
            logic.Handler(); // 停止OFF -> 停止OFF等待

            // Assert
            Assert.Equal("停止OFF等待", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_CompleteFullCycle_ReachesFinish()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act - 运行完整周期
            RunHomeLogicToCompletion(logic);

            // Assert
            Assert.True(logic.IsFinish);
            Assert.Equal("结束状态", logic.SwitchIndex);
        }

        #endregion

        #region 非阻塞行为测试

        [Fact]
        public void Handler_IsNonBlocking()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act - 测量单次 Handler 调用时间
            var startTime = DateTime.Now;
            logic.Handler();
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            // Assert - Handler 应该快速返回（不阻塞）
            Assert.True(elapsed < 100, $"Handler took {elapsed}ms, expected < 100ms");
        }

        [Fact]
        public void Handler_WaitStates_ReturnImmediatelyWhenNotTimedOut()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // 进入等待状态
            logic.Handler(); // 初始状态 -> 停止ON
            logic.Handler(); // 停止ON -> 停止ON等待

            // Act - 立即再次调用（未超时）
            var startTime = DateTime.Now;
            logic.Handler();
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            // Assert - 应该快速返回，状态不变
            Assert.True(elapsed < 50, $"Handler took {elapsed}ms during wait state");
            Assert.Equal("停止ON等待", logic.SwitchIndex);
        }

        #endregion

        #region BinElevator 交互测试

        [Fact]
        public void Handler_CallsPerformHardwareInitialization()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act - 运行到料仓初始化步骤
            RunToState(logic, "料仓初始化");
            logic.Handler();

            // Assert
            Assert.True(binElevator.PerformHardwareInitializationCalled);
        }

        #endregion

        #region Rset 测试

        [Fact]
        public void Rset_ResetsToInitialState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // 推进到某个中间状态
            logic.Handler();
            logic.Handler();
            Assert.NotEqual("初始状态", logic.SwitchIndex);

            // Act
            logic.Rset();

            // Assert
            Assert.Equal("初始状态", logic.SwitchIndex);
            Assert.False(logic.IsFinish);
        }

        #endregion

        #region StepChanged 事件测试

        [Fact]
        public void Handler_RaisesStepChangedEvent()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            string fromStep = null;
            string toStep = null;
            logic.StepChanged += (sender, args) =>
            {
                fromStep = args.FromStep;
                toStep = args.ToStep;
            };

            // Act
            logic.Handler();

            // Assert
            Assert.Equal("初始状态", fromStep);
            Assert.Equal("停止ON", toStep);
        }

        #endregion

        #region GetLogicState 测试

        [Fact]
        public void GetLogicState_ReturnsCurrentState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act
            logic.Handler();
            var state = logic.GetLogicState();

            // Assert
            Assert.Contains("HomeLogic", state);
            Assert.Contains("停止ON", state);
        }

        #endregion

        #region 边界条件测试

        [Fact]
        public void Handler_WithNullBinElevator_DoesNotThrow()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var logic = new HomeLogic(sharedState, null);

            // Act - 运行完整周期，不应抛出异常
            var exception = Record.Exception(() => RunHomeLogicToCompletion(logic));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Handler_MultipleCallsInWaitState_StaysInSameState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // 进入等待状态
            logic.Handler();
            logic.Handler();
            Assert.Equal("停止ON等待", logic.SwitchIndex);

            // Act - 多次调用（未超时）
            for (int i = 0; i < 10; i++)
            {
                logic.Handler();
            }

            // Assert - 状态不变
            Assert.Equal("停止ON等待", logic.SwitchIndex);
        }

        #endregion

        #region 性能测试

        [Fact]
        public void Handler_HighFrequencyCallsAreEfficient()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var binElevator = new MockBinElevatorForHomeLogic();
            var logic = new HomeLogic(sharedState, binElevator);

            // Act - 高频率调用 1000 次
            var startTime = DateTime.Now;
            for (int i = 0; i < 1000; i++)
            {
                logic.Handler();
            }
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            // Assert - 1000 次调用应该在 500ms 内完成
            Assert.True(elapsed < 500, $"1000 Handler calls took {elapsed}ms, expected < 500ms");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 运行 HomeLogic 直到指定状态
        /// </summary>
        private void RunToState(HomeLogic logic, string targetState)
        {
            int maxIterations = 100;
            int iterations = 0;
            while (logic.SwitchIndex != targetState && iterations < maxIterations)
            {
                logic.Handler();
                if (logic.SwitchIndex.Contains("等待"))
                {
                    Thread.Sleep(1000); // 足够超时时间
                    logic.Handler(); // 触发超时转换
                }
                iterations++;
            }
        }

        /// <summary>
        /// 运行 HomeLogic 直到完成
        /// </summary>
        private void RunHomeLogicToCompletion(HomeLogic logic)
        {
            int maxIterations = 100;
            int iterations = 0;
            while (!logic.IsFinish && iterations < maxIterations)
            {
                logic.Handler();
                if (logic.SwitchIndex.Contains("等待"))
                {
                    Thread.Sleep(1000); // 足够超时时间
                    logic.Handler(); // 触发超时转换
                }
                iterations++;
            }
        }

        #endregion
    }

    #region Mock 类

    /// <summary>
    /// 用于 HomeLogic 测试的模拟 BinElevator
    /// </summary>
    public class MockBinElevatorForHomeLogic : IBinElevator
    {
        public bool InitCalled { get; private set; }
        public bool DestroyCalled { get; private set; }
        public bool ForceStopAllBinsCalled { get; private set; }
        public bool PerformHardwareInitializationCalled { get; private set; }
        public int RunCallCount { get; private set; }

        public void Init()
        {
            InitCalled = true;
        }

        public void Destroy()
        {
            DestroyCalled = true;
        }

        public void ForceStopAllBins()
        {
            ForceStopAllBinsCalled = true;
        }

        public void PerformHardwareInitialization()
        {
            PerformHardwareInitializationCalled = true;
        }

        public bool Run()
        {
            RunCallCount++;
            return true;
        }

        public BinMaterialCheckResult RaiseToSensor(int binNumber)
        {
            return BinMaterialCheckResult.CreateHasMaterial(binNumber);
        }
    }

    #endregion
}
