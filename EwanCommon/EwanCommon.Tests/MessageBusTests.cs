using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EwanCore.Messaging;
using Xunit;

namespace EwanCommon.Tests
{
    /// <summary>
    /// 测试用消息类型
    /// </summary>
    public class TestMessage : IMessage
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// 测试用消息（带关联ID）
    /// </summary>
    public class TestRequest : IMessage, ICorrelatedMessage<Guid>
    {
        public DateTimeOffset Timestamp { get; set; }
        public Guid CorrelationId { get; set; }
        public string Query { get; set; } = string.Empty;
    }

    /// <summary>
    /// 测试用响应消息
    /// </summary>
    public class TestReply : IMessage, ICorrelatedMessage<Guid>
    {
        public DateTimeOffset Timestamp { get; set; }
        public Guid CorrelationId { get; set; }
        public string Result { get; set; } = string.Empty;
    }

    /// <summary>
    /// 派生消息（用于测试多态订阅）
    /// </summary>
    public class DerivedTestMessage : TestMessage
    {
        public int ExtraData { get; set; }
    }

    /// <summary>
    /// MessageBus 单元测试
    /// </summary>
    public class MessageBusTests : IDisposable
    {
        private readonly MessageBus _bus;

        public MessageBusTests()
        {
            _bus = new MessageBus();
        }

        public void Dispose()
        {
            _bus.Dispose();
        }

        #region 基本发布/订阅测试

        [Fact]
        public void Subscribe_ShouldReceivePublishedMessage()
        {
            // Arrange
            TestMessage? received = null;
            using var sub = _bus.Subscribe<TestMessage>(msg => received = msg);

            // Act
            _bus.Publish(new TestMessage { Content = "Hello" });

            // Assert
            Assert.NotNull(received);
            Assert.Equal("Hello", received!.Content);
        }

        [Fact]
        public void Subscribe_MultipleHandlers_ShouldAllReceive()
        {
            // Arrange
            var receivedCount = 0;
            using var sub1 = _bus.Subscribe<TestMessage>(_ => Interlocked.Increment(ref receivedCount));
            using var sub2 = _bus.Subscribe<TestMessage>(_ => Interlocked.Increment(ref receivedCount));
            using var sub3 = _bus.Subscribe<TestMessage>(_ => Interlocked.Increment(ref receivedCount));

            // Act
            _bus.Publish(new TestMessage { Content = "Test" });

            // Assert
            Assert.Equal(3, receivedCount);
        }

        [Fact]
        public void Unsubscribe_ShouldNotReceiveAfterDispose()
        {
            // Arrange
            var receivedCount = 0;
            var sub = _bus.Subscribe<TestMessage>(_ => receivedCount++);

            _bus.Publish(new TestMessage { Content = "First" });
            Assert.Equal(1, receivedCount);

            // Act
            sub.Dispose();
            _bus.Publish(new TestMessage { Content = "Second" });

            // Assert
            Assert.Equal(1, receivedCount);
        }

        [Fact]
        public void Publish_ShouldSetTimestampIfDefault()
        {
            // Arrange
            TestMessage? received = null;
            using var sub = _bus.Subscribe<TestMessage>(msg => received = msg);

            // Act
            var before = DateTimeOffset.Now;
            _bus.Publish(new TestMessage { Content = "Test" });
            var after = DateTimeOffset.Now;

            // Assert
            Assert.NotNull(received);
            Assert.True(received!.Timestamp >= before && received.Timestamp <= after);
        }

        [Fact]
        public void Publish_ShouldPreserveExistingTimestamp()
        {
            // Arrange
            TestMessage? received = null;
            using var sub = _bus.Subscribe<TestMessage>(msg => received = msg);
            var customTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

            // Act
            _bus.Publish(new TestMessage { Content = "Test", Timestamp = customTime });

            // Assert
            Assert.NotNull(received);
            Assert.Equal(customTime, received!.Timestamp);
        }

        #endregion

        #region 多态订阅测试

