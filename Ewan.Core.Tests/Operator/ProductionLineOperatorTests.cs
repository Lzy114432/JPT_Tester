using Ewan.Core.Logic;
using Ewan.Core.Module;
using Ewan.Core.Operator;
using EwanCore.AlarmSystem;
using EwanCore.StateMachine;
using System;
using System.Threading;
using Xunit;

namespace Ewan.Core.Tests.Operator
{
    /// <summary>
    /// ProductionLineOperator 单元测试
    /// 验证生产线操作器的启动、停止、暂停、复位等核心功能
    /// </summary>
    public class ProductionLineOperatorTests : IDisposable
    {
        private readonly MockBinElevator _mockBinElevator;
        private ProductionLineOperator _operator;

        public ProductionLineOperatorTests()
        {
            _mockBinElevator = new MockBinElevator();
        }

        public void Dispose()
        {
            _operator?.Dispose();
        }

        #region 构造函数测试

        [Fact]
        public void Constructor_WithCustomBinElevator_InitializesSuccessfully()
        {
            // Act
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Assert
            Assert.NotNull(_operator);
            Assert.NotNull(_operator.SharedState);
            Assert.NotNull(_operator.BinElevator);
            Assert.Same(_mockBinElevator, _operator.BinElevator);
            Assert.True(_mockBinElevator.InitCalled);
        }

