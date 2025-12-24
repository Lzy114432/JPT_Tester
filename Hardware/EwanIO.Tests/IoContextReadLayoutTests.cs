using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    public class ReadLayoutTestLayout
    {
        [IO(0)]
        public InputSignal InputA { get; set; }

        [IO(1)]
        public InputSignal InputB { get; set; }
    }

    public class IoContextReadLayoutTests
    {
        [Fact]
        public void R_ShouldNotMutateCapturedInstance_AcrossTick()
        {
            var hardware = new InMemoryHardwareIO(2, 2);
            hardware.SetInputBit(0, false);
            hardware.SetInputBit(1, false);

            var context = IoContextBuilder.For<ReadLayoutTestLayout>()
                .WithHardware(hardware)
                .BuildAndConnect("test");

            context.Tick();
            var snapshot = context.R;

            Assert.False(snapshot.InputA);
            Assert.False(snapshot.InputB);

            hardware.SetInputBit(0, true);
            hardware.SetInputBit(1, true);
            context.Tick();

            Assert.False(snapshot.InputA);
            Assert.False(snapshot.InputB);
            Assert.True(context.R.InputA);
            Assert.True(context.R.InputB);

            context.Dispose();
        }
    }
}