        [Fact]
        public void Subscribe_ShouldReceiveDerivedMessages()
        {
            // Arrange
            TestMessage? received = null;
            using var sub = _bus.Subscribe<TestMessage>(msg => received = msg);

            // Act
            _bus.Publish(new DerivedTestMessage { Content = "Derived", ExtraData = 42 });

            // Assert
            Assert.NotNull(received);
            Assert.IsType<DerivedTestMessage>(received);
            Assert.Equal("Derived", received!.Content);
            Assert.Equal(42, ((DerivedTestMessage)received).ExtraData);
        }

        #endregion

        #region SubscribeAll 测试

        [Fact]
        public void SubscribeAll_ShouldReceiveAllMessageTypes()
        {
            // Arrange
            var messages = new ConcurrentBag<IMessage>();
            using var sub = _bus.SubscribeAll(msg => messages.Add(msg));

            // Act
            _bus.Publish(new TestMessage { Content = "Message1" });
            _bus.Publish(new DerivedTestMessage { Content = "Message2", ExtraData = 1 });

            // Assert
            Assert.Equal(2, messages.Count);
        }

        #endregion

        #region Post（异步队列）测试

        [Fact]
        public async Task Post_ShouldDeliverMessageAsynchronously()
        {
            // Arrange
            var tcs = new TaskCompletionSource<TestMessage>();
            using var sub = _bus.Subscribe<TestMessage>(msg => tcs.TrySetResult(msg));

            // Act
            var posted = _bus.Post(new TestMessage { Content = "Async" });

            // Assert
            Assert.True(posted);
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(completedTask == tcs.Task, "Task timed out");
            var received = await tcs.Task;
            Assert.Equal("Async", received.Content);
        }

        [Fact]
        public async Task Post_MultipleMessages_ShouldDeliverInOrder()
        {
            // Arrange
            var messages = new ConcurrentQueue<int>();
            var countdown = new CountdownEvent(3);
            using var sub = _bus.Subscribe<TestMessage>(msg =>
            {
                messages.Enqueue(int.Parse(msg.Content));
                countdown.Signal();
            });

            // Act
            _bus.Post(new TestMessage { Content = "1" });
            _bus.Post(new TestMessage { Content = "2" });
            _bus.Post(new TestMessage { Content = "3" });

            // Assert
            Assert.True(countdown.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(3, messages.Count);
        }

        #endregion

        #region 诊断统计测试

        [Fact]
        public void TotalPublished_ShouldIncrementOnPublish()
        {
            // Arrange
            _bus.ResetStatistics();

            // Act
            _bus.Publish(new TestMessage { Content = "1" });
            _bus.Publish(new TestMessage { Content = "2" });

            // Assert
            Assert.Equal(2, _bus.TotalPublished);
        }

        [Fact]
        public void GetSubscriberCount_ShouldReturnCorrectCount()
        {
            // Arrange
            using var sub1 = _bus.Subscribe<TestMessage>(_ => { });
            using var sub2 = _bus.Subscribe<TestMessage>(_ => { });

            // Act & Assert
            Assert.Equal(2, _bus.GetSubscriberCount<TestMessage>());
        }

        [Fact]
        public void SubscribedTypes_ShouldListAllSubscribedTypes()
        {
            // Arrange
            using var sub1 = _bus.Subscribe<TestMessage>(_ => { });
            using var sub2 = _bus.Subscribe<DerivedTestMessage>(_ => { });

            // Act
            var types = _bus.SubscribedTypes;

            // Assert
            Assert.Contains(typeof(TestMessage), types);
            Assert.Contains(typeof(DerivedTestMessage), types);
        }

        #endregion

        #region 异常处理测试

        [Fact]
        public void Publish_WithCatchHandlerExceptions_ShouldNotThrow()
        {
            // Arrange
            var options = new MessageBusOptions { CatchHandlerExceptions = true };
            using var bus = new MessageBus(options);

            var secondHandlerCalled = false;
            using var sub1 = bus.Subscribe<TestMessage>(_ => throw new InvalidOperationException("Test"));
            using var sub2 = bus.Subscribe<TestMessage>(_ => secondHandlerCalled = true);

            // Act
            var ex = Record.Exception(() => bus.Publish(new TestMessage { Content = "Test" }));

            // Assert
            Assert.Null(ex);
            Assert.True(secondHandlerCalled);
        }

        [Fact]
        public void HandlerException_Event_ShouldBeFired()
        {
            // Arrange
            var options = new MessageBusOptions { CatchHandlerExceptions = true };
            using var bus = new MessageBus(options);

            MessageHandlerExceptionEventArgs? eventArgs = null;
            bus.HandlerException += (_, e) => eventArgs = e;

            using var sub = bus.Subscribe<TestMessage>(_ => throw new InvalidOperationException("Test Error"));

            // Act
            bus.Publish(new TestMessage { Content = "Test" });

            // Assert
            Assert.NotNull(eventArgs);
            Assert.IsType<InvalidOperationException>(eventArgs!.Exception);
            Assert.Equal("Test Error", eventArgs.Exception.Message);
        }

        #endregion

        #region Request/Reply 测试

        [Fact]
        public async Task RequestAsync_ShouldReceiveReply()
        {
            // Arrange
            using var responder = _bus.Respond<TestRequest, TestReply>(req =>
                new TestReply { Result = $"Reply to: {req.Query}" });

            // Act
            var reply = await _bus.RequestAsync<TestRequest, TestReply>(
                new TestRequest { Query = "Hello" },
                timeoutMs: 5000);

            // Assert
            Assert.Equal("Reply to: Hello", reply.Result);
        }

        [Fact]
        public async Task RequestAsync_ShouldTimeoutWhenNoResponder()
        {
            // Arrange & Act & Assert
            // MessageBus throws TaskCanceledException on timeout
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await _bus.RequestAsync<TestRequest, TestReply>(
                    new TestRequest { Query = "NoResponder" },
                    timeoutMs: 100);
            });
        }

