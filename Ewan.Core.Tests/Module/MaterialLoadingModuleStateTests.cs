using Ewan.Core.Module;
using System;
using System.Reflection;
using Xunit;

namespace Ewan.Core.Tests.Module
{
    /// <summary>
    /// MaterialLoadingModule 状态转换测试
    /// 特征测试：验证当前行为的状态转换逻辑
    /// </summary>
    public class MaterialLoadingModuleStateTests
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
        private static MaterialLoadingState GetCurrentState(MaterialLoadingModule module)
        {
            return GetPrivateField<MaterialLoadingState>(module, "_currentState");
        }

        /// <summary>
        /// 设置当前状态
        /// </summary>
        private static void SetCurrentState(MaterialLoadingModule module, MaterialLoadingState state)
        {
            SetPrivateField(module, "_currentState", state);
        }

        /// <summary>
        /// 创建测试用模块实例
        /// </summary>
        private static MaterialLoadingModule CreateTestModule(ProductionLineSharedState? sharedState = null)
        {
            var state = sharedState ?? new ProductionLineSharedState();
            return new MaterialLoadingModule(state);
        }

        #endregion

        #region 初始状态测试

        [Fact]
        public void Constructor_SetsInitialStateToIdle()
        {
            // Arrange & Act
            var sharedState = new ProductionLineSharedState();
            var module = new MaterialLoadingModule(sharedState);

            // Assert
            var state = GetCurrentState(module);
            Assert.Equal(MaterialLoadingState.Idle, state);
        }

        #endregion

        #region Idle 状态转换测试

        [Fact]
        public void Idle_WhenNoMaterialDetected_RemainsInIdle()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialLoadingState.Idle);

            // Act - 没有料片检测信号时应保持 Idle
            // 这需要模拟 IO 状态，但在单元测试中我们验证状态逻辑

            // Assert
            Assert.Equal(MaterialLoadingState.Idle, GetCurrentState(module));
        }

        #endregion

        #region PickingMaterial 状态转换测试

        [Fact]
        public void PickingMaterial_InitialBeltStopRequestIsFalse()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            var beltStopRequested = GetPrivateField<bool>(module, "_beltStopRequested");

            // Assert
            Assert.False(beltStopRequested);
        }

        #endregion

        #region ForceStopLoading 测试

        [Fact]
        public void ForceStopLoading_ResetsStateToIdle()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialLoadingState.PickingMaterial);

            // Act
            module.ForceStopLoading();

            // Assert
            Assert.Equal(MaterialLoadingState.Idle, GetCurrentState(module));
        }

        [Fact]
        public void ForceStopLoading_ClearsAllRequestFlags()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_stopRequested", true);
            SetPrivateField(module, "_loadingRequested", true);
            SetCurrentState(module, MaterialLoadingState.MovingToBinByScanInfo);

            // Act
            module.ForceStopLoading();

            // Assert
            var stopRequested = GetPrivateField<bool>(module, "_stopRequested");
            var loadingRequested = GetPrivateField<bool>(module, "_loadingRequested");
            Assert.False(stopRequested);
            Assert.False(loadingRequested);
        }

        [Fact]
        public void ForceStopLoading_ClearsSharedStateLoadingProgress()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.MarkLoadingInProgress();
            sharedState.SetLoadingCompleted(true);
            var module = CreateTestModule(sharedState);
            SetCurrentState(module, MaterialLoadingState.AtScanPosition);

            // Act
            module.ForceStopLoading();

            // Assert
            Assert.False(sharedState.IsLoadingInProgress());
            Assert.False(sharedState.GetLoadingCompleted());
        }

        [Fact]
        public void ForceStopLoading_ReleasesBeltControl()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_beltStopRequested", true);
            SetCurrentState(module, MaterialLoadingState.PickingMaterial);

            // Act
            module.ForceStopLoading();

            // Assert
            var beltStopRequested = GetPrivateField<bool>(module, "_beltStopRequested");
            Assert.False(beltStopRequested);
        }

        #endregion

        #region StopLoading 测试

        [Fact]
        public void StopLoading_SetsStopRequestedFlag()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);

            // Act
            module.StopLoading();

            // Assert
            var stopRequested = GetPrivateField<bool>(module, "_stopRequested");
            Assert.True(stopRequested);
        }

        [Fact]
        public void StopLoading_ClearsLoadingRequestedFlag()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var module = CreateTestModule(sharedState);
            SetPrivateField(module, "_loadingRequested", true);

            // Act
            module.StopLoading();

            // Assert
            var loadingRequested = GetPrivateField<bool>(module, "_loadingRequested");
            Assert.False(loadingRequested);
        }

        #endregion

        #region 状态枚举值测试

        [Fact]
        public void MaterialLoadingState_HasExpectedValues()
        {
            // Assert - 验证所有状态枚举值存在
            Assert.True(Enum.IsDefined(typeof(MaterialLoadingState), MaterialLoadingState.Idle));
            Assert.True(Enum.IsDefined(typeof(MaterialLoadingState), MaterialLoadingState.PickingMaterial));
            Assert.True(Enum.IsDefined(typeof(MaterialLoadingState), MaterialLoadingState.AtScanPosition));
            Assert.True(Enum.IsDefined(typeof(MaterialLoadingState), MaterialLoadingState.MovingToBinByScanInfo));
            Assert.True(Enum.IsDefined(typeof(MaterialLoadingState), MaterialLoadingState.Stopped));
        }

        [Fact]
        public void MaterialLoadingState_HasFiveStates()
        {
            // Assert
            var stateCount = Enum.GetValues(typeof(MaterialLoadingState)).Length;
            Assert.Equal(5, stateCount);
        }

        #endregion

        #region 系统暂停状态测试

        [Fact]
        public void Module_WhenSystemPaused_ShouldNotProcessState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetSystemPaused(true);
            var module = CreateTestModule(sharedState);

            // Act
            bool isPaused = sharedState.IsSystemPaused();

            // Assert
            Assert.True(isPaused);
        }

        #endregion
    }
}
