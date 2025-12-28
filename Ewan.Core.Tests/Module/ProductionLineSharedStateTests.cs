using Ewan.Core.Module;
using System.Threading.Tasks;
using Xunit;

namespace Ewan.Core.Tests.Module
{
    /// <summary>
    /// ProductionLineSharedState 测试
    /// 验证共享状态的线程安全性和状态管理逻辑
    /// </summary>
    public class ProductionLineSharedStateTests
    {
        #region 初始状态测试

        [Fact]
        public void Constructor_InitializesAllStatesToDefault()
        {
            // Arrange & Act
            var sharedState = new ProductionLineSharedState();

            // Assert
            Assert.False(sharedState.GetLoadingCompleted());
            Assert.False(sharedState.GetUnloadingCompleted());
            Assert.False(sharedState.IsSystemPaused());
            Assert.False(sharedState.RequireReinit());
            Assert.False(sharedState.IsLoadingInProgress());
        }

        #endregion

        #region 装载完成状态测试

        [Fact]
        public void SetLoadingCompleted_True_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.SetLoadingCompleted(true);

            // Assert
            Assert.True(sharedState.GetLoadingCompleted());
        }

        [Fact]
        public void SetLoadingCompleted_False_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetLoadingCompleted(true);

            // Act
            sharedState.SetLoadingCompleted(false);

            // Assert
            Assert.False(sharedState.GetLoadingCompleted());
        }

        [Fact]
        public void LoadingCompleted_Property_WorksCorrectly()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.LoadingCompleted = true;

            // Assert
            Assert.True(sharedState.LoadingCompleted);
        }

        #endregion

        #region 卸载完成状态测试

        [Fact]
        public void SetUnloadingCompleted_True_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.SetUnloadingCompleted(true);

            // Assert
            Assert.True(sharedState.GetUnloadingCompleted());
        }

        [Fact]
        public void SetUnloadingCompleted_False_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetUnloadingCompleted(true);

            // Act
            sharedState.SetUnloadingCompleted(false);

            // Assert
            Assert.False(sharedState.GetUnloadingCompleted());
        }

        [Fact]
        public void UnloadingCompleted_Property_WorksCorrectly()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.UnloadingCompleted = true;

            // Assert
            Assert.True(sharedState.UnloadingCompleted);
        }

        #endregion

        #region 系统暂停状态测试

        [Fact]
        public void SetSystemPaused_True_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.SetSystemPaused(true);

            // Assert
            Assert.True(sharedState.IsSystemPaused());
        }

        [Fact]
        public void SetSystemPaused_False_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetSystemPaused(true);

            // Act
            sharedState.SetSystemPaused(false);

            // Assert
            Assert.False(sharedState.IsSystemPaused());
        }

        #endregion

        #region 重新初始化标志测试

        [Fact]
        public void SetRequireReinit_True_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.SetRequireReinit(true);

            // Assert
            Assert.True(sharedState.RequireReinit());
        }

        [Fact]
        public void SetRequireReinit_False_UpdatesState()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetRequireReinit(true);

            // Act
            sharedState.SetRequireReinit(false);

            // Assert
            Assert.False(sharedState.RequireReinit());
        }

        #endregion

        #region 装料流程标志测试

        [Fact]
        public void MarkLoadingInProgress_SetsFlag()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();

            // Act
            sharedState.MarkLoadingInProgress();

            // Assert
            Assert.True(sharedState.IsLoadingInProgress());
        }

        [Fact]
        public void ClearLoadingInProgress_ClearsFlag()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.MarkLoadingInProgress();

            // Act
            sharedState.ClearLoadingInProgress();

            // Assert
            Assert.False(sharedState.IsLoadingInProgress());
        }

        #endregion

        #region ResetAllStates 测试

        [Fact]
        public void ResetAllStates_ClearsAllFlags()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            sharedState.SetLoadingCompleted(true);
            sharedState.SetUnloadingCompleted(true);
            sharedState.SetSystemPaused(true);
            sharedState.SetRequireReinit(true);

            // Act
            sharedState.ResetAllStates();

            // Assert
            Assert.False(sharedState.GetLoadingCompleted());
            Assert.False(sharedState.GetUnloadingCompleted());
            Assert.False(sharedState.IsSystemPaused());
            Assert.False(sharedState.RequireReinit());
        }

        #endregion

        #region 线程安全测试

        [Fact]
        public void ConcurrentStateUpdates_ThreadSafe()
        {
            // Arrange
            var sharedState = new ProductionLineSharedState();
            var tasks = new Task[200];

            // Act
            for (int i = 0; i < 100; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    sharedState.SetLoadingCompleted(true);
                    sharedState.SetLoadingCompleted(false);
                });
            }

            for (int i = 100; i < 200; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    sharedState.SetUnloadingCompleted(true);
                    sharedState.SetUnloadingCompleted(false);
                });
            }

            Task.WaitAll(tasks);

            // Assert - 无异常即通过
            _ = sharedState.GetLoadingCompleted();
            _ = sharedState.GetUnloadingCompleted();
        }

        #endregion
    }
}
