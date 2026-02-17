using System;
using System.Diagnostics;
using System.Threading;
using Engine.Core.Abstractions;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Physics;
using Engine.Rendering;
using Engine.UI;

namespace Engine.App;

public sealed class GameHost
{
    private readonly World _world;
    private readonly IPlatformFacade _platformFacade;
    private readonly ITimingFacade _timingFacade;
    private readonly IPhysicsFacade _physicsFacade;
    private readonly IUiFacade _uiFacade;
    private readonly IRenderPacketBuilder _renderPacketBuilder;
    private readonly IRenderingFacade _renderingFacade;
    private readonly GameHostOptions _options;
    private TimeSpan _physicsAccumulator;

    public GameHost(
        World world,
        IPlatformFacade platformFacade,
        ITimingFacade timingFacade,
        IPhysicsFacade physicsFacade,
        IUiFacade uiFacade,
        IRenderPacketBuilder renderPacketBuilder,
        IRenderingFacade renderingFacade,
        GameHostOptions? options = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _platformFacade = platformFacade ?? throw new ArgumentNullException(nameof(platformFacade));
        _timingFacade = timingFacade ?? throw new ArgumentNullException(nameof(timingFacade));
        _physicsFacade = physicsFacade ?? throw new ArgumentNullException(nameof(physicsFacade));
        _uiFacade = uiFacade ?? throw new ArgumentNullException(nameof(uiFacade));
        _renderPacketBuilder = renderPacketBuilder ?? throw new ArgumentNullException(nameof(renderPacketBuilder));
        _renderingFacade = renderingFacade ?? throw new ArgumentNullException(nameof(renderingFacade));
        _options = options ?? GameHostOptions.Default;
    }

    public FrameObservabilitySnapshot LastFrameObservability { get; private set; } =
        FrameObservabilitySnapshot.Empty;

    public void Run(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_platformFacade.PumpEvents())
            {
                return;
            }

            ExecuteFrame();
        }
    }

    public int RunFrames(int frameCount, CancellationToken cancellationToken = default)
    {
        if (frameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count cannot be negative.");
        }

        var executedFrames = 0;

        while (executedFrames < frameCount && !cancellationToken.IsCancellationRequested)
        {
            if (!_platformFacade.PumpEvents())
            {
                break;
            }

            ExecuteFrame();
            executedFrames++;
        }

        return executedFrames;
    }

    private void ExecuteFrame()
    {
        var timing = _timingFacade.NextFrameTiming();
        if (timing.DeltaTime < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Timing facade returned a negative frame delta: {timing.DeltaTime}.");
        }

        timing = ResolveFrameTiming(timing);
        TimeSpan physicsFixedDt = ResolvePhysicsFixedDt();

        long frameStart = Stopwatch.GetTimestamp();

        long stageStart = Stopwatch.GetTimestamp();
        _world.RunStage(SystemStage.PrePhysics, timing);
        TimeSpan prePhysicsCpuTime = Stopwatch.GetElapsedTime(stageStart);

        _physicsAccumulator += timing.DeltaTime;
        if (_physicsAccumulator > _options.MaxAccumulatedTime)
        {
            _physicsAccumulator = _options.MaxAccumulatedTime;
        }

        var substeps = 0;

        stageStart = Stopwatch.GetTimestamp();
        while (_physicsAccumulator >= physicsFixedDt && substeps < _options.MaxSubsteps)
        {
            if (substeps == 0)
            {
                _physicsFacade.SyncToPhysics(_world);
            }

            _physicsFacade.Step(physicsFixedDt);
            _physicsAccumulator -= physicsFixedDt;
            substeps++;
        }

        if (substeps > 0)
        {
            _physicsFacade.SyncFromPhysics(_world);
        }
        TimeSpan physicsCpuTime = Stopwatch.GetElapsedTime(stageStart);

        stageStart = Stopwatch.GetTimestamp();
        _world.RunStage(SystemStage.PostPhysics, timing);
        TimeSpan postPhysicsCpuTime = Stopwatch.GetElapsedTime(stageStart);

        stageStart = Stopwatch.GetTimestamp();
        _world.RunStage(SystemStage.UI, timing);
        _uiFacade.Update(_world, timing);
        TimeSpan uiCpuTime = Stopwatch.GetElapsedTime(stageStart);

        stageStart = Stopwatch.GetTimestamp();
        using var frameArena = _renderingFacade.BeginFrame(_options.FrameArenaBytes, _options.FrameArenaAlignment);
        _world.RunStage(SystemStage.PreRender, timing);
        var renderPacket = _renderPacketBuilder.Build(_world, timing, frameArena, _options.RenderSettings);
        TimeSpan preRenderCpuTime = Stopwatch.GetElapsedTime(stageStart);

        stageStart = Stopwatch.GetTimestamp();
        _renderingFacade.Submit(renderPacket);
        _renderingFacade.Present();
        RenderingFrameStats renderingStats = _renderingFacade.GetLastFrameStats();
        TimeSpan renderCpuTime = Stopwatch.GetElapsedTime(stageStart);

        TimeSpan totalCpuTime = Stopwatch.GetElapsedTime(frameStart);
        LastFrameObservability = new FrameObservabilitySnapshot(
            timing.FrameNumber,
            prePhysicsCpuTime,
            physicsCpuTime,
            postPhysicsCpuTime,
            uiCpuTime,
            preRenderCpuTime,
            renderCpuTime,
            totalCpuTime,
            substeps,
            renderingStats);
    }

    private FrameTiming ResolveFrameTiming(in FrameTiming sourceTiming)
    {
        DeterministicModeOptions deterministic = _options.DeterministicMode;
        if (!deterministic.Enabled)
        {
            return sourceTiming;
        }

        TimeSpan resolvedDelta = deterministic.FixedDeltaTimeOverride ?? _options.FixedDt;
        TimeSpan resolvedTotal = TimeSpan.FromTicks(
            checked((sourceTiming.FrameNumber + 1) * resolvedDelta.Ticks));
        return new FrameTiming(sourceTiming.FrameNumber, resolvedDelta, resolvedTotal);
    }

    private TimeSpan ResolvePhysicsFixedDt()
    {
        DeterministicModeOptions deterministic = _options.DeterministicMode;
        if (deterministic.Enabled && deterministic.FixedDeltaTimeOverride.HasValue)
        {
            return deterministic.FixedDeltaTimeOverride.Value;
        }

        return _options.FixedDt;
    }
}
