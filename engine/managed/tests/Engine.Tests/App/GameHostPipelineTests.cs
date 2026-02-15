using System;
using System.Collections.Generic;
using Engine.App;
using Engine.Core.Abstractions;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Physics;
using Engine.Rendering;
using Engine.UI;
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

        var timing = new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
        var host = new GameHost(
            world,
            new RecordingPlatformFacade(execution, true),
            new RecordingTimingFacade(execution, timing),
            new RecordingPhysicsFacade(execution),
            new RecordingUiFacade(execution),
            new RecordingPacketBuilder(execution),
            new RecordingRenderingFacade(execution));

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
    }

    [Fact]
    public void RunFrames_StopsWhenPlatformRequestsShutdown()
    {
        var execution = new List<string>();
        var world = new World();
        var host = new GameHost(
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
        var host = new GameHost(
            world,
            new RecordingPlatformFacade([], false),
            new ThrowingTimingFacade(),
            new RecordingPhysicsFacade([]),
            new RecordingUiFacade([]),
            new RecordingPacketBuilder([]),
            new RecordingRenderingFacade([]));

        Assert.Throws<ArgumentOutOfRangeException>(() => host.RunFrames(-1));
    }

    private sealed class RecordingWorldSystem : IWorldSystem
    {
        private readonly string _name;
        private readonly IList<string> _execution;

        public RecordingWorldSystem(string name, IList<string> execution)
        {
            _name = name;
            _execution = execution;
        }

        public void Update(World world, in FrameTiming timing)
        {
            _execution.Add(_name);
        }
    }

    private sealed class RecordingPlatformFacade : IPlatformFacade
    {
        private readonly IList<string> _execution;
        private readonly Queue<bool> _responses;

        public RecordingPlatformFacade(IList<string> execution, params bool[] responses)
        {
            _execution = execution;
            _responses = new Queue<bool>(responses);
        }

        public bool PumpEvents()
        {
            _execution.Add("platform");

            if (_responses.Count == 0)
            {
                return false;
            }

            return _responses.Dequeue();
        }
    }

    private sealed class RecordingTimingFacade : ITimingFacade
    {
        private readonly IList<string> _execution;
        private readonly FrameTiming _timing;

        public RecordingTimingFacade(IList<string> execution, FrameTiming timing)
        {
            _execution = execution;
            _timing = timing;
        }

        public FrameTiming NextFrameTiming()
        {
            _execution.Add("timing");
            return _timing;
        }
    }

    private sealed class ThrowingTimingFacade : ITimingFacade
    {
        public FrameTiming NextFrameTiming()
        {
            throw new InvalidOperationException("Timing should not be called.");
        }
    }

    private sealed class RecordingPhysicsFacade : IPhysicsFacade
    {
        private readonly IList<string> _execution;

        public RecordingPhysicsFacade(IList<string> execution)
        {
            _execution = execution;
        }

        public void SyncToPhysics(World world)
        {
            _execution.Add("physics.sync.to");
        }

        public void Step(TimeSpan deltaTime)
        {
            _execution.Add("physics.step");
        }

        public void SyncFromPhysics(World world)
        {
            _execution.Add("physics.sync.from");
        }
    }

    private sealed class RecordingUiFacade : IUiFacade
    {
        private readonly IList<string> _execution;

        public RecordingUiFacade(IList<string> execution)
        {
            _execution = execution;
        }

        public void Update(World world, in FrameTiming timing)
        {
            _execution.Add("ui.facade");
        }
    }

    private sealed class RecordingPacketBuilder : IRenderPacketBuilder
    {
        private readonly IList<string> _execution;

        public RecordingPacketBuilder(IList<string> execution)
        {
            _execution = execution;
        }

        public RenderPacket Build(World world, in FrameTiming timing, FrameArena frameArena)
        {
            Assert.NotNull(frameArena);
            _execution.Add("render.build");
            return RenderPacket.Empty(timing.FrameNumber);
        }
    }

    private sealed class RecordingRenderingFacade : IRenderingFacade
    {
        private readonly IList<string> _execution;

        public RecordingRenderingFacade(IList<string> execution)
        {
            _execution = execution;
        }

        public FrameArena BeginFrame(int requestedBytes, int alignment)
        {
            _execution.Add("render.begin_frame");
            return new FrameArena(requestedBytes, alignment);
        }

        public void Submit(RenderPacket packet)
        {
            _execution.Add("render.submit");
        }

        public void Present()
        {
            _execution.Add("render.present");
        }
    }
}
