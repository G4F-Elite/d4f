using System;
using System.Collections.Generic;
using Engine.App;
using Engine.Core.Timing;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.App;

public sealed class GameHostDeterministicTests
{
    [Fact]
    public void RunFrames_UsesDeterministicDeltaOverride_WhenEnabled()
    {
        var execution = new List<string>();
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new RecordingWorldSystem("stage.prephysics", execution));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));

        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(30),
            maxSubsteps: 8,
            frameArenaBytes: 2048,
            frameArenaAlignment: 128,
            deterministicMode: new DeterministicModeOptions(
                enabled: true,
                seed: 777,
                fixedDeltaTimeOverride: TimeSpan.FromMilliseconds(10),
                disableAutoExposure: true,
                disableJitterEffects: true));
        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(3));
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

        int frames = host.RunFrames(1);
        FrameObservabilitySnapshot snapshot = host.LastFrameObservability;

        Assert.Equal(1, frames);
        Assert.Equal(1, physics.StepCallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(10), physics.StepDeltaTimes[0]);
        Assert.Equal(1, snapshot.PhysicsSubsteps);
    }
}
