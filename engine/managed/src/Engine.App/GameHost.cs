using System;
using System.Threading;
using Engine.Core.Abstractions;
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

        _world.RunStage(SystemStage.PrePhysics, timing);

        _physicsAccumulator += timing.DeltaTime;
        if (_physicsAccumulator > _options.MaxAccumulatedTime)
        {
            _physicsAccumulator = _options.MaxAccumulatedTime;
        }

        var substeps = 0;

        while (_physicsAccumulator >= _options.FixedDt && substeps < _options.MaxSubsteps)
        {
            if (substeps == 0)
            {
                _physicsFacade.SyncToPhysics(_world);
            }

            _physicsFacade.Step(_options.FixedDt);
            _physicsAccumulator -= _options.FixedDt;
            substeps++;
        }

        if (substeps > 0)
        {
            _physicsFacade.SyncFromPhysics(_world);
        }

        _world.RunStage(SystemStage.PostPhysics, timing);

        _world.RunStage(SystemStage.UI, timing);
        _uiFacade.Update(_world, timing);

        using var frameArena = _renderingFacade.BeginFrame(_options.FrameArenaBytes, _options.FrameArenaAlignment);
        _world.RunStage(SystemStage.PreRender, timing);
        var renderPacket = _renderPacketBuilder.Build(_world, timing, frameArena);
        _renderingFacade.Submit(renderPacket);
        _renderingFacade.Present();
    }
}
