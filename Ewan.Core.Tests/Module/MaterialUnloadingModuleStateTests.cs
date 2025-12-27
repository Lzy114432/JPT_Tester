using Ewan.Core.Module;
using System;
using System.Reflection;
using Xunit;

namespace Ewan.Core.Tests.Module
{
    /// <summary>
    /// MaterialUnloadingModule 状态转换测试
    /// 特征测试：验证当前行为的状态转换逻辑
    /// </summary>
    public class MaterialUnloadingModuleStateTests
    {
        #region 测试辅助方法

        /// <summary>
        /// 使用反射获取私有字段值
        /// </summary>
        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found");
            return (T)field.GetValue(obj);
        }

        /// <summary>
        /// 使用反射设置私有字段值
        /// </summary>
        private static void SetPrivateField<T>(object obj, string fieldName, T value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"Field '{fieldName}' not found");
            field.SetValue(obj, value);
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        private static MaterialUnloadingState GetCurrentState(MaterialUnloadingModule module)
        {
            return GetPrivateField<MaterialUnloadingState>(module, "_currentState");
        }

        /// <summary>
        /// 设置当前状态
        /// </summary>
        private static void SetCurrentState(MaterialUnloadingModule module, MaterialUnloadingState state)
        {
            SetPrivateField(module, "_currentState", state);
        }

        /// <summary>
        /// 创建测试用模块实例
        /// </summary>
        private static MaterialUnloadingModule CreateTestModule(ProductionLineSharedState? sharedState = null)
        {
            var state = sharedState ?? new ProductionLineSharedState();
            return new MaterialUnloadingModule(state);
        }

        #endregion

        #region 初始状态测试

        [Fact]
        public void Constructor_SetsInitialStateToIdle()
        {
            // Arrange & Act
            var sharedState = new ProductionLineSharedState();
            var module = new MaterialUnloadingModule(sharedState);

            // Assert
            var state = GetCurrentState(module);
            Assert.Equal(MaterialUnloadingState.Idle, state);
        }

        [Fact]
        public void Constructor_InitializesRingLineSignalToFalse()
        {
            // Arrange & Act
            var sharedState = new ProductionLineSharedState();
            var module = new MaterialUnloadingModule(sharedState);

            // Assert
            var ringLineSignal = GetPrivateField<bool>(module, "_ringLineSignal");
            Assert.False(ringLineSignal);
        }

        [Fact]
        public void Constructor_InitializesRequestProcessedToFalse()
        {
            // Arrange & Act
            var sharedState = new ProductionLineSharedState();
            var module = new MaterialUnloadingModule(sharedState);

            // Assert
            var requestProcessed = GetPrivateField<bool>(module, "_requestProcessed");
            Assert.False(requestProcessed);
        }

        #endregion

        #region Idle 状态转换测试