        [Fact]
        public void Constructor_WithNullBinElevator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ProductionLineOperator(null));
        }

        [Fact]
        public void Constructor_InitialState_NoAlarms()
        {
            // Arrange & Act
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Assert
            Assert.False(_operator.HasAlarm);
            Assert.False(_operator.HasNeedResetAlarm);
        }

        [Fact]
        public void Constructor_InitialState_RunStateIsStopped()
        {
            // Arrange & Act
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Assert
            Assert.Equal(RunTimeTag.Stop, _operator.RunState);
        }

        #endregion

        #region Start 测试

        [Fact]
        public void Start_WithNoAlarm_ReturnsTrue()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act
            bool result = _operator.Start();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Start_WithNoAlarm_StartsRunning()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act
            _operator.Start();
            Thread.Sleep(50); // 等待线程启动

            // Assert
            Assert.Equal(RunTimeTag.Run, _operator.RunState);
        }

        [Fact]
        public void Start_WithAlarm_ReturnsFalse()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.EmergencyStop(); // 添加报警

            // 清除报警后重新添加一个
            _operator.ClearAlarm();
            // 手动触发紧急停止以产生报警
            _operator.EmergencyStop();

            // Act
            bool result = _operator.Start();

            // Assert
            Assert.False(result);
            Assert.True(_operator.HasAlarm);
        }

        #endregion

        #region Stop 测试

        [Fact]
        public void Stop_AfterStart_StopsRunning()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);

            // Act
            _operator.Stop();

            // Assert
            Assert.Equal(RunTimeTag.Stop, _operator.RunState);
        }

        [Fact]
        public void Stop_ResetsSharedState()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.SharedState.SetSystemPaused(true);
            _operator.SharedState.TryStartLoading();

            // Act
            _operator.Stop();

            // Assert
            Assert.False(_operator.SharedState.IsSystemPaused());
            Assert.Equal(ProductionLineSharedState.ActiveProcess.None, _operator.SharedState.GetCurrentProcess());
        }

        [Fact]
        public void Stop_CallsForceStopAllBins()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);

            // Act
            _operator.Stop();

            // Assert
            Assert.True(_mockBinElevator.ForceStopAllBinsCalled);
        }

        #endregion

        #region Pause/Resume 测试

        [Fact]
        public void Pause_AfterStart_SetsSystemPaused()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);

            // Act
            _operator.Pause();

            // Assert
            Assert.True(_operator.SharedState.IsSystemPaused());
            Assert.Equal(RunTimeTag.Pause, _operator.RunState);
        }

        [Fact]
        public void Resume_AfterPause_ClearsSystemPaused()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);
            _operator.Pause();

            // Act
            _operator.Resume();

            // Assert
            Assert.False(_operator.SharedState.IsSystemPaused());
        }

        [Fact]
        public void Resume_AfterPause_SetsRequireReinit()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);
            _operator.Pause();

            // Act
            _operator.Resume();

            // Assert
            Assert.True(_operator.SharedState.RequireReinit());
        }

        [Fact]
        public void Resume_AfterPause_StartsRunning()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);
            _operator.Pause();

            // Act
            _operator.Resume();
            Thread.Sleep(50);

            // Assert
            Assert.Equal(RunTimeTag.Run, _operator.RunState);
        }

        #endregion

        #region EmergencyStop 测试

        [Fact]
        public void EmergencyStop_StopsRunning()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(50);

            // Act
            _operator.EmergencyStop();

            // Assert
            Assert.Equal(RunTimeTag.Stop, _operator.RunState);
        }

        [Fact]
        public void EmergencyStop_AddsAlarm()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act
            _operator.EmergencyStop();

            // Assert
            Assert.True(_operator.HasAlarm);
            Assert.True(_operator.HasNeedResetAlarm);
        }

        #endregion

        #region Home 测试

        [Fact]
        public void Home_StartsHomeLogic()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act
            _operator.Home(clearAlarm: true);
            Thread.Sleep(100);

            // Assert - HomeLogic 应该开始运行
            Assert.Equal(RunTimeTag.Run, _operator.RunState);
        }

        [Fact]
        public void Home_ClearsAlarms_WhenClearAlarmTrue()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.EmergencyStop(); // 添加报警
            Assert.True(_operator.HasAlarm);

            // Act
            _operator.Home(clearAlarm: true);

            // Assert
            Assert.False(_operator.HasAlarm);
        }

        [Fact]
        public void Home_KeepsAlarms_WhenClearAlarmFalse()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.EmergencyStop();
            Assert.True(_operator.HasAlarm);

            // Act
            _operator.Home(clearAlarm: false);

            // Assert - 报警应该保留
            Assert.True(_operator.HasAlarm);
        }

        [Fact]
        public void Home_ResetsSharedState()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.SharedState.TryStartLoading();
            _operator.SharedState.SetSystemPaused(true);

            // Act
            _operator.Home();

            // Assert
            Assert.Equal(ProductionLineSharedState.ActiveProcess.None, _operator.SharedState.GetCurrentProcess());
            Assert.False(_operator.SharedState.IsSystemPaused());
        }

        [Fact]
        public void Home_CallsForceStopAllBins()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act
            _operator.Home();

            // Assert
            Assert.True(_mockBinElevator.ForceStopAllBinsCalled);
        }

        #endregion

        #region ClearAlarm 测试

        [Fact]
        public void ClearAlarm_RemovesAllAlarms()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.EmergencyStop();
            Assert.True(_operator.HasAlarm);

            // Act
            _operator.ClearAlarm();

            // Assert
            Assert.False(_operator.HasAlarm);
        }

        #endregion

        #region Step 测试

        [Fact]
        public void Step_ExecutesSingleStep()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Home(); // 添加 HomeLogic 到队列
            Thread.Sleep(50);
            _operator.Pause();

            // Act
            _operator.Step();
            Thread.Sleep(100); // 等待单步执行完成

            // Assert - 执行单步后状态应该变化（Step 模式执行后变为 Stop）
            // 由于 Runner 是后台线程执行，需要一些时间
            Thread.Sleep(200);
            // Step 模式下会执行一轮然后停止
            Assert.True(_operator.RunState == RunTimeTag.Stop || _operator.RunState == RunTimeTag.Step);
        }

        #endregion

        #region SharedState 属性测试

        [Fact]
        public void SharedState_ReturnsProductionLineSharedState()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act & Assert
            Assert.NotNull(_operator.SharedState);
            Assert.IsType<ProductionLineSharedState>(_operator.SharedState);
        }

        #endregion

        #region Alarms 属性测试

        [Fact]
        public void Alarms_ReturnsAlarmService()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act & Assert
            Assert.NotNull(_operator.Alarms);
        }

        #endregion

        #region Dispose 测试

        [Fact]
        public void Dispose_StopsBinElevatorThread()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.Start();
            Thread.Sleep(100);

            // Act
            _operator.Dispose();
            Thread.Sleep(100);

            // Assert - 不应该再有 Run 调用
            int runCountAfterDispose = _mockBinElevator.RunCallCount;
            Thread.Sleep(100);
            Assert.Equal(runCountAfterDispose, _mockBinElevator.RunCallCount);
        }

        [Fact]
        public void Dispose_CallsDestroyOnBinElevator()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act
            _operator.Dispose();

            // Assert
            Assert.True(_mockBinElevator.DestroyCalled);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act & Assert - 不应该抛出异常
            _operator.Dispose();
            _operator.Dispose();
        }

        #endregion

        #region 集成场景测试

        [Fact]
        public void FullWorkflow_StartPauseResumeStop()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);

            // Act & Assert - Start
            Assert.True(_operator.Start());
            Thread.Sleep(50);
            Assert.Equal(RunTimeTag.Run, _operator.RunState);

            // Act & Assert - Pause
            _operator.Pause();
            Assert.Equal(RunTimeTag.Pause, _operator.RunState);
            Assert.True(_operator.SharedState.IsSystemPaused());

            // Act & Assert - Resume
            _operator.Resume();
            Thread.Sleep(50);
            Assert.Equal(RunTimeTag.Run, _operator.RunState);
            Assert.False(_operator.SharedState.IsSystemPaused());

            // Act & Assert - Stop
            _operator.Stop();
            Assert.Equal(RunTimeTag.Stop, _operator.RunState);
        }

        [Fact]
        public void AlarmBlocksStart_UntilCleared()
        {
            // Arrange
            _operator = new ProductionLineOperator(_mockBinElevator);
            _operator.EmergencyStop();
            Assert.True(_operator.HasAlarm);

            // Act - 尝试启动（应该失败）
            Assert.False(_operator.Start());

            // Act - 清除报警后重新启动
            _operator.ClearAlarm();
            Assert.True(_operator.Start());
        }

        #endregion
    }

    #region Mock 类

    /// <summary>
    /// 模拟的 BinElevator 实现用于测试
    /// </summary>
    public class MockBinElevator : IBinElevator
    {
        public bool InitCalled { get; private set; }
        public bool DestroyCalled { get; private set; }
        public bool ForceStopAllBinsCalled { get; private set; }
        public bool PerformHardwareInitializationCalled { get; private set; }
        public int RunCallCount { get; private set; }
        public int RaiseToSensorCallCount { get; private set; }
        public int LastBinNumber { get; private set; }

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
            Thread.Sleep(10); // 模拟一些工作
            return true;
        }

        public BinMaterialCheckResult RaiseToSensor(int binNumber)
        {
            RaiseToSensorCallCount++;
            LastBinNumber = binNumber;
            return BinMaterialCheckResult.CreateHasMaterial(binNumber);
        }
    }

    #endregion
}
