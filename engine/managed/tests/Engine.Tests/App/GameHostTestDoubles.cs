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

internal static class GameHostFactory
{
    public static GameHost CreateHost(
        World world,
        IPlatformFacade platformFacade,
        ITimingFacade timingFacade,
        IPhysicsFacade physicsFacade,
        IUiFacade uiFacade,
        IRenderPacketBuilder renderPacketBuilder,
        IRenderingFacade renderingFacade,
        GameHostOptions? options = null)
    {
        return new GameHost(
            world,
            platformFacade,
            timingFacade,
            physicsFacade,
            uiFacade,
            renderPacketBuilder,
            renderingFacade,
            options);
    }
}

internal sealed class RecordingWorldSystem : IWorldSystem
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

internal sealed class RecordingPlatformFacade : IPlatformFacade
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

internal sealed class RecordingTimingFacade : ITimingFacade
{
    private readonly IList<string> _execution;
    private readonly Queue<FrameTiming> _timings;

    public RecordingTimingFacade(IList<string> execution, params FrameTiming[] timings)
    {
        _execution = execution;

        if (timings.Length == 0)
        {
            throw new ArgumentException("At least one timing must be provided.", nameof(timings));
        }

        _timings = new Queue<FrameTiming>(timings);
    }

    public FrameTiming NextFrameTiming()
    {
        _execution.Add("timing");

        if (_timings.Count == 0)
        {
            throw new InvalidOperationException("No frame timings configured.");
        }

        return _timings.Dequeue();
    }
}

internal sealed class ThrowingTimingFacade : ITimingFacade
{
    public FrameTiming NextFrameTiming()
    {
        throw new InvalidOperationException("Timing should not be called.");
    }
}

internal sealed class RecordingPhysicsFacade : IPhysicsFacade
{
    private readonly IList<string> _execution;
    private readonly List<TimeSpan> _stepDeltaTimes = [];

    public RecordingPhysicsFacade(IList<string> execution)
    {
        _execution = execution;
    }

    public int SyncToCallCount { get; private set; }

    public int StepCallCount => _stepDeltaTimes.Count;

    public int SyncFromCallCount { get; private set; }

    public IReadOnlyList<TimeSpan> StepDeltaTimes => _stepDeltaTimes;

    public void SyncToPhysics(World world)
    {
        SyncToCallCount++;
        _execution.Add("physics.sync.to");
    }

    public void Step(TimeSpan deltaTime)
    {
        _stepDeltaTimes.Add(deltaTime);
        _execution.Add("physics.step");
    }

    public void SyncFromPhysics(World world)
    {
        SyncFromCallCount++;
        _execution.Add("physics.sync.from");
    }
}

internal sealed class RecordingUiFacade : IUiFacade
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

internal sealed class RecordingPacketBuilder : IRenderPacketBuilder
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

internal sealed class RecordingRenderingFacade : IRenderingFacade
{
    private readonly IList<string> _execution;

    public RecordingRenderingFacade(IList<string> execution)
    {
        _execution = execution;
    }

    public int LastRequestedBytes { get; private set; }

    public int LastAlignment { get; private set; }

    public FrameArena BeginFrame(int requestedBytes, int alignment)
    {
        LastRequestedBytes = requestedBytes;
        LastAlignment = alignment;
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
