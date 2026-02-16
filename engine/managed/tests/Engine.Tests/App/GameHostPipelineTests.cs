using System;
using System.Collections.Generic;
using Engine.App;
using Engine.Core.Timing;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.App;

public sealed class GameHostPipelineTests
{
    [Fact]
    public void RunFrames_ExecutesCanonicalPipelineOrder()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128);
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var physics = new RecordingPhysicsFacade(execution);
        var rendering = new RecordingRenderingFacade(execution);
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true),
            new RecordingTimingFacade(execution, timing),
            physics,
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            rendering,
            options);

        var frames = host.RunFrames(1);

        Assert.Equal(1, frames);
        Assert.Equal(
            [
                "platform",
                "timing",
                "stage.prephysics",
                "physics.sync.to",
                "physics.step",
                "physics.sync.from",
                "stage.postphysics",
                "stage.ui",
                "ui.facade",
                "render.begin_frame",
                "stage.prerender",
                "render.build",
                "render.submit",
                "render.present"
            ],
            execution);
        Assert.Equal([options.FixedDt], physics.StepDeltaTimes);
        Assert.Equal(options.FrameArenaBytes, rendering.LastRequestedBytes);
        Assert.Equal(options.FrameArenaAlignment, rendering.LastAlignment);
    }

    [Fact]
    public void RunFrames_SkipsPhysicsWhenNoSubstepsReady()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128);
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5));
        var physics = new RecordingPhysicsFacade(execution);
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true),
            new RecordingTimingFacade(execution, timing),
            physics,
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution),
            options);

        var frames = host.RunFrames(1);

        Assert.Equal(1, frames);
        Assert.Equal(0, physics.SyncToCallCount);
        Assert.Equal(0, physics.StepCallCount);
        Assert.Equal(0, physics.SyncFromCallCount);
        Assert.Equal(
            [
                "platform",
                "timing",
                "stage.prephysics",
                "stage.postphysics",
                "stage.ui",
                "ui.facade",
                "render.begin_frame",
                "stage.prerender",
                "render.build",
                "render.submit",
                "render.present"
            ],
            execution);
    }

    [Fact]
    public void RunFrames_ExecutesMultiplePhysicsSubstepsInSingleFrame()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(10),
            maxSubsteps: 8,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128);
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40));
        var physics = new RecordingPhysicsFacade(execution);
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true),
            new RecordingTimingFacade(execution, timing),
            physics,
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution),
            options);

        var frames = host.RunFrames(1);

        Assert.Equal(1, frames);
        Assert.Equal(1, physics.SyncToCallCount);
        Assert.Equal(4, physics.StepCallCount);
        Assert.Equal(1, physics.SyncFromCallCount);
        Assert.Equal([options.FixedDt, options.FixedDt, options.FixedDt, options.FixedDt], physics.StepDeltaTimes);
        Assert.Equal(
            [
                "platform",
                "timing",
                "stage.prephysics",
                "physics.sync.to",
                "physics.step",
                "physics.step",
                "physics.step",
                "physics.step",
                "physics.sync.from",
                "stage.postphysics",
                "stage.ui",
                "ui.facade",
                "render.begin_frame",
                "stage.prerender",
                "render.build",
                "render.submit",
                "render.present"
            ],
            execution);
    }

    [Fact]
    public void RunFrames_RespectsMaxSubstepsPerFrame()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(10),
            maxSubsteps: 3,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128);
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        var physics = new RecordingPhysicsFacade(execution);
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true),
            new RecordingTimingFacade(execution, timing),
            physics,
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution),
            options);

        var frames = host.RunFrames(1);

        Assert.Equal(1, frames);
        Assert.Equal(1, physics.SyncToCallCount);
        Assert.Equal(options.MaxSubsteps, physics.StepCallCount);
        Assert.Equal(1, physics.SyncFromCallCount);
        Assert.Equal([options.FixedDt, options.FixedDt, options.FixedDt], physics.StepDeltaTimes);
        Assert.Equal(
            [
                "platform",
                "timing",
                "stage.prephysics",
                "physics.sync.to",
                "physics.step",
                "physics.step",
                "physics.step",
                "physics.sync.from",
                "stage.postphysics",
                "stage.ui",
                "ui.facade",
                "render.begin_frame",
                "stage.prerender",
                "render.build",
                "render.submit",
                "render.present"
            ],
            execution);
    }

    [Fact]
    public void RunFrames_AccumulatesDeltaAcrossFrames()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(10),
            maxSubsteps: 8,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128);
        var firstFrame = new FrameTiming(0, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(5));
        var secondFrame = new FrameTiming(1, TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(10));
        var physics = new RecordingPhysicsFacade(execution);
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true, true),
            new RecordingTimingFacade(execution, firstFrame, secondFrame),
            physics,
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution),
            options);

        var frames = host.RunFrames(2);

        Assert.Equal(2, frames);
        Assert.Equal(1, physics.SyncToCallCount);
        Assert.Equal(1, physics.StepCallCount);
        Assert.Equal(1, physics.SyncFromCallCount);
        Assert.Equal([options.FixedDt], physics.StepDeltaTimes);
    }

    [Fact]
    public void RunFrames_ClampsAccumulatorToConfiguredMaximum()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(10),
            maxSubsteps: 3,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128,
            maxAccumulatedTime: TimeSpan.FromMilliseconds(30));
        var firstFrame = new FrameTiming(0, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        var secondFrame = new FrameTiming(1, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        var physics = new RecordingPhysicsFacade(execution);
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, true, true),
            new RecordingTimingFacade(execution, firstFrame, secondFrame),
            physics,
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution),
            options);

        var frames = host.RunFrames(2);

        Assert.Equal(2, frames);
        Assert.Equal(1, physics.SyncToCallCount);
        Assert.Equal(3, physics.StepCallCount);
        Assert.Equal(1, physics.SyncFromCallCount);
        Assert.Equal([options.FixedDt, options.FixedDt, options.FixedDt], physics.StepDeltaTimes);
    }

    [Fact]
    public void RunFrames_StopsWhenPlatformRequestsShutdown()
    {
        var execution = new List<string>();
        var world = new World();
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade(execution, false),
            new ThrowingTimingFacade(),
            new RecordingPhysicsFacade(execution),
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution));

        var frames = host.RunFrames(3);

        Assert.Equal(0, frames);
        Assert.Equal(["platform"], execution);
    }

    [Fact]
    public void RunFrames_RejectsNegativeFrameCount()
    {
        var world = new World();
        var host = GameHostFactory.CreateHost(
            world,
            new RecordingPlatformFacade([], false),
            new ThrowingTimingFacade(),
            new RecordingPhysicsFacade([]),
            new RecordingUiFacade([]),
            new RecordingPacketBuilder([]),
            new RecordingRenderingFacade([]));

        Assert.Throws<ArgumentOutOfRangeException>(() => host.RunFrames(-1));
    }
}
