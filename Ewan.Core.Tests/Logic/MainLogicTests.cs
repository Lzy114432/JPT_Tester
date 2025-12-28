using Ewan.Core.Logic;
using Ewan.Model.System;
using EwanCore.StateMachine;
using System;
using Xunit;

namespace Ewan.Core.Tests.Logic
{
    /// <summary>
    /// MainLogic 单元测试：验证主逻辑状态流转与对子逻辑的调用。
    /// </summary>
    public class MainLogicTests
    {
        private static readonly object s_parametersLock = new object();

        private static void WithSystemParameters(bool loadingEnabled, bool unloadingEnabled, Action action)
        {
            lock (s_parametersLock)
            {
                var manager = SystemParametersManager.Instance;
                var original = manager.Parameters;

                var parameters = SystemParameters.CreateDefault();
                parameters.EnableLoadingModule = loadingEnabled;
                parameters.EnableUnloadingModule = unloadingEnabled;

                Assert.True(manager.SaveParameters(parameters));

                try
                {
                    action();
                }
                finally
                {
                    _ = manager.SaveParameters(original);
                }
            }
        }

        [Fact]
        public void Handler_LoadingCompletes_SwitchesToUnloadingFlow()
        {
            WithSystemParameters(loadingEnabled: true, unloadingEnabled: true, () =>
            {
                var loading = new OneShotLogic();
                var unloading = new TestLogic();
                var logic = new MainLogic(loading, unloading);

                logic.Handler(); // 初始状态 -> 装料流程
                logic.Handler(); // 装料流程：loading complete -> 切换到下料流程

                Assert.Equal(1, loading.HandlerCallCount);
                Assert.Equal(0, unloading.HandlerCallCount);
                Assert.Equal("下料流程", logic.SwitchIndex);
            });
        }

        [Fact]
        public void Handler_UnloadingCompletes_SwitchesBackToLoadingFlow()
        {
            WithSystemParameters(loadingEnabled: true, unloadingEnabled: true, () =>
            {
                var loading = new OneShotLogic();
                var unloading = new OneShotLogic();
                var logic = new MainLogic(loading, unloading);

                logic.Handler(); // 初始状态 -> 装料流程
                logic.Handler(); // 装料流程：loading complete -> 下料流程
                logic.Handler(); // 下料流程：unloading complete -> 装料流程

                Assert.Equal(1, loading.HandlerCallCount);
                Assert.Equal(1, unloading.HandlerCallCount);
                Assert.Equal("装料流程", logic.SwitchIndex);
            });
        }

        [Fact]
        public void Handler_LoadingDisabled_SwitchesToUnloadingFlow()
        {
            WithSystemParameters(loadingEnabled: false, unloadingEnabled: true, () =>
            {
                var loading = new TestLogic();
                var unloading = new TestLogic();
                var logic = new MainLogic(loading, unloading);

                logic.Handler(); // 初始状态 -> 装料流程
                logic.Handler(); // 装料流程：loading禁用 -> 直接切换到下料流程

                Assert.Equal(0, loading.HandlerCallCount);
                Assert.Equal(0, unloading.HandlerCallCount);
                Assert.Equal("下料流程", logic.SwitchIndex);
            });
        }

        [Fact]
        public void Handler_UnloadingDisabled_SwitchesBackToLoadingFlow()
        {
            WithSystemParameters(loadingEnabled: true, unloadingEnabled: false, () =>
            {
                var loading = new OneShotLogic();
                var unloading = new TestLogic();
                var logic = new MainLogic(loading, unloading);

                logic.Handler(); // 初始状态 -> 装料流程
                logic.Handler(); // 装料流程：loading complete -> 下料流程
                logic.Handler(); // 下料流程：unloading禁用 -> 直接切换到装料流程

                Assert.Equal(1, loading.HandlerCallCount);
                Assert.Equal(0, unloading.HandlerCallCount);
                Assert.Equal("装料流程", logic.SwitchIndex);
            });
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