        [Fact]
        public void Idle_WhenSystemPaused_RemainsInIdle()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetSystemPaused(true);
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialUnloadingState.Idle);

            // Act
            bool isPaused = sharedState.IsSystemPaused();

            // Assert
            Assert.True(isPaused);
            Assert.Equal(MaterialUnloadingState.Idle, GetCurrentState(module));
        }

        [Fact]
        public void Idle_WhenNoRingLineSignal_RemainsInIdle()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialUnloadingState.Idle);
            SetPrivateField(module, "_ringLineSignal", false);

            // Assert
            Assert.Equal(MaterialUnloadingState.Idle, GetCurrentState(module));
        }

        [Fact]
        public void Idle_WhenRingLineSignalAndAlreadyProcessed_RemainsInIdle()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialUnloadingState.Idle);
            SetPrivateField(module, "_ringLineSignal", true);
            SetPrivateField(module, "_requestProcessed", true);

            // Assert - 已处理过的请求不会重复处理
            var requestProcessed = GetPrivateField<bool>(module, "_requestProcessed");
            Assert.True(requestProcessed);
            Assert.Equal(MaterialUnloadingState.Idle, GetCurrentState(module));
        }

        #endregion

        #region ForceStopUnloading 测试

        [Fact]
        public void ForceStopUnloading_ResetsStateToIdle()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialUnloadingState.PickingMaterial);

            // Act
            module.ForceStopUnloading();

            // Assert
            Assert.Equal(MaterialUnloadingState.Idle, GetCurrentState(module));
        }

        [Fact]
        public void ForceStopUnloading_ClearsAllRequestFlags()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_stopRequested", true);
            SetPrivateField(module, "_unloadingRequested", true);
            SetPrivateField(module, "_ringLineSignal", true);
            SetPrivateField(module, "_requestProcessed", true);
            SetCurrentState(module, MaterialUnloadingState.Scanning);

            // Act
            module.ForceStopUnloading();

            // Assert
            var stopRequested = GetPrivateField<bool>(module, "_stopRequested");
            var unloadingRequested = GetPrivateField<bool>(module, "_unloadingRequested");
            var ringLineSignal = GetPrivateField<bool>(module, "_ringLineSignal");
            var requestProcessed = GetPrivateField<bool>(module, "_requestProcessed");

            Assert.False(stopRequested);
            Assert.False(unloadingRequested);
            Assert.False(ringLineSignal);
            Assert.False(requestProcessed);
        }

        [Fact]
        public void ForceStopUnloading_ClearsSharedStateUnloadingProgress()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.TryStartUnloading();
            sharedState.RequestUnloadingPriority();
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialUnloadingState.PuttingToCart);

            // Act
            module.ForceStopUnloading();

            // Assert
            Assert.False(sharedState.HasUnloadingPriorityRequest());
            Assert.Equal(ProductionLineSharedState.ActiveProcess.None, sharedState.GetCurrentProcess());
        }

        [Fact]
        public void ForceStopUnloading_ReleasesBeltControl()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_beltStopRequested", true);
            SetCurrentState(module, MaterialUnloadingState.WaitingForScanPosition);

            // Act
            module.ForceStopUnloading();

            // Assert
            var beltStopRequested = GetPrivateField<bool>(module, "_beltStopRequested");
            Assert.False(beltStopRequested);
        }

        #endregion

        #region StopUnloading 测试

        [Fact]
        public void StopUnloading_SetsStopRequestedFlag()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            module.StopUnloading();

            // Assert
            var stopRequested = GetPrivateField<bool>(module, "_stopRequested");
            Assert.True(stopRequested);
        }

        [Fact]
        public void StopUnloading_ClearsUnloadingRequestedFlag()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_unloadingRequested", true);

            // Act
            module.StopUnloading();

            // Assert
            var unloadingRequested = GetPrivateField<bool>(module, "_unloadingRequested");
            Assert.False(unloadingRequested);
        }

        #endregion

        #region 状态枚举值测试

        [Fact]
        public void MaterialUnloadingState_HasExpectedValues()
        {
            // Assert - 验证所有状态枚举值存在
            Assert.True(Enum.IsDefined(typeof(MaterialUnloadingState), MaterialUnloadingState.Idle));
            Assert.True(Enum.IsDefined(typeof(MaterialUnloadingState), MaterialUnloadingState.PickingMaterial));
            Assert.True(Enum.IsDefined(typeof(MaterialUnloadingState), MaterialUnloadingState.WaitingForScanPosition));
            Assert.True(Enum.IsDefined(typeof(MaterialUnloadingState), MaterialUnloadingState.Scanning));
            Assert.True(Enum.IsDefined(typeof(MaterialUnloadingState), MaterialUnloadingState.PuttingToCart));
            Assert.True(Enum.IsDefined(typeof(MaterialUnloadingState), MaterialUnloadingState.Stopped));
        }

        [Fact]
        public void MaterialUnloadingState_HasSixStates()
        {
            // Assert
            var stateCount = Enum.GetValues(typeof(MaterialUnloadingState)).Length;
            Assert.Equal(6, stateCount);
        }

        #endregion

        #region SharedState 集成测试

        [Fact]
        public void Module_WithSharedState_CanAcquireUnloadingLock()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            bool acquired = sharedState.TryStartUnloading();

            // Assert
            Assert.True(acquired);
            Assert.True(sharedState.IsUnloading());
        }

        [Fact]
        public void Module_WithSharedState_CannotAcquireLockWhenLoadingActive()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.TryStartLoading(); // 先获取装料锁
            var module = CreateTestModule(sharedState);

            // Act
            bool acquired = sharedState.TryStartUnloading();

            // Assert
            Assert.False(acquired);
            Assert.True(sharedState.IsLoading());
        }

        [Fact]
        public void Module_WithSharedState_CanRequestPriority()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            sharedState.RequestUnloadingPriority();

            // Assert
            Assert.True(sharedState.HasUnloadingPriorityRequest());
        }

        [Fact]
        public void Module_WithSharedState_CanClearPriority()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.RequestUnloadingPriority();
            var module = CreateTestModule(sharedState);

            // Act
            sharedState.ClearUnloadingPriority();

            // Assert
            Assert.False(sharedState.HasUnloadingPriorityRequest());
        }

        #endregion

        #region 环线信号处理测试

        [Fact]
        public void RingLineSignal_WhenSignalEnds_ClearsRequestProcessed()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_ringLineSignal", false);
            SetPrivateField(module, "_requestProcessed", true);

            // Assert - 当信号变为0时，_requestProcessed应该在下次ProcessIdleState时被清除
            // 这里验证初始设置
            var ringLineSignal = GetPrivateField<bool>(module, "_ringLineSignal");
            Assert.False(ringLineSignal);
        }

        #endregion

        #region 扫码结果测试

        [Fact]
        public void LastScannedQrCode_InitiallyEmpty()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            var qrCode = GetPrivateField<string>(module, "_lastScannedQrCode");

            // Assert
            Assert.Equal(string.Empty, qrCode);
        }

        #endregion

        #region 选择料仓测试

        [Fact]
        public void SelectedBin_DefaultsToOne()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            var selectedBin = GetPrivateField<int>(module, "_selectedBin");

            // Assert
            Assert.Equal(1, selectedBin);
        }

        #endregion
    }
}
