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
    private const int DefaultFrameArenaBytes = 1 * 1024 * 1024;
    private const int DefaultFrameArenaAlignment = 64;

    private readonly World _world;
    private readonly IPlatformFacade _platformFacade;
    private readonly ITimingFacade _timingFacade;
    private readonly IPhysicsFacade _physicsFacade;
    private readonly IUiFacade _uiFacade;
    private readonly IRenderPacketBuilder _renderPacketBuilder;
    private readonly IRenderingFacade _renderingFacade;

    public GameHost(
        World world,
        IPlatformFacade platformFacade,
        ITimingFacade timingFacade,
        IPhysicsFacade physicsFacade,
        IUiFacade uiFacade,
        IRenderPacketBuilder renderPacketBuilder,
        IRenderingFacade renderingFacade)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _platformFacade = platformFacade ?? throw new ArgumentNullException(nameof(platformFacade));
        _timingFacade = timingFacade ?? throw new ArgumentNullException(nameof(timingFacade));
        _physicsFacade = physicsFacade ?? throw new ArgumentNullException(nameof(physicsFacade));
        _uiFacade = uiFacade ?? throw new ArgumentNullException(nameof(uiFacade));
        _renderPacketBuilder = renderPacketBuilder ?? throw new ArgumentNullException(nameof(renderPacketBuilder));
        _renderingFacade = renderingFacade ?? throw new ArgumentNullException(nameof(renderingFacade));
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

        _world.RunStage(SystemStage.PrePhysics, timing);

        _physicsFacade.SyncToPhysics(_world);
        _physicsFacade.Step(timing.DeltaTime);
        _physicsFacade.SyncFromPhysics(_world);

        _world.RunStage(SystemStage.PostPhysics, timing);

        _world.RunStage(SystemStage.UI, timing);
        _uiFacade.Update(_world, timing);

        using var frameArena = _renderingFacade.BeginFrame(DefaultFrameArenaBytes, DefaultFrameArenaAlignment);
        _world.RunStage(SystemStage.PreRender, timing);
        var renderPacket = _renderPacketBuilder.Build(_world, timing, frameArena);
        _renderingFacade.Submit(renderPacket);
        _renderingFacade.Present();
    }
}
