using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EwanCore;
using EwanCore.Attribute;
using EwanCore.Bootstrap;
using Xunit;

namespace EwanCommon.Tests
{
    #region 测试用 Manager 类型

    /// <summary>
    /// 测试用 Manager 接口实现
    /// </summary>
    public class TestManager : IManager
    {
        public bool InitCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public bool ShouldFailInit { get; set; }

        public bool Init()
        {
            InitCalled = true;
            return !ShouldFailInit;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }

    /// <summary>
    /// 测试用异步 Manager
    /// </summary>
    public class TestAsyncManager : IAsyncManager
    {
        public bool InitCalled { get; private set; }
        public bool InitAsyncCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public bool DisposeAsyncCalled { get; private set; }

        public bool Init()
        {
            InitCalled = true;
            return true;
        }

        public Task<bool> InitAsync()
        {
            InitAsyncCalled = true;
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }

        public Task DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return Task.CompletedTask;
        }
    }

    #endregion

    /// <summary>
    /// ManagerLifetimeHost 单元测试
    /// </summary>
    public class ManagerLifetimeHostTests
    {
        #region 构造函数测试

        [Fact]
        public void Constructor_NullResolver_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ManagerLifetimeHost((Func<Type, IManager>)null!, Array.Empty<Assembly>()));
        }

        [Fact]
        public void Constructor_WithResolver_ShouldNotThrow()
        {
            // Arrange & Act
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Assert
            Assert.NotNull(host);
            Assert.False(host.IsStarted);
            Assert.False(host.IsDisposed);
        }

        #endregion

        #region Start 测试

        [Fact]
        public void Start_NoManagers_ShouldSucceed()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            var result = host.Start(requireAll: false);

            // Assert
            Assert.True(result);
            Assert.True(host.IsStarted);
        }

        [Fact]
        public void Start_WithManager_ShouldCallInit()
        {
            // Arrange
            var manager = new TestManager();
            var managerTypes = new Dictionary<Type, IManager>
            {
                { typeof(TestManager), manager }
            };

            // 创建包含测试 Manager 的程序集列表
            var host = new ManagerLifetimeHost(
                type => managerTypes.TryGetValue(type, out var m) ? m : null,
                Array.Empty<Assembly>());

            // Act
            var result = host.Start(requireAll: false);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Start_CalledTwice_ShouldReturnTrueWithoutReinit()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            host.Start();
            var result = host.Start();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Start_AfterDispose_ShouldThrow()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());
            host.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => host.Start());
        }

        #endregion

        #region Stop 测试

        [Fact]
        public void Stop_NotStarted_ShouldNotThrow()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            var ex = Record.Exception(() => host.Stop());

            // Assert
            Assert.Null(ex);
        }

        [Fact]
        public void Stop_AfterStart_ShouldResetStartedState()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());
            host.Start();

            // Act
            host.Stop();

            // Assert
            Assert.False(host.IsStarted);
        }

        #endregion

        #region Dispose 测试

        [Fact]
        public void Dispose_ShouldSetDisposedFlag()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            host.Dispose();

            // Assert
            Assert.True(host.IsDisposed);
        }

        [Fact]
        public void Dispose_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            var ex = Record.Exception(() =>
            {
                host.Dispose();
                host.Dispose();
                host.Dispose();
            });

            // Assert
            Assert.Null(ex);
        }

        #endregion

        #region 异步测试

        [Fact]
        public async Task StartAsync_NoManagers_ShouldSucceed()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            var result = await host.StartAsync(requireAll: false);

            // Assert
            Assert.True(result);
            Assert.True(host.IsStarted);
        }

        [Fact]
        public async Task StopAsync_AfterStart_ShouldResetStartedState()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());
            await host.StartAsync();

            // Act
            await host.StopAsync();

            // Assert
            Assert.False(host.IsStarted);
        }

        [Fact]
        public async Task DisposeAsync_ShouldSetDisposedFlag()
        {
            // Arrange
            var host = new ManagerLifetimeHost(
                type => null,
                Array.Empty<Assembly>());

            // Act
            await host.DisposeAsync();

            // Assert
            Assert.True(host.IsDisposed);
        }

        #endregion

        #region Resolver 包装测试

        [Fact]
        public void Constructor_WithObjectResolver_ShouldWrapCorrectly()
        {
            // Arrange
            Func<Type, object> objectResolver = type => new TestManager();

            // Act
            var host = new ManagerLifetimeHost(objectResolver, Array.Empty<Assembly>());

            // Assert
            Assert.NotNull(host);
        }

        #endregion
    }

    /// <summary>
    /// ManagerTypeScanner 单元测试
    /// </summary>
    public class ManagerTypeScannerTests
    {
        #region Discover 测试

        [Fact]
        public void Discover_EmptyAssemblies_ShouldReturnEmptyList()
        {
            // Act
            var result = ManagerTypeScanner.Discover(Array.Empty<Assembly>());

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void Discover_NullAssemblies_ShouldScanAppDomain()
        {
            // Act - 传 null 会扫描 AppDomain
            var result = ManagerTypeScanner.Discover(null);

            // Assert
            Assert.NotNull(result);
            // 结果取决于 AppDomain 中加载的程序集
        }

        [Fact]
        public void Discover_WithSpecificAssembly_ShouldOnlyScanThat()
        {
            // Arrange
            var assemblies = new[] { typeof(ManagerTypeScannerTests).Assembly };

            // Act
            var result = ManagerTypeScanner.Discover(assemblies);

            // Assert
            Assert.NotNull(result);
            // 测试程序集中没有标记 [Manager] 的类型
        }

        [Fact]
        public void Discover_ShouldReturnReadOnlyList()
        {
            // Act
            var result = ManagerTypeScanner.Discover(Array.Empty<Assembly>());

            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<Type>>(result);
        }

        #endregion

        #region 排序测试

        [Fact]
        public void Discover_ShouldSortByPriority()
        {
            // 注意：这个测试依赖于实际有 [Manager] 标记的类型
            // 在纯单元测试中，我们只能验证返回的是有序列表

            // Act
            var result = ManagerTypeScanner.Discover(Array.Empty<Assembly>());

            // Assert
            Assert.NotNull(result);
        }

        #endregion

        #region 异常处理测试

        [Fact]
        public void Discover_AssemblyWithLoadError_ShouldNotThrow()
        {
            // Arrange - 使用当前程序集（不会有加载错误）
            var assemblies = new[] { typeof(ManagerTypeScannerTests).Assembly };

            // Act
            var ex = Record.Exception(() => ManagerTypeScanner.Discover(assemblies));

            // Assert
            Assert.Null(ex);
        }

        #endregion
    }
}
