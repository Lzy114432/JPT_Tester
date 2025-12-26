using System;
using EwanCore.Plc.Cmd;
using Xunit;

namespace EwanCommon.Tests
{
    /// <summary>
    /// Deque 双端队列单元测试
    /// </summary>
    public class DequeTests
    {
        #region 基本入队/出队测试

        [Fact]
        public void EnqueueFirst_DequeueFirst_ShouldWorkLikeStack()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act
            deque.EnqueueFirst(1);
            deque.EnqueueFirst(2);
            deque.EnqueueFirst(3);

            // Assert - LIFO 顺序
            Assert.Equal(3, deque.DequeueFirst());
            Assert.Equal(2, deque.DequeueFirst());
            Assert.Equal(1, deque.DequeueFirst());
        }

        [Fact]
        public void EnqueueLast_DequeueFirst_ShouldWorkLikeQueue()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act
            deque.EnqueueLast(1);
            deque.EnqueueLast(2);
            deque.EnqueueLast(3);

            // Assert - FIFO 顺序
            Assert.Equal(1, deque.DequeueFirst());
            Assert.Equal(2, deque.DequeueFirst());
            Assert.Equal(3, deque.DequeueFirst());
        }

        [Fact]
        public void EnqueueFirst_DequeueLast_ShouldWorkLikeQueue()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act
            deque.EnqueueFirst(1);
            deque.EnqueueFirst(2);
            deque.EnqueueFirst(3);

            // Assert - FIFO 顺序（反向）
            Assert.Equal(1, deque.DequeueLast());
            Assert.Equal(2, deque.DequeueLast());
            Assert.Equal(3, deque.DequeueLast());
        }

        [Fact]
        public void EnqueueLast_DequeueLast_ShouldWorkLikeStack()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act
            deque.EnqueueLast(1);
            deque.EnqueueLast(2);
            deque.EnqueueLast(3);

            // Assert - LIFO 顺序
            Assert.Equal(3, deque.DequeueLast());
            Assert.Equal(2, deque.DequeueLast());
            Assert.Equal(1, deque.DequeueLast());
        }

        #endregion

        #region 空队列测试

        [Fact]
        public void DequeueFirst_EmptyDeque_ShouldThrow()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => deque.DequeueFirst());
            Assert.Equal("Deque is empty", ex.Message);
        }

        [Fact]
        public void DequeueLast_EmptyDeque_ShouldThrow()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => deque.DequeueLast());
            Assert.Equal("Deque is empty", ex.Message);
        }

        #endregion

        #region 容量限制测试

        [Fact]
        public void EnqueueFirst_ExceedsCapacity_ShouldRemoveLast()
        {
            // Arrange
            var deque = new Deque<int>(3);

            // Act
            deque.EnqueueFirst(1);
            deque.EnqueueFirst(2);
            deque.EnqueueFirst(3);
            deque.EnqueueFirst(4); // 应该移除 1

            // Assert
            Assert.Equal(4, deque.DequeueFirst());
            Assert.Equal(3, deque.DequeueFirst());
            Assert.Equal(2, deque.DequeueFirst());
            Assert.Throws<InvalidOperationException>(() => deque.DequeueFirst()); // 1 已被移除
        }

        [Fact]
        public void EnqueueLast_ExceedsCapacity_ShouldRemoveFirst()
        {
            // Arrange
            var deque = new Deque<int>(3);

            // Act
            deque.EnqueueLast(1);
            deque.EnqueueLast(2);
            deque.EnqueueLast(3);
            deque.EnqueueLast(4); // 应该移除 1

            // Assert
            Assert.Equal(2, deque.DequeueFirst()); // 1 已被移除
            Assert.Equal(3, deque.DequeueFirst());
            Assert.Equal(4, deque.DequeueFirst());
        }

        [Fact]
        public void Capacity_ZeroOrNegative_ShouldNotLimit()
        {
            // Arrange
            var dequeZero = new Deque<int>(0);
            var dequeNegative = new Deque<int>(-1);

            // Act
            for (int i = 0; i < 100; i++)
            {
                dequeZero.EnqueueLast(i);
                dequeNegative.EnqueueLast(i);
            }

            // Assert - 所有元素都应该保留
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(i, dequeZero.DequeueFirst());
                Assert.Equal(i, dequeNegative.DequeueFirst());
            }
        }

        [Fact]
        public void Capacity_One_ShouldOnlyKeepLatest()
        {
            // Arrange
            var deque = new Deque<int>(1);

            // Act
            deque.EnqueueLast(1);
            deque.EnqueueLast(2);
            deque.EnqueueLast(3);

            // Assert
            Assert.Equal(3, deque.DequeueFirst());
            Assert.Throws<InvalidOperationException>(() => deque.DequeueFirst());
        }

        #endregion

        #region 混合操作测试

        [Fact]
        public void MixedOperations_ShouldMaintainCorrectOrder()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act
            deque.EnqueueLast(1);   // [1]
            deque.EnqueueLast(2);   // [1, 2]
            deque.EnqueueFirst(0);  // [0, 1, 2]
            deque.EnqueueFirst(-1); // [-1, 0, 1, 2]
            deque.EnqueueLast(3);   // [-1, 0, 1, 2, 3]

            // Assert
            Assert.Equal(-1, deque.DequeueFirst()); // [0, 1, 2, 3]
            Assert.Equal(3, deque.DequeueLast());   // [0, 1, 2]
            Assert.Equal(0, deque.DequeueFirst());  // [1, 2]
            Assert.Equal(2, deque.DequeueLast());   // [1]
            Assert.Equal(1, deque.DequeueFirst());  // []
        }

        [Fact]
        public void AlternatingEnqueueDequeue_ShouldWork()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act & Assert
            deque.EnqueueLast(1);
            Assert.Equal(1, deque.DequeueFirst());

            deque.EnqueueFirst(2);
            Assert.Equal(2, deque.DequeueLast());

            deque.EnqueueLast(3);
            deque.EnqueueFirst(4);
            Assert.Equal(4, deque.DequeueFirst());
            Assert.Equal(3, deque.DequeueLast());
        }

        #endregion

        #region 不同数据类型测试

        [Fact]
        public void Deque_WithStringType_ShouldWork()
        {
            // Arrange
            var deque = new Deque<string>();

            // Act
            deque.EnqueueLast("Hello");
            deque.EnqueueLast("World");

            // Assert
            Assert.Equal("Hello", deque.DequeueFirst());
            Assert.Equal("World", deque.DequeueFirst());
        }

        [Fact]
        public void Deque_WithNullableType_ShouldWork()
        {
            // Arrange
            var deque = new Deque<string?>();

            // Act
            deque.EnqueueLast(null);
            deque.EnqueueLast("Value");
            deque.EnqueueLast(null);

            // Assert
            Assert.Null(deque.DequeueFirst());
            Assert.Equal("Value", deque.DequeueFirst());
            Assert.Null(deque.DequeueFirst());
        }

        [Fact]
        public void Deque_WithReferenceType_ShouldWork()
        {
            // Arrange
            var deque = new Deque<object>();
            var obj1 = new object();
            var obj2 = new object();

            // Act
            deque.EnqueueLast(obj1);
            deque.EnqueueLast(obj2);

            // Assert
            Assert.Same(obj1, deque.DequeueFirst());
            Assert.Same(obj2, deque.DequeueFirst());
        }

        #endregion

        #region 边界条件测试

        [Fact]
        public void SingleElement_EnqueueDequeue_ShouldWork()
        {
            // Arrange
            var deque = new Deque<int>();

            // Act & Assert
            deque.EnqueueFirst(42);
            Assert.Equal(42, deque.DequeueFirst());
            Assert.Throws<InvalidOperationException>(() => deque.DequeueFirst());

            deque.EnqueueLast(43);
            Assert.Equal(43, deque.DequeueLast());
            Assert.Throws<InvalidOperationException>(() => deque.DequeueLast());
        }

        [Fact]
        public void LargeNumberOfElements_ShouldWork()
        {
            // Arrange
            var deque = new Deque<int>();
            var count = 10000;

            // Act
            for (int i = 0; i < count; i++)
            {
                deque.EnqueueLast(i);
            }

            // Assert
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(i, deque.DequeueFirst());
            }
        }

        [Fact]
        public void Capacity_ExactlyFilled_ShouldNotOverflow()
        {
            // Arrange
            var capacity = 5;
            var deque = new Deque<int>(capacity);

            // Act
            for (int i = 0; i < capacity; i++)
            {
                deque.EnqueueLast(i);
            }

            // Assert - 所有元素都应该存在
            for (int i = 0; i < capacity; i++)
            {
                Assert.Equal(i, deque.DequeueFirst());
            }
        }

        #endregion

        #region 使用场景测试

        [Fact]
        public void UseAsRecentHistory_ShouldKeepMostRecentItems()
        {
            // Arrange - 只保留最近3条记录
            var historyDeque = new Deque<string>(3);

            // Act - 模拟添加历史记录
            historyDeque.EnqueueLast("操作1");
            historyDeque.EnqueueLast("操作2");
            historyDeque.EnqueueLast("操作3");
            historyDeque.EnqueueLast("操作4"); // 操作1 被移除
            historyDeque.EnqueueLast("操作5"); // 操作2 被移除

            // Assert - 只保留最近3条
            Assert.Equal("操作3", historyDeque.DequeueFirst());
            Assert.Equal("操作4", historyDeque.DequeueFirst());
            Assert.Equal("操作5", historyDeque.DequeueFirst());
        }

        [Fact]
        public void UseAsPriorityQueue_HighPriorityFirst()
        {
            // Arrange
            var priorityDeque = new Deque<string>();

            // Act - 正常任务从后面加，高优先级任务从前面加
            priorityDeque.EnqueueLast("普通任务1");
            priorityDeque.EnqueueLast("普通任务2");
            priorityDeque.EnqueueFirst("紧急任务"); // 插队到最前面

            // Assert - 从前面取，紧急任务先执行
            Assert.Equal("紧急任务", priorityDeque.DequeueFirst());
            Assert.Equal("普通任务1", priorityDeque.DequeueFirst());
            Assert.Equal("普通任务2", priorityDeque.DequeueFirst());
        }

        #endregion
    }
}
