using System;
using System.Diagnostics;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Physics;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal sealed class NativePlatformApiStub : INativePlatformApi
{
    public bool PumpEvents() => true;
}

internal sealed class NativeTimingApiStub : INativeTimingApi
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _previous = TimeSpan.Zero;
    private long _frameNumber;

    public FrameTiming NextFrameTiming()
    {
        var current = _stopwatch.Elapsed;
        var delta = current - _previous;
        _previous = current;
        var timing = new FrameTiming(_frameNumber, delta, current);
        _frameNumber++;
        return timing;
    }
}

internal sealed class NativePhysicsApiStub : INativePhysicsApi
{
    public void SyncToPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
    }

    public void Step(TimeSpan deltaTime)
    {
        if (deltaTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }
    }

    public void SyncFromPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
    }

    public bool Raycast(in PhysicsRaycastQuery query, out PhysicsRaycastHit hit)
    {
        hit = default;
        return false;
    }
}

internal sealed class NativeUiApiStub : INativeUiApi
{
    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);
    }
}

internal sealed class NativeRenderingApiStub : INativeRenderingApi
{
    public FrameArena BeginFrame(int requestedBytes, int alignment)
    {
        return new FrameArena(requestedBytes, alignment);
    }

    public void Submit(RenderPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
    }

    public void Present()
    {
    }
}
