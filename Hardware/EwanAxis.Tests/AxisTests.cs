using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EwanAxis.Core.Interfaces;
using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanAxis.Tests
{
    internal sealed class TestAxis : IAxis
    {
        private volatile bool _isBusy;
        private volatile bool _isHomed;
        private volatile bool _isAlarm;
        private volatile bool _servoOn;

        public AxisParameter Parameter { get; set; }
        public string Name => Parameter.Name;
        public int AxisIndex => Parameter.AxisNum;
        public double Position { get; set; }
        public double FeedbackPosition { get; set; }
        public bool IsBusy => _isBusy;
        public bool ServoOn
        {
            get => _servoOn;
            set => _servoOn = value;
        }

        public bool IsAlarm => _isAlarm;
        public bool IsHomed => _isHomed;

        public int MoveDelayMs { get; set; } = 50;
        public int HomeDelayMs { get; set; } = 50;
        public bool CompleteMove { get; set; } = true;
        public bool CompleteHome { get; set; } = true;

        public TestAxis(string name, int axisIndex)
        {
            Parameter = new AxisParameter { Name = name, AxisNum = axisIndex };
            _servoOn = true;
        }

        public void Home()
        {
            _isHomed = false;
            _isBusy = true;

            if (!CompleteHome) return;

            Task.Run(async () =>
            {
                await Task.Delay(HomeDelayMs).ConfigureAwait(false);
                _isHomed = true;
                _isBusy = false;
            });
        }

        public bool HomeIsDown() => _isHomed;

        public bool AbsMove(double pos)
        {
            if (!_servoOn) return false;
            if (_isAlarm) return false;

            _isBusy = true;

            if (!CompleteMove) return true;

            Task.Run(async () =>
            {
                await Task.Delay(MoveDelayMs).ConfigureAwait(false);
                Position = pos;
                FeedbackPosition = pos;
                _isBusy = false;
            });

            return true;
        }

        public bool RelMove(double distance) => AbsMove(Position + distance);

        public bool Jog(double speed)
        {
            if (!_servoOn) return false;
            if (_isAlarm) return false;
            _isBusy = true;
            return true;
        }

        public bool JogStop()
        {
            _isBusy = false;
            return true;
        }

        public void DecStop()
        {
            _isBusy = false;
        }

        public void EmgStop()
        {
            _isBusy = false;
        }

        public void SetMotionParams(double startVelocity, double velocity, double accTime, double decTime)
        {
            Parameter.Speed = velocity;
            Parameter.Acc = accTime;
            Parameter.Dec = decTime;
        }

        public void SetHomeParams(bool homeDir, int homeMode, double velocity, double scale)
        {
            Parameter.HomeDir = homeDir;
            Parameter.HomeMode = homeMode;
            Parameter.HomeSpeed = velocity;
        }

        public void ClearError()
        {
            _isAlarm = false;
        }

        public AxisIOState GetAxisIO()
        {
            return new AxisIOState
            {
                Busy = _isBusy,
                Home = !_isHomed && _isBusy
            };
        }

        public void SetAlarm(bool alarm) => _isAlarm = alarm;
    }

    internal sealed class TestAxisCard : IAxisCard
    {
        private readonly List<IAxis> _axes;
        private bool _disposed;

        public string CardName { get; set; } = "";
        public int CardIndex { get; }
        public bool IsInitialized { get; private set; }
        public bool IsConnected { get; private set; }
        public int AxisCount => _axes.Count;
        public IReadOnlyList<IAxis> Axes => _axes;

        public event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;
        public event EventHandler<AxisAlarmEventArgs>? AxisAlarm;

        public TestAxisCard(int cardIndex, params IAxis[] axes)
        {
            CardIndex = cardIndex;
            _axes = new List<IAxis>(axes ?? Array.Empty<IAxis>());
        }

        public IAxis this[int index] => _axes[index];

        public IAxis? GetAxisByName(string name)
        {
            foreach (var axis in _axes)
            {
                if (axis.Name == name) return axis;
            }
            return null;
        }

        public bool Initialize(string configPath)
        {
            IsInitialized = true;
            return true;
        }

        public bool Connect()
        {
            IsConnected = true;
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(true));
            return true;
        }

        public bool Disconnect()
        {
            IsConnected = false;
            ConnectionChanged?.Invoke(this, new ConnectionChangedEventArgs(false));
            return true;
        }

        public bool SaveConfig(string? configPath = null) => true;

        public void EmgStopAll()
        {
            foreach (var axis in _axes) axis.EmgStop();
        }

        public void ServoOnAll(bool enable)
        {
            foreach (var axis in _axes) axis.ServoOn = enable;
        }

        public void ClearAllErrors()
        {
            foreach (var axis in _axes) axis.ClearError();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
        }
    }

    public class AxisManagerTests
    {
        [Fact]
        public void GetAxisByName_ShouldSearchAllCards()
        {
            var axisX = new TestAxis("X", 0);
            var axisZ = new TestAxis("Z", 0);

            var card1 = new TestAxisCard(1, axisX);
            var card2 = new TestAxisCard(2, axisZ);

            var manager = new AxisManager()
                .AddCard(card1)
                .AddCard(card2);

            Assert.Same(axisZ, manager.GetAxisByName("Z"));
            Assert.Same(axisX, manager.GetAxisByName("X"));
            Assert.Null(manager.GetAxisByName("NotExists"));
        }

        [Fact]
        public void TryGetAxis_ShouldResolveByCardIndexAndAxisIndex()
        {
            var axis0 = new TestAxis("Axis0", 0);
            var axis1 = new TestAxis("Axis1", 1);
            var card = new TestAxisCard(7, axis0, axis1);

            var manager = new AxisManager().AddCard(card);

            Assert.True(manager.TryGetAxis(7, 1, out var axis));
            Assert.Same(axis1, axis);
        }
    }

    public class AxisAsyncExtensionsTests
    {
        [Fact]
        public async Task AbsMoveAsync_ShouldWaitUntilIdle()
        {
            var axis = new TestAxis("X", 0) { MoveDelayMs = 60 };

            var ok = await axis.AbsMoveAsync(123.4, TimeSpan.FromSeconds(1));

            Assert.True(ok);
            Assert.False(axis.IsBusy);
            Assert.Equal(123.4, axis.Position, 3);
        }

        [Fact]
        public async Task WaitIdleAsync_ShouldTimeout_WhenAxisKeepsBusy()
        {
            var axis = new TestAxis("X", 0) { CompleteMove = false };
            axis.Jog(10); // make it busy

            var ok = await axis.WaitIdleAsync(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(10));

            Assert.False(ok);
        }

        [Fact]
        public async Task HomeAsync_ShouldReturnFalse_OnTimeout()
        {
            var axis = new TestAxis("X", 0) { CompleteHome = false };

            var ok = await axis.HomeAsync(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(10));

            Assert.False(ok);
        }
    }

    public class AxisBaseTests
    {
        private sealed class AxisBaseProbe : AxisBase
        {
            public AxisBaseProbe(AxisParameter parameter) : base(parameter)
            {
            }

            public double LastAbsMoveTarget { get; private set; }

            public int PositionToPulsePublic(double position) => PositionToPulse(position);
            public double PulseToPositionPublic(double pulse) => PulseToPosition(pulse);

            public override double Position { get; set; }
            public override double FeedbackPosition { get; set; }
            public override bool IsBusy => false;
            public override bool ServoOn { get; set; }
            public override bool IsAlarm => false;
            public override void Home() { }
            public override bool HomeIsDown() => true;
            public override bool AbsMove(double pos)
            {
                LastAbsMoveTarget = pos;
                return true;
            }
            public override bool Jog(double speed) => true;
            public override bool JogStop() => true;
            public override void DecStop() { }
            public override void EmgStop() { }
            public override void SetMotionParams(double startVelocity, double velocity, double accTime, double decTime) { }
            public override void SetHomeParams(bool homeDir, int homeMode, double velocity, double scale) { }
            public override void ClearError() { }
            public override AxisIOState GetAxisIO() => new AxisIOState();
        }

        [Fact]
        public void RelMove_ShouldCallAbsMove_WithPositionPlusDistance()
        {
            var axis = new AxisBaseProbe(new AxisParameter { Name = "X", AxisNum = 0, Step = 1000 });
            axis.Position = 10;

            axis.RelMove(5);

            Assert.Equal(15, axis.LastAbsMoveTarget);
        }

        [Fact]
        public void PositionToPulse_ShouldApplyStepAndDirection()
        {
            var axis = new AxisBaseProbe(new AxisParameter { Name = "X", AxisNum = 0, Step = 1000, Dir = false });
            Assert.Equal(1500, axis.PositionToPulsePublic(1.5));

            axis.Parameter.Dir = true;
            Assert.Equal(-1500, axis.PositionToPulsePublic(1.5));
        }
    }

    public class AxisCardBaseTests
    {
        private sealed class TestAxisCardBaseImpl : AxisCardBase
        {
            private readonly int _cardIndex;
            public override int CardIndex => _cardIndex;

            public TestAxisCardBaseImpl(int cardIndex)
            {
                _cardIndex = cardIndex;
            }

            public override bool Connect()
            {
                IsConnected = true;
                return true;
            }

            public override bool Disconnect()
            {
                IsConnected = false;
                return true;
            }

            protected override IAxis CreateAxis(AxisParameter parameter)
            {
                return new TestAxis(parameter.Name, parameter.AxisNum);
            }

            protected override void CreateDefaultConfig()
            {
                _axes.Add(CreateAxis(new AxisParameter { Name = "Axis0", AxisNum = 0 }));
                _axes.Add(CreateAxis(new AxisParameter { Name = "Axis1", AxisNum = 1 }));
            }
        }

        [Fact]
        public void Initialize_WhenConfigMissing_ShouldUseDefaultAxes()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            var card = new TestAxisCardBaseImpl(0);

            try
            {
                Assert.True(card.Initialize(tempPath));
                Assert.True(File.Exists(tempPath));
                Assert.Equal(2, card.AxisCount);
                Assert.Equal("Axis1", card[1].Name);
                Assert.NotNull(card.GetAxisByName("Axis0"));
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    public class AxisParameterTests
    {
        [Fact]
        public void Clone_ShouldDeepCopyCalibrationData()
        {
            var p = new AxisParameter
            {
                Name = "X",
                AxisNum = 0,
                Step = 1000,
                CalibrationData = new CalibrationData
                {
                    Enable = true,
                    PointNum = 2,
                    StartPoint = 1,
                    Length = 10,
                    CalData = new System.Collections.Generic.List<double> { 1, 2 }
                }
            };

            var clone = p.Clone();
            Assert.NotSame(p, clone);
            Assert.NotSame(p.CalibrationData, clone.CalibrationData);

            clone.CalibrationData.CalData[0] = 999;
            Assert.Equal(1, p.CalibrationData.CalData[0]);
        }
    }

    public class AxisIoIntegrationExampleTests
    {
        public class DemoLayout
        {
            [IO(0)]
            public InputSignal DoorClosed { get; set; }

            [IO(1)]
            public InputSignal EmergencyStop { get; set; }

            [IO(0)]
            public OutputSignal Buzzer { get; set; }
        }

        [Fact]
        public async Task SafeMoveExample_ShouldBeTestable_WithInMemoryIo()
        {
            using var hw = new InMemoryHardwareIO(inputCount: 2, outputCount: 1);
            hw.Connect("mem");

            using var io = IoContextBuilder.For<DemoLayout>()
                .WithId("TestStation")
                .WithHardware(hw)
                .WithConfirmTimeout(TimeSpan.FromMilliseconds(200))
                .Build();

            var axis = new TestAxis("X", 0) { MoveDelayMs = 120 };

            hw.SetInputBit(0, true);
            hw.SetInputBit(1, false);
            io.Tick();

            async Task SafeMoveAsync(double pos)
            {
                if (!io.R.DoorClosed) throw new InvalidOperationException("Door is open");
                if (io.R.EmergencyStop) throw new InvalidOperationException("E-Stop pressed");
                if (axis.IsAlarm) throw new InvalidOperationException("Axis alarm");

                axis.AbsMove(pos);
                while (axis.IsBusy)
                {
                    io.Tick();
                    if (io.R.EmergencyStop)
                    {
                        axis.EmgStop();
                        throw new InvalidOperationException("E-Stop during move");
                    }
                    await Task.Delay(10);
                }
            }

            var moveTask = SafeMoveAsync(10);
            await moveTask;
            Assert.Equal(10, axis.Position, 3);

            axis.MoveDelayMs = 200;
            var moveTask2 = SafeMoveAsync(20);
            await Task.Delay(30);
            hw.SetInputBit(1, true);
            io.Tick();

            await Assert.ThrowsAsync<InvalidOperationException>(() => moveTask2);
            Assert.False(axis.IsBusy);
        }
    }
}
