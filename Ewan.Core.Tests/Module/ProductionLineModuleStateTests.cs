using Ewan.Core.Module;
using System;
using System.Reflection;
using Xunit;

namespace Ewan.Core.Tests.Module
{
    /// <summary>
    /// ProductionLineModule 状态转换测试
    /// 特征测试：验证当前行为的系统状态转换逻辑
    /// </summary>
    public class ProductionLineModuleStateTests
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
        /// 创建测试用模块实例（不调用Init）
        /// </summary>
        private static ProductionLineModule CreateTestModule()
        {
            return new ProductionLineModule();
        }

        #endregion

        #region 初始状态测试

        [Fact]
        public void Constructor_InitializesSystemReadyToFalse()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var systemReady = GetPrivateField<bool>(module, "_systemReady");
            Assert.False(systemReady);
        }

        [Fact]
        public void Constructor_InitializesIsRunningToFalse()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var isRunning = GetPrivateField<bool>(module, "_isRunning");
            Assert.False(isRunning);
        }

        [Fact]
        public void Constructor_InitializesIsPausedToFalse()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var isPaused = GetPrivateField<bool>(module, "_isPaused");
            Assert.False(isPaused);
        }

        [Fact]
        public void Constructor_InitializesInitializedToFalse()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var initialized = GetPrivateField<bool>(module, "_initialized");
            Assert.False(initialized);
        }

        #endregion

        #region StartProduction 状态转换测试

        [Fact]
        public void StartProduction_WhenNotInitialized_DoesNotSetIsRunning()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", false);

            // Act
            module.StartProduction();

            // Assert
            var isRunning = GetPrivateField<bool>(module, "_isRunning");
            Assert.False(isRunning);
        }

        [Fact]
        public void StartProduction_WhenInitialized_SetsIsRunningToTrue()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);

            // Act
            module.StartProduction();

            // Assert
            var isRunning = GetPrivateField<bool>(module, "_isRunning");
            Assert.True(isRunning);
        }

        [Fact]
        public void StartProduction_ClearsPausedState()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isPaused", true);

            // Act
            module.StartProduction();

            // Assert
            var isPaused = GetPrivateField<bool>(module, "_isPaused");
            Assert.False(isPaused);
        }

        #endregion

        #region StopProduction 状态转换测试

        [Fact]
        public void StopProduction_SetsIsRunningToFalse()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.StopProduction();

            // Assert
            var isRunning = GetPrivateField<bool>(module, "_isRunning");
            Assert.False(isRunning);
        }

        [Fact]
        public void StopProduction_SetsInitializedToFalse()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.StopProduction();

            // Assert
            var initialized = GetPrivateField<bool>(module, "_initialized");
            Assert.False(initialized);
        }

        [Fact]
        public void StopProduction_ClearsPausedState()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isPaused", true);
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.StopProduction();

            // Assert
            var isPaused = GetPrivateField<bool>(module, "_isPaused");
            Assert.False(isPaused);
        }

        #endregion

        #region PauseProduction 状态转换测试

        [Fact]
        public void PauseProduction_SetsIsPausedToTrue()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isPaused", false);

            // Act
            module.PauseProduction();

            // Assert
            var isPaused = GetPrivateField<bool>(module, "_isPaused");
            Assert.True(isPaused);
        }

        [Fact]
        public void PauseProduction_DoesNotChangeIsRunning()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.PauseProduction();

            // Assert
            var isRunning = GetPrivateField<bool>(module, "_isRunning");
            Assert.True(isRunning);
        }

        #endregion

        #region ResumeProduction 状态转换测试

        [Fact]
        public void ResumeProduction_SetsIsPausedToFalse()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isPaused", true);

            // Act
            module.ResumeProduction();

            // Assert
            var isPaused = GetPrivateField<bool>(module, "_isPaused");
            Assert.False(isPaused);
        }

        #endregion

        #region EmergencyStop 状态转换测试

        [Fact]
        public void EmergencyStop_SetsIsRunningToFalse()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.EmergencyStop();

            // Assert
            var isRunning = GetPrivateField<bool>(module, "_isRunning");
            Assert.False(isRunning);
        }

        [Fact]
        public void EmergencyStop_SetsInitializedToFalse()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.EmergencyStop();

            // Assert
            var initialized = GetPrivateField<bool>(module, "_initialized");
            Assert.False(initialized);
        }

        [Fact]
        public void EmergencyStop_ClearsPausedState()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_isPaused", true);

            // Act
            module.EmergencyStop();

            // Assert
            var isPaused = GetPrivateField<bool>(module, "_isPaused");
            Assert.False(isPaused);
        }

        #endregion

        #region 状态组合测试

        [Fact]
        public void StateTransition_NotReady_To_Initialized()
        {
            // Arrange
            var module = CreateTestModule();
            Assert.False(GetPrivateField<bool>(module, "_initialized"));

            // Act - 模拟初始化完成
            SetPrivateField(module, "_initialized", true);

            // Assert
            Assert.True(GetPrivateField<bool>(module, "_initialized"));
            Assert.False(GetPrivateField<bool>(module, "_isRunning"));
        }

        [Fact]
        public void StateTransition_Initialized_To_Running()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            Assert.False(GetPrivateField<bool>(module, "_isRunning"));

            // Act
            module.StartProduction();

            // Assert
            Assert.True(GetPrivateField<bool>(module, "_initialized"));
            Assert.True(GetPrivateField<bool>(module, "_isRunning"));
            Assert.False(GetPrivateField<bool>(module, "_isPaused"));
        }

        [Fact]
        public void StateTransition_Running_To_Paused()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);
            Assert.False(GetPrivateField<bool>(module, "_isPaused"));

            // Act
            module.PauseProduction();

            // Assert
            Assert.True(GetPrivateField<bool>(module, "_isRunning"));
            Assert.True(GetPrivateField<bool>(module, "_isPaused"));
        }

        [Fact]
        public void StateTransition_Paused_To_Running()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);
            SetPrivateField(module, "_isPaused", true);

            // Act
            module.ResumeProduction();

            // Assert
            Assert.True(GetPrivateField<bool>(module, "_isRunning"));
            Assert.False(GetPrivateField<bool>(module, "_isPaused"));
        }

        [Fact]
        public void StateTransition_Running_To_Stopped()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.StopProduction();

            // Assert
            Assert.False(GetPrivateField<bool>(module, "_initialized"));
            Assert.False(GetPrivateField<bool>(module, "_isRunning"));
            Assert.False(GetPrivateField<bool>(module, "_isPaused"));
        }

        [Fact]
        public void StateTransition_Paused_To_Stopped()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);
            SetPrivateField(module, "_isPaused", true);

            // Act
            module.StopProduction();

            // Assert
            Assert.False(GetPrivateField<bool>(module, "_initialized"));
            Assert.False(GetPrivateField<bool>(module, "_isRunning"));
            Assert.False(GetPrivateField<bool>(module, "_isPaused"));
        }

        [Fact]
        public void StateTransition_Running_To_EmergencyStop()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);

            // Act
            module.EmergencyStop();

            // Assert
            Assert.False(GetPrivateField<bool>(module, "_initialized"));
            Assert.False(GetPrivateField<bool>(module, "_isRunning"));
        }

        [Fact]
        public void StateTransition_Paused_To_EmergencyStop()
        {
            // Arrange
            var module = CreateTestModule();
            SetPrivateField(module, "_initialized", true);
            SetPrivateField(module, "_isRunning", true);
            SetPrivateField(module, "_isPaused", true);

            // Act
            module.EmergencyStop();

            // Assert
            Assert.False(GetPrivateField<bool>(module, "_initialized"));
            Assert.False(GetPrivateField<bool>(module, "_isRunning"));
            Assert.False(GetPrivateField<bool>(module, "_isPaused"));
        }

        #endregion

        #region 模块启用/禁用配置测试

        [Fact]
        public void LoadingEnabled_InitiallyTrue()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var loadingEnabled = GetPrivateField<bool>(module, "_loadingEnabled");
            Assert.True(loadingEnabled);
        }

        [Fact]
        public void UnloadingEnabled_InitiallyTrue()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var unloadingEnabled = GetPrivateField<bool>(module, "_unloadingEnabled");
            Assert.True(unloadingEnabled);
        }

        #endregion

        #region 扫描间隔测试

        [Fact]
        public void ScanInterval_DefaultsTo100ms()
        {
            // Arrange & Act
            var module = CreateTestModule();

            // Assert
            var scanInterval = GetPrivateField<int>(module, "_scanInterval");
            Assert.Equal(100, scanInterval);
        }

        #endregion
    }
}
