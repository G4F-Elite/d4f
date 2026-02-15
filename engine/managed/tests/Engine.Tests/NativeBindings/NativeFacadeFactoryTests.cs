using System;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryTests
{
    [Fact]
    public void FactoryCreatesUsableFacadeSet()
    {
        var platform = NativeFacadeFactory.CreatePlatformFacade();
        var timing = NativeFacadeFactory.CreateTimingFacade();
        var physics = NativeFacadeFactory.CreatePhysicsFacade();
        var ui = NativeFacadeFactory.CreateUiFacade();
        var rendering = NativeFacadeFactory.CreateRenderingFacade();

        Assert.True(platform.PumpEvents());

        var frame0 = timing.NextFrameTiming();
        var frame1 = timing.NextFrameTiming();

        Assert.Equal(0, frame0.FrameNumber);
        Assert.Equal(1, frame1.FrameNumber);
        Assert.True(frame1.TotalTime >= frame0.TotalTime);

        var world = new World();
        physics.SyncToPhysics(world);
        physics.Step(TimeSpan.FromMilliseconds(8));
        physics.SyncFromPhysics(world);

        ui.Update(world, frame1);
        rendering.Submit(RenderPacket.Empty(frame1.FrameNumber));
        rendering.Present();
    }

    [Fact]
    public void PhysicsFacadeRejectsNegativeDeltaTime()
    {
        var physics = NativeFacadeFactory.CreatePhysicsFacade();
        Assert.Throws<ArgumentOutOfRangeException>(() => physics.Step(TimeSpan.FromMilliseconds(-1)));
    }
}
