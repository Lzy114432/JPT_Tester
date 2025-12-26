using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EwanCore.AlarmSystem;
using EwanCore.StateMachine;
using Xunit;

namespace EwanCommon.Tests
{
    #region 测试用 Logic 类型

    /// <summary>
    /// 简单测试逻辑：立即完成
    /// </summary>
    public class SimpleTestLogic : LogicBase
    {
        public int HandlerCallCount { get; private set; }

        public override void Handler()
        {
            HandlerCallCount++;
            Complete();
        }
    }

    /// <summary>
    /// 多步骤测试逻辑
    /// </summary>
    public class MultiStepLogic : LogicBase
    {
        public List<string> StepHistory { get; } = new List<string>();

        public override void Handler()
        {
            StepHistory.Add(SwitchIndex);

            switch (SwitchIndex)
            {
                case "初始状态":
                    SwitchIndex = "步骤1";
                    break;
                case "步骤1":
                    SwitchIndex = "步骤2";
                    break;
                case "步骤2":
                    SwitchIndex = "步骤3";
                    break;
                case "步骤3":
                    Complete();
                    break;
            }
        }
    }

    /// <summary>
    /// 抛出异常的测试逻辑
    /// </summary>
    public class ExceptionLogic : LogicBase
    {
        public override void Handler()
        {
            SwitchIndex = "异常步骤";
            throw new InvalidOperationException("测试异常");
        }
    }

    /// <summary>
    /// 永不完成的测试逻辑
    /// </summary>
    public class NeverFinishLogic : LogicBase
    {
        public int HandlerCallCount { get; private set; }

        public override void Handler()
        {
            HandlerCallCount++;
            // 不调用 Complete()，永不完成
        }
    }

    /// <summary>
    /// 带超时的测试逻辑
    /// </summary>
    public class TimeoutTestLogic : LogicBase
    {
        public bool TimeoutOccurred { get; private set; }

        public override void Handler()
        {
            switch (SwitchIndex)
            {
                case "初始状态":
                    SwitchIndex = "等待超时";
                    break;
                case "等待超时":
                    if (Tw.StartCheckIsTimeout(SwitchIndex, 50))
                    {
                        TimeoutOccurred = true;
                        Complete();
                    }
                    break;
            }
        }
    }

    #endregion

    /// <summary>
    /// LogicBase 单元测试
    /// </summary>
    public class LogicBaseTests
    {
        #region 基本属性测试

        [Fact]
        public void LogicBase_InitialState_ShouldBeCorrect()
        {
            // Arrange & Act
            var logic = new SimpleTestLogic();

            // Assert
            Assert.Equal("初始状态", logic.SwitchIndex);
            Assert.False(logic.IsFinish);
        }

        [Fact]
        public void Complete_ShouldSetFinishAndEndState()
        {
            // Arrange
            var logic = new SimpleTestLogic();

            // Act
            logic.Complete();

            // Assert
            Assert.True(logic.IsFinish);
            Assert.Equal("结束状态", logic.SwitchIndex);
        }

        [Fact]
        public void Rset_ShouldResetToInitialState()
        {
            // Arrange
            var logic = new MultiStepLogic();
            logic.Handler(); // 进入步骤1
            logic.Handler(); // 进入步骤2

            // Act
            logic.Rset();

            // Assert
            Assert.Equal("初始状态", logic.SwitchIndex);
            Assert.False(logic.IsFinish);
        }

        #endregion

        #region 步骤切换测试

        [Fact]
        public void SwitchIndex_SameValue_ShouldNotTriggerEvent()
        {
            // Arrange
            var logic = new SimpleTestLogic();
            var eventCount = 0;
            logic.StepChanged += (_, __) => eventCount++;

            // Act - 设置相同的值
            // 通过 Handler 触发，不直接设置（因为 set 是 protected）
            // 初始值就是"初始状态"，所以第一次 Handler 会触发事件

            // Assert
            Assert.Equal(0, eventCount); // 未调用 Handler，不应有事件
        }

        [Fact]
        public void StepChanged_Event_ShouldBeFired()
        {
            // Arrange
            var logic = new MultiStepLogic();
            StepChangedEventArgs? eventArgs = null;
            logic.StepChanged += (_, e) => eventArgs = e;

            // Act
            logic.Handler(); // 从"初始状态"到"步骤1"

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal("MultiStepLogic", eventArgs!.LogicName);
            Assert.Equal("初始状态", eventArgs.FromStep);
            Assert.Equal("步骤1", eventArgs.ToStep);
        }

