using System;
using System.Threading;
using EwanIO.Core.Attributes;
using EwanIO.Core.Context;
using EwanIO.Core.Simulation;
using EwanIO.Hardware.InMemory;
using Xunit;

namespace EwanIO.Tests
{
    public class BypassAccessTestLayout
    {
        [IO(0)]
        public InputSignal Input0 { get; set; }

        [IO(0)]
        public OutputSignal Output0 { get; set; }
    }

    public class IoContextBypassAccessTests
    {
        [Fact]
        public void InputViews_ShouldReflect_Simulation_And_Mapping_Stages()
        {
            var hardware = new InMemoryHardwareIO(1, 1);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<BypassAccessTestLayout>()
                .WithHardware(hardware)
                .Build();

            // Input0 is NC: logical = value ^ true
            ctx.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);
            hardware.SetInputBit(0, false);

            // Init snapshot/edges
            ctx.Tick();

            // Force simulation ON and tick again
            ctx.Sim.ForcePhysicalOn(0);
            ctx.Tick();

            // hw=false, sim=true, logical=false (NC invert), noSim=true (hw + invert)
            Assert.False(ctx.GetHardwareInput(0));
            Assert.True(ctx.GetPreMapInput(0));
            Assert.True(ctx.GetNoSimInput(0));
            Assert.False(ctx.GetInput(0));

            Assert.False(ctx.Hw.Input0);
            Assert.True(ctx.PreMap.Input0);
            Assert.True(ctx.NoSim.Input0);
            Assert.False(ctx.R.Input0);

            // Edge sources are also separated
            Assert.True(ctx.EdgePreMap.R(0));
            Assert.True(ctx.Edge.F(0));
            Assert.False(ctx.EdgeNoSim.R(0));
            Assert.False(ctx.EdgeNoSim.F(0));
            Assert.False(ctx.EdgeHw.R(0));
            Assert.False(ctx.EdgeHw.F(0));

            ctx.Dispose();
        }

        [Fact]
        public void ForcePhysicalOff_WithNormallyClosed_ShouldInvertEdges_BetweenPreMapAndLogical()
        {
            var hardware = new InMemoryHardwareIO(1, 1);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<BypassAccessTestLayout>()
                .WithHardware(hardware)
                .Build();

            // NC mapping
            ctx.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);

            // Baseline: hw=true => PreMap=true, R=false
            hardware.SetInputBit(0, true);
            ctx.Tick();

            // Force physical/preMap OFF: PreMap=false, R=true
            ctx.Sim.ForcePhysicalOff(0);
            ctx.Tick();

            Assert.False(ctx.GetPreMapInput(0));
            Assert.True(ctx.GetInput(0));

            // Edge: PreMap falling, Logical rising
            Assert.True(ctx.EdgePreMap.F(0));
            Assert.True(ctx.Edge.R(0));

            ctx.Dispose();
        }

        [Fact]
        public void ForceOn_WithNormallyClosed_ShouldAffectLogicalOnly_AndNotChangePreMapEdges()
        {
            var hardware = new InMemoryHardwareIO(1, 1);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<BypassAccessTestLayout>()
                .WithHardware(hardware)
                .Build();

            // NC mapping: base logical = !PreMap
            ctx.Mapping.SetInputMapping(0, 0, isNormallyClosed: true);

            // Baseline: hw=true => PreMap=true, R=false
            hardware.SetInputBit(0, true);
            ctx.Tick();

            Assert.True(ctx.GetPreMapInput(0));
            Assert.False(ctx.GetInput(0));

            // Force logical ON: R=true, PreMap stays true
            ctx.Sim.ForceOn(0);
            ctx.Tick();

            Assert.True(ctx.GetPreMapInput(0));
            Assert.True(ctx.GetInput(0));

            // Edge: Logical rising; PreMap no edge
            Assert.True(ctx.Edge.R(0));
            Assert.False(ctx.EdgePreMap.R(0));
            Assert.False(ctx.EdgePreMap.F(0));

            // Simulation mode split
            Assert.Equal(SimMode.ForceOn, ctx.Sim.GetMode(0));
            Assert.Equal(SimMode.None, ctx.Sim.GetPhysicalMode(0));

            ctx.Dispose();
        }

        [Fact]
        public void ConfirmNoSim_ShouldIgnoreSimulation_WhenWaitingInput()
        {
            var hardware = new InMemoryHardwareIO(1, 1);
            hardware.Connect("test");
            hardware.SetInputBit(0, false);

            var ctx = IoContextBuilder.For<BypassAccessTestLayout>()
                .WithHardware(hardware)
                .Build();

            ctx.Tick(); // init

            // Simulate input ON (only affects ctx.GetInput / ctx.R)
            ctx.Sim.ForceOn(0);
            ctx.Tick();

            var simOp = ctx.Confirm(
                output: x => x.Output0,
                value: true,
                confirm: x => x.Input0,
                expected: true,
                timeout: TimeSpan.FromMilliseconds(50),
                now: true);

            Assert.True(simOp.IsCompleted);
            Assert.True(simOp.TryGetResult(out var simResult));
            Assert.True(simResult);

            var noSimOp = ctx.ConfirmNoSim(
                output: x => x.Output0,
                value: true,
                confirm: x => x.Input0,
                expected: true,
                timeout: TimeSpan.FromMilliseconds(30),
                now: true);

            var start = DateTime.UtcNow;
            while (!noSimOp.IsCompleted && (DateTime.UtcNow - start).TotalMilliseconds < 200)
            {
                ctx.Tick();
                Thread.Sleep(5);
            }

            Assert.True(noSimOp.IsCompleted);
            Assert.True(noSimOp.TryGetResult(out var noSimResult));
            Assert.False(noSimResult);

            ctx.Dispose();
        }

        [Fact]
        public void OutputPhysicalApis_ShouldProvidePhysicalReadAndWrite()
        {
            var hardware = new InMemoryHardwareIO(1, 1);
            hardware.Connect("test");

            var ctx = IoContextBuilder.For<BypassAccessTestLayout>()
                .WithHardware(hardware)
                .Build();

            // Output0 is NC: physical = logical ^ true
            ctx.Mapping.SetOutputMapping(0, 0, isNormallyClosed: true);

            ctx.Tick(); // init snapshot

            // Logical ON -> physical OFF
            ctx.On(x => x.Output0, now: true);
            ctx.Tick();
            Assert.True(ctx.GetOutput(0));
            Assert.False(ctx.GetPhysicalOutput(0));
            Assert.False(hardware.ReadOutBit(0));

            // Physical ON -> logical OFF (NC)
            ctx.OnPhysical(x => x.Output0, now: true);
            ctx.Tick();
            Assert.False(ctx.GetOutput(0));
            Assert.True(ctx.GetPhysicalOutput(0));
            Assert.True(hardware.ReadOutBit(0));

            // Physical OFF
            ctx.OffPhysical(0, now: true);
            ctx.Tick();
            Assert.False(ctx.GetPhysicalOutput(0));
            Assert.False(hardware.ReadOutBit(0));

            // Physical pulse (ON -> OFF), duration=10ms
            ctx.PulsePhysical(0, durationMs: 10, now: true, value: true);
            Assert.True(hardware.ReadOutBit(0));

            // 等待脉冲过期
            System.Threading.Thread.Sleep(50);

            ctx.Tick(); // pulse ends and output is set to !value
            Assert.False(hardware.ReadOutBit(0));

            ctx.Dispose();
        }
    }
}
