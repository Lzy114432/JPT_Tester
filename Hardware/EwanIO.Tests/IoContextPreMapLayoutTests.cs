using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    public class PreMapLayoutTestLayout
    {
        [IO(0)]
        public InputSignal Input0 { get; set; }

        [IO(1)]
        public InputSignal Input1 { get; set; }
    }

    public class IoContextPreMapLayoutTests
    {
        [Fact]
        public void PreMap_ShouldBypassInputNormallyClosedInversion_AndKeepPhysicalMapping()
        {
            var hardware = new InMemoryHardwareIO(2, 0);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<PreMapLayoutTestLayout>()
                .WithHardware(hardware)
                .Build();

            // logical0 -> physical1 (NC)
            // logical1 -> physical0 (NO)  (avoid duplicate physical mapping)
            ctx.Mapping.SetInputMapping(0, 1, isNormallyClosed: true);
            ctx.Mapping.SetInputMapping(1, 0, isNormallyClosed: false);

            hardware.SetInputBit(1, true);
            hardware.SetInputBit(0, false);

            ctx.Tick();

            // PreMap: simulate applied (none here), bypass NO/NC inversion
            Assert.True(ctx.GetPreMapInput(0));
            Assert.True(ctx.PreMap.Input0);

            // Logical: apply NO/NC inversion (NC => invert)
            Assert.False(ctx.GetInput(0));
            Assert.False(ctx.R.Input0);

            // Logical1: physical0 is false, NO => keep false
            Assert.False(ctx.GetPreMapInput(1));
            Assert.False(ctx.GetInput(1));
            Assert.False(ctx.PreMap.Input1);
            Assert.False(ctx.R.Input1);

            ctx.Dispose();
        }

        [Fact]
        public void PreMap_ShouldIncludeSimulation_ButStillBypassInputNormallyClosedInversion()
        {
            var hardware = new InMemoryHardwareIO(1, 0);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<PreMapLayoutTestLayout>()
                .WithHardware(hardware)
                .Build();

            // NC inversion: logical value should be inverted from simValue.
            ctx.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);

            hardware.SetInputBit(0, false);

            ctx.Tick();
            ctx.Sim.ForcePhysicalOn(0);
            ctx.Tick();

            // Simulation forces simValue = true
            Assert.True(ctx.GetPreMapInput(0));
            Assert.True(ctx.PreMap.Input0);

            // NC inversion applies after simulation: true -> false
            Assert.False(ctx.GetInput(0));
            Assert.False(ctx.R.Input0);

            ctx.Dispose();
        }

        [Fact]
        public void PreMap_ShouldNotMutateCapturedInstance_AcrossTick()
        {
            var hardware = new InMemoryHardwareIO(2, 0);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<PreMapLayoutTestLayout>()
                .WithHardware(hardware)
                .Build();

            hardware.SetInputBit(0, false);
            hardware.SetInputBit(1, false);

            ctx.Tick();
            var snapshot = ctx.PreMap;

            Assert.False(snapshot.Input0);
            Assert.False(snapshot.Input1);

            hardware.SetInputBit(0, true);
            hardware.SetInputBit(1, true);
            ctx.Tick();

            Assert.False(snapshot.Input0);
            Assert.False(snapshot.Input1);
            Assert.True(ctx.PreMap.Input0);
            Assert.True(ctx.PreMap.Input1);

            ctx.Dispose();
        }
    }
}