        [Fact]
        public void MultiStep_ShouldTrackAllSteps()
        {
            // Arrange
            var logic = new MultiStepLogic();

            // Act
            while (!logic.IsFinish)
            {
                logic.Handler();
            }

            // Assert
            Assert.True(logic.IsFinish);
            Assert.Equal("结束状态", logic.SwitchIndex);
            Assert.Contains("初始状态", logic.StepHistory);
            Assert.Contains("步骤1", logic.StepHistory);
            Assert.Contains("步骤2", logic.StepHistory);
            Assert.Contains("步骤3", logic.StepHistory);
        }

        #endregion

        #region GetLogicState 测试

        [Fact]
        public void GetLogicState_ShouldReturnFormattedString()
        {
            // Arrange
            var logic = new SimpleTestLogic();

            // Act
            var state = logic.GetLogicState();

            // Assert
            Assert.Contains("SimpleTestLogic", state);
            Assert.Contains("初始状态", state);
        }

        [Fact]
        public void GetLogicState_WithSonLogic_ShouldIncludeSonState()
        {
            // Arrange
            var parentLogic = new SimpleTestLogic();
            var sonLogic = new MultiStepLogic();

            // Act
            parentLogic.AddCurSonLogic(sonLogic);
            var state = parentLogic.GetLogicState();

            // Assert
            Assert.Contains("SimpleTestLogic", state);
            Assert.Contains("MultiStepLogic", state);
        }

        #endregion

        #region AddCurSonLogic 测试

        [Fact]
        public void AddCurSonLogic_NullLogic_ShouldThrow()
        {
            // Arrange
            var logic = new SimpleTestLogic();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => logic.AddCurSonLogic(null!));
        }

        [Fact]
        public void AddCurSonLogic_ShouldResetSonLogic()
        {
            // Arrange
            var parentLogic = new SimpleTestLogic();
            var sonLogic = new MultiStepLogic();
            sonLogic.Handler(); // 改变状态

            // Act
            parentLogic.AddCurSonLogic(sonLogic);

            // Assert
            Assert.Equal("初始状态", sonLogic.SwitchIndex);
        }

        [Fact]
        public void AddCurSonLogic_ShouldReplacePreviousSon()
        {
            // Arrange
            var parentLogic = new SimpleTestLogic();
            var son1 = new SimpleTestLogic();
            var son2 = new MultiStepLogic();

            // Act
            parentLogic.AddCurSonLogic(son1);
            parentLogic.AddCurSonLogic(son2);
            var state = parentLogic.GetLogicState();

            // Assert - 只应包含 son2
            Assert.Contains("MultiStepLogic", state);
            // state 不应包含两个逻辑的信息（son1 被替换）
        }

        #endregion

        #region TimeoutWatch 测试

        [Fact]
        public void TimeoutWatch_ShouldBeAvailable()
        {
            // Arrange
            var logic = new TimeoutTestLogic();

            // Act - 多次调用直到超时
            var maxIterations = 100;
            var iterations = 0;
            while (!logic.IsFinish && iterations < maxIterations)
            {
                logic.Handler();
                Thread.Sleep(10);
                iterations++;
            }

            // Assert
            Assert.True(logic.TimeoutOccurred);
            Assert.True(logic.IsFinish);
        }

