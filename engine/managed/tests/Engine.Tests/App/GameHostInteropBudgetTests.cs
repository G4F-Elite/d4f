using System;
using System.Collections.Generic;
using Engine.App;
using Engine.Core.Timing;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.App;

public sealed class GameHostInteropBudgetTests
{
    [Fact]
    public void RunFrames_Throws_WhenRendererInteropBudgetIsExceeded()
    {
        var execution = new List<string>();
        World world = BuildMinimalWorld(execution);

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            interopBudgets: new InteropBudgetOptions(
                enforce: true,
                maxRendererCallsPerFrame: 2,
                maxPhysicsCallsPerTick: 3));
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        GameHost host = CreateHost(execution, world, timing, options);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.RunFrames(1));
        Assert.Contains("renderer calls", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunFrames_Throws_WhenPhysicsInteropBudgetPerTickIsExceeded()
    {
        var execution = new List<string>();
        World world = BuildMinimalWorld(execution);

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            interopBudgets: new InteropBudgetOptions(
                enforce: true,
                maxRendererCallsPerFrame: 3,
                maxPhysicsCallsPerTick: 2));
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        GameHost host = CreateHost(execution, world, timing, options);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.RunFrames(1));
        Assert.Contains("physics calls", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunFrames_AllowsMultiplePhysicsSubsteps_WhenPerTickBudgetIsRespected()
    {
        var execution = new List<string>();
        World world = BuildMinimalWorld(execution);

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(10),
            maxSubsteps: 8,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            interopBudgets: new InteropBudgetOptions(
                enforce: true,
                maxRendererCallsPerFrame: 3,
                maxPhysicsCallsPerTick: 2));
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40));
        GameHost host = CreateHost(execution, world, timing, options);

        int frames = host.RunFrames(1);
        FrameObservabilitySnapshot observability = host.LastFrameObservability;

        Assert.Equal(1, frames);
        Assert.Equal(4, observability.PhysicsSubsteps);
        Assert.Equal(6, observability.PhysicsInteropCallCount);
        Assert.Equal(3, observability.RendererInteropCallCount);
    }

    [Fact]
    public void RunFrames_DoesNotThrow_WhenInteropBudgetChecksAreDisabled()
    {
        var execution = new List<string>();
        World world = BuildMinimalWorld(execution);

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            interopBudgets: new InteropBudgetOptions(
                enforce: false,
                maxRendererCallsPerFrame: 1,
                maxPhysicsCallsPerTick: 1));
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        GameHost host = CreateHost(execution, world, timing, options);

        int frames = host.RunFrames(1);

        Assert.Equal(1, frames);
    }

    private static World BuildMinimalWorld(IList<string> execution)
    {
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));
        return world;
    }

    private static GameHost CreateHost(
        IList<string> execution,
        World world,
        FrameTiming timing,
        GameHostOptions options)
    {
        return GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true),
            new RecordingTimingFacade(execution, timing),
            new RecordingPhysicsFacade(execution),
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution),
            options);
    }
}