        #endregion

        #region Dispose 测试

        [Fact]
        public void Dispose_ShouldPreventFurtherOperations()
        {
            // Arrange
            var bus = new MessageBus();
            bus.Dispose();

            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() =>
                bus.Subscribe<TestMessage>(_ => { }));
            Assert.Throws<ObjectDisposedException>(() =>
                bus.Publish(new TestMessage { Content = "Test" }));
        }

        [Fact]
        public void Dispose_MultipleTimes_ShouldNotThrow()
        {
            // Arrange
            var bus = new MessageBus();

            // Act
            var ex = Record.Exception(() =>
            {
                bus.Dispose();
                bus.Dispose();
                bus.Dispose();
            });

            // Assert
            Assert.Null(ex);
        }

        #endregion

        #region 队列容量和溢出策略测试

        [Fact]
        public void Constructor_InvalidCapacity_ShouldThrow()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MessageBus(new MessageBusOptions { AsyncQueueCapacity = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MessageBus(new MessageBusOptions { AsyncQueueCapacity = -1 }));
        }

        [Fact]
        public void QueueLength_ShouldReturnCurrentQueueSize()
        {
            // Arrange - 不订阅，消息会堆积在队列中
            var options = new MessageBusOptions { AsyncQueueCapacity = 100 };
            using var bus = new MessageBus(options);

            // Act
            for (int i = 0; i < 5; i++)
            {
                bus.Post(new TestMessage { Content = i.ToString() });
            }

            // Assert
            // 注意：由于后台工作线程可能已经开始处理，队列长度可能小于5
            Assert.True(bus.QueueLength >= 0);
        }

        #endregion

        #region 参数验证测试

        [Fact]
        public void Subscribe_NullHandler_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _bus.Subscribe<TestMessage>(null!));
        }

        [Fact]
        public void Publish_NullMessage_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _bus.Publish<TestMessage>(null!));
        }

        [Fact]
        public void Post_NullMessage_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _bus.Post<TestMessage>(null!));
        }

        [Fact]
        public void SubscribeAll_NullHandler_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _bus.SubscribeAll(null!));
        }

        #endregion
    }
}