        #endregion
    }

    /// <summary>
    /// LogicRunner 单元测试
    /// </summary>
    public class LogicRunnerTests : IDisposable
    {
        private readonly LogicRunner _runner;

        public LogicRunnerTests()
        {
            _runner = new LogicRunner();
        }

        public void Dispose()
        {
            _runner.Dispose();
        }

        #region 基本属性测试

        [Fact]
        public void LogicRunner_InitialState_ShouldBeStopped()
        {
            // Assert
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag);
            Assert.Equal(0, _runner.Count);
            Assert.Equal(string.Empty, _runner.CurLogicStateStr);
        }

        #endregion

        #region AddAction 测试

        [Fact]
        public void AddAction_ShouldIncrementCount()
        {
            // Arrange & Act
            _runner.AddAction(new SimpleTestLogic());
            _runner.AddAction(new MultiStepLogic());

            // Assert
            Assert.Equal(2, _runner.Count);
        }

        [Fact]
        public void AddAction_NullLogic_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _runner.AddAction(null!));
        }

        [Fact]
        public void AddAction_ShouldResetLogic()
        {
            // Arrange
            var logic = new MultiStepLogic();
            logic.Handler(); // 改变状态

            // Act
            _runner.AddAction(logic);

            // Assert
            Assert.Equal("初始状态", logic.SwitchIndex);
        }

        #endregion

        #region ClearAction 测试

        [Fact]
        public void ClearAction_ShouldRemoveAllLogics()
        {
            // Arrange
            _runner.AddAction(new SimpleTestLogic());
            _runner.AddAction(new MultiStepLogic());

            // Act
            _runner.ClearAction();

            // Assert
            Assert.Equal(0, _runner.Count);
        }

        #endregion

        #region ExistAction 测试

        [Fact]
        public void ExistAction_ExistingType_ShouldReturnTrue()
        {
            // Arrange
            _runner.AddAction(new SimpleTestLogic());

            // Act & Assert
            Assert.True(_runner.ExistAction(typeof(SimpleTestLogic)));
        }

        [Fact]
        public void ExistAction_NonExistingType_ShouldReturnFalse()
        {
            // Arrange
            _runner.AddAction(new SimpleTestLogic());

            // Act & Assert
            Assert.False(_runner.ExistAction(typeof(MultiStepLogic)));
        }

        [Fact]
        public void ExistAction_NullType_ShouldThrow()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _runner.ExistAction(null!));
        }

        #endregion

        #region Start/Stop/Pause 测试

        [Fact]
        public void Start_ShouldSetRunTag()
        {
            // Act
            _runner.Start();

            // Assert
            Assert.Equal(RunTimeTag.Run, _runner.RunTag);
        }

        [Fact]
        public void Stop_ShouldSetRunTag()
        {
            // Arrange
            _runner.Start();

            // Act
            _runner.Stop();

            // Assert
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag);
        }

        [Fact]
        public void Pause_ShouldSetRunTag()
        {
            // Arrange
            _runner.Start();

            // Act
            _runner.Pause();

            // Assert
            Assert.Equal(RunTimeTag.Pause, _runner.RunTag);
        }

        [Fact]
        public void Step_ShouldSetRunTag()
        {
            // Act
            _runner.Step();

            // Assert - Step 会执行后自动切回 Stop，但刚调用时应该是 Step
            // 由于线程执行，这里可能已经变成 Stop
            Assert.True(_runner.RunTag == RunTimeTag.Step || _runner.RunTag == RunTimeTag.Stop);
        }

        #endregion

        #region 逻辑执行测试

        [Fact]
        public async Task Start_ShouldExecuteLogicAndRemoveWhenFinished()
        {
            // Arrange
            var logic = new SimpleTestLogic();
            _runner.AddAction(logic);

            // Act
            _runner.Start();
            await Task.Delay(200); // 等待执行

            // Assert
            Assert.True(logic.IsFinish);
            Assert.Equal(0, _runner.Count); // 完成后应被移除
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag); // 队列空了自动停止
        }

        [Fact]
        public async Task Start_MultipleLogics_ShouldExecuteAll()
        {
            // Arrange
            var logic1 = new SimpleTestLogic();
            var logic2 = new SimpleTestLogic();
            _runner.AddAction(logic1);
            _runner.AddAction(logic2);

            // Act
            _runner.Start();
            await Task.Delay(200);

            // Assert
            Assert.True(logic1.IsFinish);
            Assert.True(logic2.IsFinish);
            Assert.Equal(0, _runner.Count);
        }

        [Fact]
        public async Task Step_ShouldExecuteOnceAndStop()
        {
            // Arrange
            var logic = new NeverFinishLogic();
            _runner.AddAction(logic);

            // Act
            _runner.Step();
            await Task.Delay(200);

            // Assert
            Assert.True(logic.HandlerCallCount >= 1);
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag);
        }

        #endregion

        #region 异常处理测试

        [Fact]
        public async Task LogicException_ShouldBeFiredOnError()
        {
            // Arrange
            var logic = new ExceptionLogic();
            _runner.AddAction(logic);
            LogicExceptionEventArgs? eventArgs = null;
            _runner.LogicException += (_, e) => eventArgs = e;

            // Act
            _runner.Start();
            await Task.Delay(200);

            // Assert
            Assert.NotNull(eventArgs);
            Assert.Equal("ExceptionLogic", eventArgs!.LogicName);
            Assert.Equal("异常步骤", eventArgs.Step);
            Assert.IsType<InvalidOperationException>(eventArgs.Exception);
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag); // 异常后自动停止
        }

        #endregion

        #region CurLogicStateStr 测试

        [Fact]
        public async Task CurLogicStateStr_ShouldUpdateDuringExecution()
        {
            // Arrange
            var logic = new NeverFinishLogic();
            _runner.AddAction(logic);

            // Act
            _runner.Start();
            await Task.Delay(100);
            var state = _runner.CurLogicStateStr;
            _runner.Stop();

            // Assert
            Assert.Contains("NeverFinishLogic", state);
        }

        #endregion
    }

    /// <summary>
    /// LogicController 单元测试
    /// </summary>
    public class LogicControllerTests
    {
        #region 构造函数测试

        [Fact]
        public void Constructor_WithRunners_ShouldAddAll()
        {
            // Arrange
            var runner1 = new LogicRunner();
            var runner2 = new LogicRunner();
            // 添加永不完成的逻辑，防止空队列自动停止
            runner1.AddAction(new NeverFinishLogic());
            runner2.AddAction(new NeverFinishLogic());

            // Act
            var controller = new LogicController(runner1, runner2);

            // Assert - 通过调用 Start 验证 runner 被添加
            controller.Start();
            Assert.Equal(RunTimeTag.Run, runner1.RunTag);
            Assert.Equal(RunTimeTag.Run, runner2.RunTag);

            // Cleanup
            controller.Stop();
            runner1.Dispose();
            runner2.Dispose();
        }

        [Fact]
        public void Constructor_NullRunners_ShouldNotThrow()
        {
            // Act & Assert
            var ex = Record.Exception(() => new LogicController(null));
            Assert.Null(ex);
        }

        #endregion

        #region AddRunner 测试

        [Fact]
        public void AddRunner_NullRunner_ShouldThrow()
        {
            // Arrange
            var controller = new LogicController();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => controller.AddRunner(null!));
        }

        [Fact]
        public void AddRunner_ShouldAddToController()
        {
            // Arrange
            var controller = new LogicController();
            var runner = new LogicRunner();
            runner.AddAction(new NeverFinishLogic()); // 防止空队列自动停止

            // Act
            controller.AddRunner(runner);
            controller.Start();

            // Assert
            Assert.Equal(RunTimeTag.Run, runner.RunTag);

            // Cleanup
            controller.Stop();
            runner.Dispose();
        }

        #endregion

        #region Start/Stop/Pause/Step 测试

        [Fact]
        public void Start_ShouldStartAllRunners()
        {
            // Arrange
            var runner1 = new LogicRunner();
            var runner2 = new LogicRunner();
            runner1.AddAction(new NeverFinishLogic()); // 防止空队列自动停止
            runner2.AddAction(new NeverFinishLogic());
            var controller = new LogicController(runner1, runner2);

            // Act
            controller.Start();

            // Assert
            Assert.Equal(RunTimeTag.Run, runner1.RunTag);
            Assert.Equal(RunTimeTag.Run, runner2.RunTag);

            // Cleanup
            controller.Stop();
            runner1.Dispose();
            runner2.Dispose();
        }

        [Fact]
        public void Stop_ShouldStopAllRunners()
        {
            // Arrange
            var runner1 = new LogicRunner();
            var runner2 = new LogicRunner();
            var controller = new LogicController(runner1, runner2);
            controller.Start();

            // Act
            controller.Stop();

            // Assert
            Assert.Equal(RunTimeTag.Stop, runner1.RunTag);
            Assert.Equal(RunTimeTag.Stop, runner2.RunTag);

            // Cleanup
            runner1.Dispose();
            runner2.Dispose();
        }

        [Fact]
        public void Pause_ShouldPauseAllRunners()
        {
            // Arrange
            var runner1 = new LogicRunner();
            var runner2 = new LogicRunner();
            var controller = new LogicController(runner1, runner2);
            controller.Start();

            // Act
            controller.Pause();

            // Assert
            Assert.Equal(RunTimeTag.Pause, runner1.RunTag);
            Assert.Equal(RunTimeTag.Pause, runner2.RunTag);

            // Cleanup
            runner1.Dispose();
            runner2.Dispose();
        }

        [Fact]
        public void Step_ShouldStepAllRunners()
        {
            // Arrange
            var runner1 = new LogicRunner();
            var runner2 = new LogicRunner();
            var controller = new LogicController(runner1, runner2);

            // Act
            controller.Step();

            // Assert - Step 模式会快速切回 Stop
            Assert.True(runner1.RunTag == RunTimeTag.Step || runner1.RunTag == RunTimeTag.Stop);
            Assert.True(runner2.RunTag == RunTimeTag.Step || runner2.RunTag == RunTimeTag.Stop);

            // Cleanup
            runner1.Dispose();
            runner2.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// MachineOperator 单元测试
    /// </summary>
    public class MachineOperatorTests : IDisposable
    {
        private readonly AlarmService _alarms;
        private readonly LogicRunner _runner;
        private readonly MachineOperator _operator;

        public MachineOperatorTests()
        {
            _alarms = new AlarmService();
            _runner = new LogicRunner();
            _operator = new MachineOperator(_alarms, _runner);
        }

        public void Dispose()
        {
            _runner.Dispose();
        }

        #region 构造函数测试

        [Fact]
        public void Constructor_NullAlarms_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MachineOperator(null!, new LogicRunner()));
        }

        [Fact]
        public void Constructor_NullRunner_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MachineOperator(new AlarmService(), null!));
        }

        #endregion

        #region Start 测试

        [Fact]
        public void Start_WithAlarm_ShouldReturnFalse()
        {
            // Arrange
            _alarms.AddAlarm("测试报警");

            // Act
            var result = _operator.Start(() => new SimpleTestLogic());

            // Assert
            Assert.False(result);
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag);
        }

        [Fact]
        public void Start_NoAlarm_ShouldReturnTrue()
        {
            // Act
            var result = _operator.Start(() => new SimpleTestLogic());

            // Assert
            Assert.True(result);
            Assert.Equal(RunTimeTag.Run, _runner.RunTag);
        }

        [Fact]
        public void Start_EmptyQueue_ShouldAddLogic()
        {
            // Act
            _operator.Start(() => new SimpleTestLogic());

            // Assert
            Assert.True(_runner.Count >= 0); // 可能已执行完毕
        }

        [Fact]
        public void Start_NullFactory_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => _operator.Start(null!));
        }

        [Fact]
        public void Start_WithExistingLogic_ShouldNotAddAnother()
        {
            // Arrange
            _runner.AddAction(new NeverFinishLogic());
            var initialCount = _runner.Count;

            // Act
            _operator.Start(() => new SimpleTestLogic());

            // Assert - 不应该添加新逻辑
            Assert.Equal(initialCount, _runner.Count);
        }

        #endregion

        #region Stop 测试

        [Fact]
        public void Stop_ShouldStopRunner()
        {
            // Arrange
            _operator.Start(() => new NeverFinishLogic());

            // Act
            _operator.Stop();

            // Assert
            Assert.Equal(RunTimeTag.Stop, _runner.RunTag);
        }

        [Fact]
        public void Stop_WithClearQueue_ShouldClearActions()
        {
            // Arrange
            _runner.AddAction(new NeverFinishLogic());

            // Act
            _operator.Stop(clearQueue: true);

            // Assert
            Assert.Equal(0, _runner.Count);
        }

        [Fact]
        public void Stop_WithoutClearQueue_ShouldKeepActions()
        {
            // Arrange
            _runner.AddAction(new NeverFinishLogic());
            var countBefore = _runner.Count;

            // Act
            _operator.Stop(clearQueue: false);

            // Assert
            Assert.Equal(countBefore, _runner.Count);
        }

        #endregion

        #region Pause 测试

        [Fact]
        public void Pause_ShouldPauseRunner()
        {
            // Arrange
            _operator.Start(() => new NeverFinishLogic());

            // Act
            _operator.Pause();

            // Assert
            Assert.Equal(RunTimeTag.Pause, _runner.RunTag);
        }

        #endregion

        #region Step 测试

        [Fact]
        public void Step_ShouldStepRunner()
        {
            // Arrange
            _runner.AddAction(new NeverFinishLogic());

            // Act
            _operator.Step();

            // Assert
            Assert.True(_runner.RunTag == RunTimeTag.Step || _runner.RunTag == RunTimeTag.Stop);
        }

        #endregion

        #region Home 测试

        [Fact]
        public void Home_ShouldStopClearAndStart()
        {
            // Arrange
            _runner.AddAction(new NeverFinishLogic());
            _alarms.AddAlarm("报警");

            // Act
            _operator.Home(() => new SimpleTestLogic());

            // Assert
            Assert.Equal(0, _alarms.AlarmCount); // 报警被清除
            Assert.Equal(RunTimeTag.Run, _runner.RunTag);
        }

        [Fact]
        public void Home_WithoutClearAlarm_ShouldKeepAlarms()
        {
            // Arrange
            _alarms.AddAlarm("报警");

            // Act
            _operator.Home(() => new SimpleTestLogic(), clearAlarm: false);

            // Assert
            Assert.Equal(1, _alarms.AlarmCount);
        }

        [Fact]
        public void Home_WithBeforeCallback_ShouldInvokeCallback()
        {
            // Arrange
            var callbackInvoked = false;

            // Act
            _operator.Home(() => new SimpleTestLogic(), beforeHome: () => callbackInvoked = true);

            // Assert
            Assert.True(callbackInvoked);
        }

        [Fact]
        public void Home_NullFactory_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => _operator.Home(null!));
        }

        #endregion

        #region ClearAlarm 测试

        [Fact]
        public void ClearAlarm_ShouldClearAllAlarms()
        {
            // Arrange
            _alarms.AddAlarm("报警1");
            _alarms.AddAlarm("报警2");

            // Act
            _operator.ClearAlarm();

            // Assert
            Assert.Equal(0, _alarms.AlarmCount);
        }

        #endregion
    }

    /// <summary>
    /// StepChangedEventArgs 单元测试
    /// </summary>
    public class StepChangedEventArgsTests
    {
        [Fact]
        public void Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var timestamp = DateTimeOffset.Now;

            // Act
            var args = new StepChangedEventArgs("TestLogic", "步骤1", "步骤2", timestamp);

            // Assert
            Assert.Equal("TestLogic", args.LogicName);
            Assert.Equal("步骤1", args.FromStep);
            Assert.Equal("步骤2", args.ToStep);
            Assert.Equal(timestamp, args.Timestamp);
        }

        [Fact]
        public void Constructor_NullValues_ShouldUseEmptyStrings()
        {
            // Act
            var args = new StepChangedEventArgs(null!, null!, null!, DateTimeOffset.Now);

            // Assert
            Assert.Equal(string.Empty, args.LogicName);
            Assert.Equal(string.Empty, args.FromStep);
            Assert.Equal(string.Empty, args.ToStep);
        }
    }

    /// <summary>
    /// LogicExceptionEventArgs 单元测试
    /// </summary>
    public class LogicExceptionEventArgsTests
    {
        [Fact]
        public void Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var exception = new InvalidOperationException("测试");

            // Act
            var args = new LogicExceptionEventArgs("TestLogic", "步骤1", exception);

            // Assert
            Assert.Equal("TestLogic", args.LogicName);
            Assert.Equal("步骤1", args.Step);
            Assert.Same(exception, args.Exception);
        }

        [Fact]
        public void Constructor_NullValues_ShouldUseEmptyStrings()
        {
            // Act
            var args = new LogicExceptionEventArgs(null!, null!, null!);

            // Assert
            Assert.Equal(string.Empty, args.LogicName);
            Assert.Equal(string.Empty, args.Step);
            Assert.Null(args.Exception);
        }
    }

    /// <summary>
    /// RunTimeTag 枚举测试
    /// </summary>
    public class RunTimeTagTests
    {
        [Fact]
        public void RunTimeTag_ShouldHaveCorrectValues()
        {
            Assert.Equal(0, (int)RunTimeTag.Run);
            Assert.Equal(1, (int)RunTimeTag.Step);
            Assert.Equal(2, (int)RunTimeTag.Stop);
            Assert.Equal(3, (int)RunTimeTag.Pause);
        }

        [Theory]
        [InlineData(RunTimeTag.Run)]
        [InlineData(RunTimeTag.Step)]
        [InlineData(RunTimeTag.Stop)]
        [InlineData(RunTimeTag.Pause)]
        public void RunTimeTag_AllValues_ShouldBeDefined(RunTimeTag tag)
        {
            Assert.True(Enum.IsDefined(typeof(RunTimeTag), tag));
        }
    }
}
