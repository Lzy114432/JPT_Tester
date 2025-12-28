using Ewan.Core.Logic;
using Ewan.Model.Production;
using EwanCore.Messaging;
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
            var logic = new HomeLogic();

            // Assert
            Assert.Equal("初始状态", logic.SwitchIndex);
            Assert.False(logic.IsFinish);
        }

        #endregion

        #region 状态转换测试

        [Fact]
        public void Handler_FromInitialState_TransitionsToStopPulse()
        {
            // Arrange
            var logic = new HomeLogic();

            // Act
            logic.Handler();

            // Assert
            Assert.Equal("发送停止脉冲", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_FromStopPulse_TransitionsToStopWait()
        {
            // Arrange
            var logic = new HomeLogic();

            // Act - 进入 "发送停止脉冲" 状态
            logic.Handler(); // 初始状态 -> 发送停止脉冲
            Assert.Equal("发送停止脉冲", logic.SwitchIndex);

            logic.Handler(); // 发送停止脉冲 -> 等待停止完成

            // Assert
            Assert.Equal("等待停止完成", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_StopWait_TransitionsAfterTimeout()
        {
            // Arrange
            var logic = new HomeLogic();

            // 进入等待状态
            logic.Handler(); // 初始状态 -> 发送停止脉冲
            logic.Handler(); // 发送停止脉冲 -> 等待停止完成
            Assert.Equal("等待停止完成", logic.SwitchIndex);

            // 第一次调用 StartCheckIsTimeout 会启动计时器
            logic.Handler(); // 应该仍在等待
            Assert.Equal("等待停止完成", logic.SwitchIndex);

            // 等待足够长时间让超时触发（STOP_PULSE_DURATION + STOP_OFF_DELAY）
            Thread.Sleep(1200);
            logic.Handler();

            // Assert
            Assert.Equal("发送开始脉冲", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_StartPulse_TransitionsToStartWait()
        {
            // Arrange
            var logic = new HomeLogic();

            // 使用辅助方法推进到 "发送开始脉冲" 状态
            RunToState(logic, "发送开始脉冲");
            Assert.Equal("发送开始脉冲", logic.SwitchIndex);

            // Act
            logic.Handler(); // 发送开始脉冲 -> 等待开始完成

            // Assert
            Assert.Equal("等待开始完成", logic.SwitchIndex);
        }

        [Fact]
        public void Handler_CompleteFullCycle_ReachesFinish()
        {
            // Arrange
            var logic = new HomeLogic();
            using var responder = RegisterInitializeResponder();

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
            var logic = new HomeLogic();

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
            var logic = new HomeLogic();

            // 进入等待状态
            logic.Handler(); // 初始状态 -> 发送停止脉冲
            logic.Handler(); // 发送停止脉冲 -> 等待停止完成

            // Act - 立即再次调用（未超时）
            var startTime = DateTime.Now;
            logic.Handler();
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            // Assert - 应该快速返回，状态不变
            Assert.True(elapsed < 50, $"Handler took {elapsed}ms during wait state");
            Assert.Equal("等待停止完成", logic.SwitchIndex);
        }

        #endregion

        #region BinElevator 交互测试

        [Fact]
        public void Handler_PostsBinInitializationMessage()
        {
            // Arrange
            var logic = new HomeLogic();
            using var signal = new ManualResetEventSlim(false);
            BinElevatorCommandMessage? receivedMessage = null;

            using var responder = RegisterInitializeResponder();
            using var subscription = MessageHub.Current.Subscribe<BinElevatorCommandMessage>(message =>
            {
                if (message?.Command == BinCommand.Initialize)
                {
                    receivedMessage = message;
                    signal.Set();
                }
            });

            // Act - 运行到料仓初始化步骤
            RunToState(logic, "料仓初始化");
            logic.Handler();

            // Assert
            Assert.True(signal.Wait(1000));
            Assert.NotNull(receivedMessage);
            Assert.Equal(BinCommand.Initialize, receivedMessage!.Command);
            Assert.Equal(0, receivedMessage.BinNumber);
        }

        #endregion

        #region Rset 测试

        [Fact]
        public void Rset_ResetsToInitialState()
        {
            // Arrange
            var logic = new HomeLogic();

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
            var logic = new HomeLogic();

            string fromStep = string.Empty;
            string toStep = string.Empty;
            logic.StepChanged += (sender, args) =>
            {
                fromStep = args.FromStep;
                toStep = args.ToStep;
            };

            // Act
            logic.Handler();

            // Assert
            Assert.Equal("初始状态", fromStep);
            Assert.Equal("发送停止脉冲", toStep);
        }

        #endregion

        #region GetLogicState 测试

        [Fact]
        public void GetLogicState_ReturnsCurrentState()
        {
            // Arrange
            var logic = new HomeLogic();

            // Act
            logic.Handler();
            var state = logic.GetLogicState();

            // Assert
            Assert.Contains("HomeLogic", state);
            Assert.Contains("发送停止脉冲", state);
        }

        #endregion

        #region 边界条件测试

        [Fact]
        public void Handler_WithDefaultConstructor_DoesNotThrow()
        {
            // Arrange
            var logic = new HomeLogic();

            // Act - 运行数步，不应抛出异常
            var exception = Record.Exception(() =>
            {
                logic.Handler();
                logic.Handler();
            });

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Handler_MultipleCallsInWaitState_StaysInSameState()
        {
            // Arrange
            var logic = new HomeLogic();

            // 进入等待状态
            logic.Handler();
            logic.Handler();
            Assert.Equal("等待停止完成", logic.SwitchIndex);

            // Act - 多次调用（未超时）
            for (int i = 0; i < 10; i++)
            {
                logic.Handler();
            }

            // Assert - 状态不变
            Assert.Equal("等待停止完成", logic.SwitchIndex);
        }

        #endregion

        #region 性能测试

        [Fact]
        public void Handler_HighFrequencyCallsAreEfficient()
        {
            // Arrange
            var logic = new HomeLogic();

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

        private IDisposable RegisterInitializeResponder(
            BinOperationResult result = BinOperationResult.Success,
            string description = "测试",
            string errorMessage = "")
        {
            return MessageHub.Current.Respond<BinElevatorCommandMessage, BinElevatorStatusMessage>(request =>
                request.Command == BinCommand.Initialize
                    ? BinElevatorStatusMessage.InitializeResult(result, description, errorMessage)
                    : BinElevatorStatusMessage.InitializeResult(BinOperationResult.Error, "非初始化指令"));
        }

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
                if (logic.SwitchIndex.Contains("等待") && logic.SwitchIndex != targetState)
                {
                    Thread.Sleep(logic.SwitchIndex == "等待料仓完成" ? 100 : 1200);
                    logic.Handler();
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
                    Thread.Sleep(logic.SwitchIndex == "等待料仓完成" ? 100 : 1200);
                    logic.Handler();
                }
                iterations++;
            }
        }

        #endregion
    }
}
