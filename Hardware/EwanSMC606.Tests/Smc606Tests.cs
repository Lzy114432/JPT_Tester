using System;
using Xunit;

namespace EwanSMC606.Tests
{
    internal sealed class FakeSmc606NativeApi : ISmc606NativeApi
    {
        public int InitCalls { get; private set; }
        public int CloseCalls { get; private set; }

        public short BoardInit(ushort cardNo, ushort connectType, string connectString, uint baudRate)
        {
            InitCalls++;
            return 0;
        }

        public short BoardClose(ushort cardNo)
        {
            CloseCalls++;
            return 0;
        }
    }

    public class Smc606ConnectionPoolTests
    {
        [Fact]
        public void TryAcquireExisting_WhenNotConnected_ShouldReturnFalse()
        {
            Assert.False(Smc606ConnectionPool.TryAcquireExisting(999, out var lease));
            Assert.Null(lease);
        }

        [Fact]
        public void Acquire_SameCard_ShouldInitOnce_AndCloseOnce()
        {
            var native = new FakeSmc606NativeApi();
            Smc606ConnectionPool.NativeApi = native;

            var options = new Smc606ConnectionOptions
            {
                CardNo = 10,
                ConnectType = 2,
                ConnectString = "192.168.1.100",
                BaudRate = 115200
            };

            using (var lease1 = Smc606ConnectionPool.Acquire(options))
            {
                using (var lease2 = Smc606ConnectionPool.Acquire(options))
                {
                    Assert.Equal(1, native.InitCalls);
                    Assert.Same(lease1.SyncRoot, lease2.SyncRoot);
                }

                Assert.Equal(0, native.CloseCalls);
            }

            Assert.Equal(1, native.CloseCalls);
        }

        [Fact]
        public void Acquire_DifferentCards_ShouldInitAndCloseForEach()
        {
            var native = new FakeSmc606NativeApi();
            Smc606ConnectionPool.NativeApi = native;

            var options1 = new Smc606ConnectionOptions
            {
                CardNo = 20,
                ConnectType = 2,
                ConnectString = "192.168.1.100",
                BaudRate = 115200
            };
            var options2 = new Smc606ConnectionOptions
            {
                CardNo = 21,
                ConnectType = 2,
                ConnectString = "192.168.1.101",
                BaudRate = 115200
            };

            using (Smc606ConnectionPool.Acquire(options1))
            using (Smc606ConnectionPool.Acquire(options2))
            {
                Assert.Equal(2, native.InitCalls);
                Assert.Equal(0, native.CloseCalls);
            }

            Assert.Equal(2, native.CloseCalls);
        }

        [Fact]
        public void TryAcquireExisting_WhenConnected_ShouldNotInitAgain()
        {
            var native = new FakeSmc606NativeApi();
            Smc606ConnectionPool.NativeApi = native;

            var options = new Smc606ConnectionOptions
            {
                CardNo = 30,
                ConnectType = 2,
                ConnectString = "192.168.1.100",
                BaudRate = 115200
            };

            using (var lease1 = Smc606ConnectionPool.Acquire(options))
            {
                Assert.Equal(1, native.InitCalls);

                Assert.True(Smc606ConnectionPool.TryAcquireExisting(30, out var lease2));
                Assert.NotNull(lease2);

                using (lease2!)
                {
                    Assert.Equal(1, native.InitCalls);
                    Assert.Same(lease1.SyncRoot, lease2!.SyncRoot);
                }

                Assert.Equal(0, native.CloseCalls);
            }

            Assert.Equal(1, native.CloseCalls);
        }
    }

    public class Smc606ConnectionOptionsTests
    {
        [Fact]
        public void Validate_EthernetWithoutAddress_ShouldThrow()
        {
            var options = new Smc606ConnectionOptions
            {
                CardNo = 0,
                ConnectType = 2,
                ConnectString = " ",
                BaudRate = 115200
            };

            var ex = Assert.Throws<ArgumentException>(() => Smc606ConnectionPool.Acquire(options));
            Assert.Contains("ConnectString", ex.Message);
        }

        [Fact]
        public void Clone_ShouldCopyValues()
        {
            var options = new Smc606ConnectionOptions
            {
                CardNo = 1,
                ConnectType = 1,
                ConnectString = "",
                BaudRate = 1000000
            };

            var clone = options.Clone();
            Assert.NotSame(options, clone);
            Assert.Equal(options.CardNo, clone.CardNo);
            Assert.Equal(options.ConnectType, clone.ConnectType);
            Assert.Equal(options.ConnectString, clone.ConnectString);
            Assert.Equal(options.BaudRate, clone.BaudRate);
        }
    }
}
