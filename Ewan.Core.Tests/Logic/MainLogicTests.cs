using Ewan.Core.Logic;
using EwanCore.StateMachine;
using Xunit;

namespace Ewan.Core.Tests.Logic
{
    /// <summary>
    /// MainLogic 单元测试：验证主逻辑状态流转与对子逻辑的调用。
    /// </summary>
    public class MainLogicTests
    {
        [Fact]
        public void Handler_FromInitialState_TransitionsToMainAction()
        {
            var loading = new TestLogic();
            var unloading = new TestLogic();
            var logic = new MainLogic(loading, unloading);

            Assert.Equal("初始状态", logic.SwitchIndex);

            logic.Handler();

            Assert.Equal("主动作", logic.SwitchIndex);
            Assert.False(loading.IsFinish);
            Assert.False(unloading.IsFinish);
            Assert.Equal("初始状态", loading.SwitchIndex);
            Assert.Equal("初始状态", unloading.SwitchIndex);
        }

        [Fact]
        public void Handler_InMainAction_CallsChildHandlers()
        {
            var loading = new TestLogic();
            var unloading = new TestLogic();
            var logic = new MainLogic(loading, unloading);

            logic.Handler(); // 初始状态 -> 主动作
            logic.Handler(); // 主动作：调用子逻辑

            Assert.Equal(1, loading.HandlerCallCount);
            Assert.Equal(1, unloading.HandlerCallCount);
        }

        [Fact]
        public void Handler_ResetsChildLogic_WhenChildCompletes()
        {
            var loading = new OneShotLogic();
            var unloading = new OneShotLogic();
            var logic = new MainLogic(loading, unloading);

            logic.Handler(); // 初始状态 -> 主动作
            logic.Handler(); // 主动作：子逻辑 complete -> 应被 reset

            Assert.Equal(1, loading.HandlerCallCount);
            Assert.Equal(1, unloading.HandlerCallCount);
            Assert.False(loading.IsFinish);
            Assert.False(unloading.IsFinish);
            Assert.Equal("初始状态", loading.SwitchIndex);
            Assert.Equal("初始状态", unloading.SwitchIndex);
        }

        private sealed class TestLogic : LogicBase
        {
            public int HandlerCallCount { get; private set; }

            public override void Handler()
            {
                HandlerCallCount++;
            }
        }

        private sealed class OneShotLogic : LogicBase
        {
            public int HandlerCallCount { get; private set; }

            public override void Handler()
            {
                HandlerCallCount++;
                Complete();
            }
        }
    }
}

