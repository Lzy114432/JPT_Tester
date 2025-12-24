using System;
using EwanIO.Core.Interfaces;
using EwanIO.Hardware.Composite;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    /// <summary>
    /// CompositeHardwareIO 组合硬件测试
    ///
    /// 测试目的：
    /// CompositeHardwareIO 是一个将多个硬件 IO 设备组合为单一逻辑设备的类。
    /// 例如：IOC0640 (64输入64输出) + SMC606 (40输入34输出) = 组合设备 (104输入98输出)
    ///
    /// 主要测试内容：
    /// 1. 硬件连接和断开 - 验证多个子硬件的连接管理
    /// 2. 全局位索引映射 - 验证全局索引正确映射到对应的子硬件
    /// 3. DataSync 同步 - 验证所有子硬件都被同步
    /// 4. Dispose 资源释放 - 验证所有子硬件都被正确释放
    /// 5. 边界条件 - 验证无效索引、未连接状态等边界情况
    /// </summary>
    public class CompositeHardwareIOTests : IDisposable
    {
        private CompositeHardwareIO _composite;

        public void Dispose()
        {
            _composite?.Dispose();
        }

        #region 基本连接测试 - 验证硬件连接和断开功能

        /// <summary>
        /// 测试：单个硬件连接应该成功
        /// 场景：只添加一个子硬件时，组合设备应该能正常连接
        /// </summary>
        [Fact]
        public void Connect_WithSingleHardware_ShouldSucceed()
        {
            // Arrange - 创建单个 8 输入 8 输出的内存硬件
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            // Act - 连接组合设备
            bool result = _composite.Connect("");

            // Assert - 验证连接成功且 IO 数量正确
            Assert.True(result);
            Assert.True(_composite.IsConnected);
            Assert.Equal(8, _composite.InputCount);
            Assert.Equal(8, _composite.OutputCount);
        }

        /// <summary>
        /// 测试：多个硬件连接应该全部成功，且 IO 数量累加
        /// 场景：添加多个子硬件时，总输入输出数应该是各子硬件的总和
        /// </summary>
        [Fact]
        public void Connect_WithMultipleHardware_ShouldConnectAll()
        {
            // Arrange - 创建三个不同大小的硬件
            var hw1 = new InMemoryHardwareIO(8, 8);    // 8 输入, 8 输出
            var hw2 = new InMemoryHardwareIO(16, 16);  // 16 输入, 16 输出
            var hw3 = new InMemoryHardwareIO(4, 4);    // 4 输入, 4 输出

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2")
                .AddHardware(hw3, "hw3");

            // Act
            bool result = _composite.Connect("");

            // Assert - 总数应该是 8+16+4=28
            Assert.True(result);
            Assert.True(_composite.IsConnected);
            Assert.Equal(28, _composite.InputCount);
            Assert.Equal(28, _composite.OutputCount);
            Assert.Equal(3, _composite.HardwareCount);
        }

        /// <summary>
        /// 测试：没有添加任何硬件时，连接应该失败
        /// 场景：空的组合设备不应该能连接
        /// </summary>
        [Fact]
        public void Connect_WithNoHardware_ShouldFail()
        {
            // Arrange - 创建空的组合设备
            _composite = new CompositeHardwareIO();

            // Act
            bool result = _composite.Connect("");

            // Assert - 连接应该失败
            Assert.False(result);
            Assert.False(_composite.IsConnected);
        }

        /// <summary>
        /// 测试：断开连接应该断开所有子硬件并重置计数
        /// 场景：断开后，IO 数量应该归零
        /// </summary>
        [Fact]
        public void Disconnect_ShouldDisconnectAllHardware()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            var hw2 = new InMemoryHardwareIO(8, 8);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            _composite.Connect("");

            // Act - 断开连接
            bool result = _composite.Disconnect();

            // Assert - 状态应该重置
            Assert.True(result);
            Assert.False(_composite.IsConnected);
            Assert.Equal(0, _composite.InputCount);
            Assert.Equal(0, _composite.OutputCount);
        }

        /// <summary>
        /// 测试：连接后尝试添加硬件应该抛出异常
        /// 场景：防止在运行时动态添加硬件导致索引混乱
        /// </summary>
        [Fact]
        public void AddHardware_AfterConnect_ShouldThrow()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert - 连接后添加硬件应该抛出 InvalidOperationException
            var hw2 = new InMemoryHardwareIO(8, 8);
            Assert.Throws<InvalidOperationException>(() =>
                _composite.AddHardware(hw2, "hw2"));
        }

        /// <summary>
        /// 测试：添加 null 硬件应该抛出异常
        /// 场景：参数验证
        /// </summary>
        [Fact]
        public void AddHardware_WithNull_ShouldThrow()
        {
            // Arrange
            _composite = new CompositeHardwareIO();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _composite.AddHardware(null, "test"));
        }

        #endregion

        #region 输入读取测试 - 验证全局输入索引到子硬件的映射

        /// <summary>
        /// 测试：使用全局索引读取输入应该映射到正确的子硬件
        /// 场景：
        /// - hw1: 输入 0-7 (本地 0-7)
        /// - hw2: 输入 8-15 (本地 0-7)
        /// - hw3: 输入 16-23 (本地 0-7)
        /// 全局索引 11 应该映射到 hw2 的本地索引 3
        /// </summary>
        [Fact]
        public void ReadInBit_WithGlobalIndex_ShouldMapToCorrectHardware()
        {
            // Arrange - 三个硬件，每个 8 个输入
            var hw1 = new InMemoryHardwareIO(8, 8);   // 全局输入 0-7
            var hw2 = new InMemoryHardwareIO(8, 8);   // 全局输入 8-15
            var hw3 = new InMemoryHardwareIO(8, 8);   // 全局输入 16-23

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2")
                .AddHardware(hw3, "hw3");

            _composite.Connect("");

            // Act - 在各个子硬件上设置输入（使用本地索引）
            hw1.SetInputBit(0, true);   // 全局索引 0
            hw1.SetInputBit(7, true);   // 全局索引 7 (hw1 的最后一位)
            hw2.SetInputBit(0, true);   // 全局索引 8 (hw2 的第一位)
            hw2.SetInputBit(3, true);   // 全局索引 11
            hw3.SetInputBit(5, true);   // 全局索引 21

            // Assert - 通过组合设备的全局索引读取
            Assert.True(_composite.ReadInBit(0));    // hw1[0]
            Assert.True(_composite.ReadInBit(7));    // hw1[7]
            Assert.True(_composite.ReadInBit(8));    // hw2[0]
            Assert.True(_composite.ReadInBit(11));   // hw2[3]
            Assert.True(_composite.ReadInBit(21));   // hw3[5]

            // 验证未设置的位为 false
            Assert.False(_composite.ReadInBit(1));   // hw1[1] - 未设置
            Assert.False(_composite.ReadInBit(10));  // hw2[2] - 未设置
            Assert.False(_composite.ReadInBit(20));  // hw3[4] - 未设置
        }

        /// <summary>
        /// 测试：使用无效索引读取输入应该返回 false
        /// 场景：负数索引、超出范围的索引
        /// </summary>
        [Fact]
        public void ReadInBit_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange - 8 个输入的组合设备
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert - 无效索引应该返回 false
            Assert.False(_composite.ReadInBit(-1));   // 负数索引
            Assert.False(_composite.ReadInBit(8));    // 刚好超出范围
            Assert.False(_composite.ReadInBit(100));  // 远超范围
        }

        /// <summary>
        /// 测试：未连接时读取输入应该返回 false
        /// 场景：安全性检查，防止未连接时访问
        /// </summary>
        [Fact]
        public void ReadInBit_WhenNotConnected_ShouldReturnFalse()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            // 注意：不调用 Connect()

            // Act & Assert
            Assert.False(_composite.ReadInBit(0));
        }

        #endregion

        #region 输出读写测试 - 验证全局输出索引到子硬件的映射

        /// <summary>
        /// 测试：使用全局索引写入输出应该写入正确的子硬件
        /// 场景：
        /// - hw1: 输出 0-7 (本地 0-7)
        /// - hw2: 输出 8-15 (本地 0-7)
        /// 全局索引 8 应该写入 hw2 的本地索引 0
        /// </summary>
        [Fact]
        public void WriteOutBit_WithGlobalIndex_ShouldWriteToCorrectHardware()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);   // 全局输出 0-7
            var hw2 = new InMemoryHardwareIO(8, 8);   // 全局输出 8-15

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            _composite.Connect("");

            // Act - 通过组合设备写入（使用全局索引）
            _composite.WriteOutBit(0, true);   // 应该写入 hw1[0]
            _composite.WriteOutBit(7, true);   // 应该写入 hw1[7]
            _composite.WriteOutBit(8, true);   // 应该写入 hw2[0]
            _composite.WriteOutBit(15, true);  // 应该写入 hw2[7]

            // Assert - 直接从子硬件验证（使用本地索引）
            Assert.True(hw1.ReadOutBit(0));
            Assert.True(hw1.ReadOutBit(7));
            Assert.True(hw2.ReadOutBit(0));
            Assert.True(hw2.ReadOutBit(7));

            // 验证未写入的位为 false
            Assert.False(hw1.ReadOutBit(1));
            Assert.False(hw2.ReadOutBit(1));
        }

        /// <summary>
        /// 测试：使用全局索引读取输出应该从正确的子硬件读取
        /// </summary>
        [Fact]
        public void ReadOutBit_WithGlobalIndex_ShouldReadFromCorrectHardware()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            var hw2 = new InMemoryHardwareIO(8, 8);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            _composite.Connect("");

            // 通过组合设备写入
            _composite.WriteOutBit(5, true);   // hw1[5]
            _composite.WriteOutBit(10, true);  // hw2[2]

            // Act & Assert - 通过组合设备读取验证
            Assert.True(_composite.ReadOutBit(5));
            Assert.True(_composite.ReadOutBit(10));
            Assert.False(_composite.ReadOutBit(0));
            Assert.False(_composite.ReadOutBit(8));
        }

        /// <summary>
        /// 测试：使用无效索引写入输出应该返回 false
        /// </summary>
        [Fact]
        public void WriteOutBit_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert
            Assert.False(_composite.WriteOutBit(-1, true));
            Assert.False(_composite.WriteOutBit(8, true));
            Assert.False(_composite.WriteOutBit(100, true));
        }

        /// <summary>
        /// 测试：使用无效索引读取输出应该返回 false
        /// </summary>
        [Fact]
        public void ReadOutBit_WithInvalidIndex_ShouldReturnFalse()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert
            Assert.False(_composite.ReadOutBit(-1));
            Assert.False(_composite.ReadOutBit(8));
            Assert.False(_composite.ReadOutBit(100));
        }

        #endregion

        #region DataSync 测试 - 验证数据同步传播到所有子硬件

        /// <summary>
        /// 测试：DataSync 应该同步所有子硬件
        /// 场景：调用组合设备的 DataSync 时，每个子硬件的 DataSync 都应该被调用
        /// </summary>
        [Fact]
        public void DataSync_ShouldSyncAllHardware()
        {
            // Arrange - 使用 Mock 硬件来计数 DataSync 调用
            var hw1 = new MockSyncCountingHardware(8, 8);
            var hw2 = new MockSyncCountingHardware(8, 8);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            _composite.Connect("");

            // Act - 调用 3 次 DataSync
            _composite.DataSync();
            _composite.DataSync();
            _composite.DataSync();

            // Assert - 每个子硬件都应该被同步 3 次
            Assert.Equal(3, hw1.DataSyncCount);
            Assert.Equal(3, hw2.DataSyncCount);
        }

        /// <summary>
        /// 测试：未连接时调用 DataSync 不应该抛出异常
        /// 场景：安全性检查
        /// </summary>
        [Fact]
        public void DataSync_WhenNotConnected_ShouldNotThrow()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            // 不连接

            // Act & Assert - 不应抛出异常
            var ex = Record.Exception(() => _composite.DataSync());
            Assert.Null(ex);
        }

        #endregion

        #region 属性和辅助方法测试 - 验证元数据和辅助功能

        /// <summary>
        /// 测试：HardwareType 应该返回 "Composite"
        /// </summary>
        [Fact]
        public void HardwareType_ShouldReturnComposite()
        {
            _composite = new CompositeHardwareIO();
            Assert.Equal("Composite", _composite.HardwareType);
        }

        /// <summary>
        /// 测试：ConnectionInfo 应该包含所有子硬件的类型信息
        /// </summary>
        [Fact]
        public void ConnectionInfo_ShouldContainAllHardwareTypes()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            var hw2 = new InMemoryHardwareIO(8, 8);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            // Act
            string info = _composite.ConnectionInfo;

            // Assert - 应该包含 "InMemory" 和 "+"
            Assert.Contains("InMemory", info);
            Assert.Contains("+", info);
        }

        /// <summary>
        /// 测试：GetHardware 应该返回正确的子硬件实例
        /// </summary>
        [Fact]
        public void GetHardware_WithValidIndex_ShouldReturnHardware()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            var hw2 = new InMemoryHardwareIO(16, 16);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            _composite.Connect("");

            // Act & Assert - 应该返回相同的实例
            Assert.Same(hw1, _composite.GetHardware(0));
            Assert.Same(hw2, _composite.GetHardware(1));
        }

        /// <summary>
        /// 测试：GetHardware 使用无效索引应该返回 null
        /// </summary>
        [Fact]
        public void GetHardware_WithInvalidIndex_ShouldReturnNull()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert
            Assert.Null(_composite.GetHardware(-1));
            Assert.Null(_composite.GetHardware(1));
            Assert.Null(_composite.GetHardware(100));
        }

        /// <summary>
        /// 测试：GetInputOffset 应该返回每个子硬件的输入起始偏移量
        /// 场景：hw1(8输入) + hw2(16输入) + hw3(4输入)
        /// - hw1 偏移: 0
        /// - hw2 偏移: 8
        /// - hw3 偏移: 24
        /// </summary>
        [Fact]
        public void GetInputOffset_ShouldReturnCorrectOffset()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            var hw2 = new InMemoryHardwareIO(16, 16);
            var hw3 = new InMemoryHardwareIO(4, 4);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2")
                .AddHardware(hw3, "hw3");

            _composite.Connect("");

            // Act & Assert
            Assert.Equal(0, _composite.GetInputOffset(0));   // hw1 从 0 开始
            Assert.Equal(8, _composite.GetInputOffset(1));   // hw2 从 8 开始
            Assert.Equal(24, _composite.GetInputOffset(2));  // hw3 从 8+16=24 开始
        }

        /// <summary>
        /// 测试：GetOutputOffset 应该返回每个子硬件的输出起始偏移量
        /// 场景：输入和输出数量可以不同
        /// </summary>
        [Fact]
        public void GetOutputOffset_ShouldReturnCorrectOffset()
        {
            // Arrange - 输出数量不同于输入数量
            var hw1 = new InMemoryHardwareIO(8, 4);   // 4 输出
            var hw2 = new InMemoryHardwareIO(8, 8);   // 8 输出
            var hw3 = new InMemoryHardwareIO(8, 2);   // 2 输出

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2")
                .AddHardware(hw3, "hw3");

            _composite.Connect("");

            // Act & Assert
            Assert.Equal(0, _composite.GetOutputOffset(0));   // hw1 从 0 开始
            Assert.Equal(4, _composite.GetOutputOffset(1));   // hw2 从 4 开始
            Assert.Equal(12, _composite.GetOutputOffset(2));  // hw3 从 4+8=12 开始
        }

        /// <summary>
        /// 测试：GetOffset 使用无效索引应该返回 -1
        /// </summary>
        [Fact]
        public void GetOffset_WithInvalidIndex_ShouldReturnNegativeOne()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert
            Assert.Equal(-1, _composite.GetInputOffset(-1));
            Assert.Equal(-1, _composite.GetInputOffset(5));
            Assert.Equal(-1, _composite.GetOutputOffset(-1));
            Assert.Equal(-1, _composite.GetOutputOffset(5));
        }

        #endregion

        #region Dispose 测试 - 验证资源释放

        /// <summary>
        /// 测试：Dispose 应该释放所有子硬件
        /// 场景：验证级联释放
        /// </summary>
        [Fact]
        public void Dispose_ShouldDisposeAllChildHardware()
        {
            // Arrange - 使用 Mock 硬件来跟踪 Dispose 状态
            var hw1 = new MockDisposableHardware(8, 8);
            var hw2 = new MockDisposableHardware(8, 8);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2");

            _composite.Connect("");

            // Act
            _composite.Dispose();

            // Assert - 所有子硬件都应该被释放
            Assert.True(hw1.IsDisposed);
            Assert.True(hw2.IsDisposed);
        }

        /// <summary>
        /// 测试：多次 Dispose 不应该抛出异常
        /// 场景：防御性编程，防止重复释放导致错误
        /// </summary>
        [Fact]
        public void Dispose_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");

            // Act & Assert - 多次 Dispose 不应抛出异常
            _composite.Dispose();
            var ex = Record.Exception(() => _composite.Dispose());
            Assert.Null(ex);
        }

        /// <summary>
        /// 测试：Dispose 后调用任何操作应该抛出 ObjectDisposedException
        /// 场景：验证已释放对象的保护
        /// </summary>
        [Fact]
        public void AfterDispose_Operations_ShouldThrow()
        {
            // Arrange
            var hw1 = new InMemoryHardwareIO(8, 8);
            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1");

            _composite.Connect("");
            _composite.Dispose();

            // Act & Assert - 所有操作都应该抛出 ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => _composite.Connect(""));
            Assert.Throws<ObjectDisposedException>(() => _composite.ReadInBit(0));
            Assert.Throws<ObjectDisposedException>(() => _composite.WriteOutBit(0, true));
            Assert.Throws<ObjectDisposedException>(() => _composite.DataSync());
            Assert.Throws<ObjectDisposedException>(() =>
                _composite.AddHardware(new InMemoryHardwareIO(8, 8), "test"));
        }

        #endregion

        #region 链式调用测试 - 验证 Fluent API

        /// <summary>
        /// 测试：AddHardware 应该支持链式调用
        /// 场景：验证 Fluent API 风格
        /// </summary>
        [Fact]
        public void AddHardware_ShouldSupportChaining()
        {
            // Arrange & Act - 链式调用
            var hw1 = new InMemoryHardwareIO(8, 8);
            var hw2 = new InMemoryHardwareIO(8, 8);
            var hw3 = new InMemoryHardwareIO(8, 8);

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2")
                .AddHardware(hw3, "hw3");

            _composite.Connect("");

            // Assert
            Assert.Equal(3, _composite.HardwareCount);
            Assert.Equal(24, _composite.InputCount);
            Assert.Equal(24, _composite.OutputCount);
        }

        #endregion

        #region 边界情况测试 - 验证极端和特殊场景

        /// <summary>
        /// 测试：大型组合设备应该正常工作
        /// 场景：10 个硬件，每个 64 IO，总共 640 IO
        /// </summary>
        [Fact]
        public void LargeComposite_ShouldWorkCorrectly()
        {
            // Arrange - 创建大型组合（10 个硬件，每个 64 IO）
            _composite = new CompositeHardwareIO();

            for (int i = 0; i < 10; i++)
            {
                _composite.AddHardware(new InMemoryHardwareIO(64, 64), $"hw{i}");
            }

            _composite.Connect("");

            // Assert - 总数应该是 640
            Assert.Equal(640, _composite.InputCount);
            Assert.Equal(640, _composite.OutputCount);

            // Act - 测试边界位
            _composite.WriteOutBit(0, true);     // 第一个硬件的第一位
            _composite.WriteOutBit(63, true);    // 第一个硬件的最后一位
            _composite.WriteOutBit(64, true);    // 第二个硬件的第一位
            _composite.WriteOutBit(639, true);   // 最后一个硬件的最后一位

            Assert.True(_composite.ReadOutBit(0));
            Assert.True(_composite.ReadOutBit(63));
            Assert.True(_composite.ReadOutBit(64));
            Assert.True(_composite.ReadOutBit(639));
        }

        /// <summary>
        /// 测试：不同大小的硬件组合应该正确映射
        /// 场景：输入和输出数量各不相同的硬件组合
        /// </summary>
        [Fact]
        public void DifferentSizedHardware_ShouldMapCorrectly()
        {
            // Arrange - 不同大小的硬件
            var hw1 = new InMemoryHardwareIO(3, 5);    // 3 输入, 5 输出
            var hw2 = new InMemoryHardwareIO(7, 2);    // 7 输入, 2 输出
            var hw3 = new InMemoryHardwareIO(10, 8);   // 10 输入, 8 输出

            _composite = new CompositeHardwareIO()
                .AddHardware(hw1, "hw1")
                .AddHardware(hw2, "hw2")
                .AddHardware(hw3, "hw3");

            _composite.Connect("");

            // Assert - 验证总数
            Assert.Equal(20, _composite.InputCount);   // 3 + 7 + 10
            Assert.Equal(15, _composite.OutputCount);  // 5 + 2 + 8

            // 验证输入映射
            // hw1: 输入 0-2, hw2: 输入 3-9, hw3: 输入 10-19
            hw1.SetInputBit(2, true);   // 全局 2 (hw1 的最后一位)
            hw2.SetInputBit(0, true);   // 全局 3 (hw2 的第一位)
            hw3.SetInputBit(9, true);   // 全局 19 (hw3 的最后一位)

            Assert.True(_composite.ReadInBit(2));
            Assert.True(_composite.ReadInBit(3));
            Assert.True(_composite.ReadInBit(19));

            // 验证输出映射
            // hw1: 输出 0-4, hw2: 输出 5-6, hw3: 输出 7-14
            _composite.WriteOutBit(4, true);   // hw1[4] (hw1 的最后一位)
            _composite.WriteOutBit(5, true);   // hw2[0] (hw2 的第一位)
            _composite.WriteOutBit(14, true);  // hw3[7] (hw3 的最后一位)

            Assert.True(hw1.ReadOutBit(4));
            Assert.True(hw2.ReadOutBit(0));
            Assert.True(hw3.ReadOutBit(7));
        }

        #endregion

        #region Mock 类 - 用于测试的模拟硬件

        /// <summary>
        /// Mock 硬件：用于测试 DataSync 调用次数
        /// 需要完整实现 IHardwareIO 因为 InMemoryHardwareIO.DataSync 不是 virtual
        /// </summary>
        private class MockSyncCountingHardware : IHardwareIO
        {
            private readonly bool[] _inputs;
            private readonly bool[] _outputs;

            /// <summary>DataSync 被调用的次数</summary>
            public int DataSyncCount { get; private set; }

            public string HardwareType => "MockSyncCounting";
            public string ConnectionInfo { get; private set; } = "";
            public bool IsConnected { get; private set; }
            public int InputCount { get; }
            public int OutputCount { get; }

            public MockSyncCountingHardware(int inputCount, int outputCount)
            {
                InputCount = inputCount;
                OutputCount = outputCount;
                _inputs = new bool[inputCount];
                _outputs = new bool[outputCount];
            }

            public bool Connect(string connectionString)
            {
                ConnectionInfo = connectionString;
                IsConnected = true;
                return true;
            }

            public bool Disconnect()
            {
                IsConnected = false;
                return true;
            }

            public void DataSync()
            {
                DataSyncCount++;  // 记录调用次数
            }

            public bool ReadInBit(int bit) =>
                bit >= 0 && bit < InputCount && _inputs[bit];

            public bool ReadOutBit(int bit) =>
                bit >= 0 && bit < OutputCount && _outputs[bit];

            public bool WriteOutBit(int bit, bool value)
            {
                if (bit >= 0 && bit < OutputCount)
                {
                    _outputs[bit] = value;
                    return true;
                }
                return false;
            }

            public void Dispose() => Disconnect();
        }

        /// <summary>
        /// Mock 硬件：用于测试 Dispose 是否被调用
        /// </summary>
        private class MockDisposableHardware : IHardwareIO
        {
            private readonly bool[] _inputs;
            private readonly bool[] _outputs;

            /// <summary>是否已被释放</summary>
            public bool IsDisposed { get; private set; }

            public string HardwareType => "MockDisposable";
            public string ConnectionInfo { get; private set; } = "";
            public bool IsConnected { get; private set; }
            public int InputCount { get; }
            public int OutputCount { get; }

            public MockDisposableHardware(int inputCount, int outputCount)
            {
                InputCount = inputCount;
                OutputCount = outputCount;
                _inputs = new bool[inputCount];
                _outputs = new bool[outputCount];
            }

            public bool Connect(string connectionString)
            {
                ConnectionInfo = connectionString;
                IsConnected = true;
                return true;
            }

            public bool Disconnect()
            {
                IsConnected = false;
                return true;
            }

            public void DataSync() { }

            public bool ReadInBit(int bit) =>
                bit >= 0 && bit < InputCount && _inputs[bit];

            public bool ReadOutBit(int bit) =>
                bit >= 0 && bit < OutputCount && _outputs[bit];

            public bool WriteOutBit(int bit, bool value)
            {
                if (bit >= 0 && bit < OutputCount)
                {
                    _outputs[bit] = value;
                    return true;
                }
                return false;
            }

            public void Dispose()
            {
                IsDisposed = true;  // 标记已释放
                Disconnect();
            }
        }

        #endregion
    }
}
