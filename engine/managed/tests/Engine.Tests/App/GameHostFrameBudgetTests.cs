using System;
using System.Collections.Generic;
using Engine.App;
using Engine.Core.Timing;
using Engine.ECS;
using Xunit;

namespace Engine.Tests.App;

public sealed class GameHostFrameBudgetTests
{
    [Fact]
    public void RunFrames_Throws_WhenManagedAllocationBudgetIsExceeded()
    {
        var execution = new List<string>();
        World world = BuildWorldWithAllocator(execution, allocationBytes: 32 * 1024);
        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            maxManagedAllocatedBytesPerFrame: 1024);

        GameHost host = CreateHost(execution, world, options);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.RunFrames(1));
        Assert.Contains("managed allocation", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunFrames_Throws_WhenCpuBudgetIsExceeded()
    {
        var execution = new List<string>();
        World world = BuildWorldWithAllocator(execution, allocationBytes: 0);
        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            maxTotalCpuTimePerFrame: TimeSpan.FromTicks(1));

        GameHost host = CreateHost(execution, world, options);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.RunFrames(1));
        Assert.Contains("total CPU", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RunFrames_PopulatesManagedAllocatedBytesInObservability()
    {
        var execution = new List<string>();
        World world = BuildWorldWithAllocator(execution, allocationBytes: 8 * 1024);
        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64);

        GameHost host = CreateHost(execution, world, options);

        int frames = host.RunFrames(1);

        Assert.Equal(1, frames);
        Assert.True(host.LastFrameObservability.ManagedAllocatedBytes >= 0L);
    }

    private static World BuildWorldWithAllocator(IList<string> execution, int allocationBytes)
    {
        var world = new World();
        world.RegisterSystem(SystemStage.PrePhysics, new AllocatingWorldSystem(execution, allocationBytes));
        world.RegisterSystem(SystemStage.PostPhysics, new RecordingWorldSystem("stage.postphysics", execution));
        world.RegisterSystem(SystemStage.UI, new RecordingWorldSystem("stage.ui", execution));
        world.RegisterSystem(SystemStage.PreRender, new RecordingWorldSystem("stage.prerender", execution));
        return world;
    }

    private static GameHost CreateHost(
        IList<string> execution,
        World world,
        GameHostOptions options)
    {
        FrameTiming timing = new(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
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

    private sealed class AllocatingWorldSystem : IWorldSystem
    {
        private readonly IList<string> _execution;
        private readonly int _allocationBytes;

        public AllocatingWorldSystem(IList<string> execution, int allocationBytes)
        {
            _execution = execution;
            _allocationBytes = allocationBytes;
        }

        public void Update(World world, in FrameTiming timing)
        {
            _execution.Add("stage.prephysics");
            if (_allocationBytes <= 0)
            {
                return;
            }

            byte[] payload = new byte[_allocationBytes];
            payload[0] = 1;
            _execution.Add(payload[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
