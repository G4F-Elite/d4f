using System;
using System.Collections.Generic;
using Engine.App;
using Engine.Core.Abstractions;
using Engine.Core.Handles;
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

    public bool Raycast(in PhysicsRaycastQuery query, out PhysicsRaycastHit hit)
    {
        hit = default;
        _execution.Add("physics.raycast");
        return false;
    }

    public bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit)
    {
        hit = default;
        _execution.Add("physics.sweep");
        return false;
    }

    public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
    {
        _execution.Add("physics.overlap");
        return 0;
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

    public RenderSettings LastRenderSettings { get; private set; } = RenderSettings.Default;

    public RenderPacket Build(World world, in FrameTiming timing, FrameArena frameArena, in RenderSettings renderSettings)
    {
        Assert.NotNull(frameArena);
        LastRenderSettings = renderSettings;
        _execution.Add("render.build");
        return RenderPacket.Empty(timing.FrameNumber);
    }
}

internal sealed class RecordingRenderingFacade : IRenderingFacade
{
    private readonly IList<string> _execution;
    private ulong _nextResourceHandle = 1u;

    public RecordingRenderingFacade(IList<string> execution)
    {
        _execution = execution;
    }

    public int LastRequestedBytes { get; private set; }

    public int LastAlignment { get; private set; }

    public RenderingFrameStats LastFrameStats { get; set; } = RenderingFrameStats.Empty;

    public int GetLastFrameStatsCallCount { get; private set; }

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

    public RenderingFrameStats GetLastFrameStats()
    {
        GetLastFrameStatsCallCount++;
        return LastFrameStats;
    }

    public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
    {
        if (blob.Length == 0)
        {
            throw new ArgumentException("Blob payload must be non-empty.", nameof(blob));
        }

        return new MeshHandle(_nextResourceHandle++);
    }

    public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
    {
        if (blob.Length == 0)
        {
            throw new ArgumentException("Blob payload must be non-empty.", nameof(blob));
        }

        return new TextureHandle(_nextResourceHandle++);
    }

    public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
    {
        if (blob.Length == 0)
        {
            throw new ArgumentException("Blob payload must be non-empty.", nameof(blob));
        }

        return new MaterialHandle(_nextResourceHandle++);
    }

    public void DestroyResource(ulong handle)
    {
        if (handle == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle must be non-zero.");
        }
    }

    public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
    {
        if (width == 0u || height == 0u)
        {
            throw new ArgumentOutOfRangeException(width == 0u ? nameof(width) : nameof(height));
        }

        return new byte[checked((int)width * (int)height * 4)];
    }
}
