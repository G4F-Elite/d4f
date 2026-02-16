using System;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Physics;
using Engine.Rendering;

namespace Engine.NativeBindings.Internal;

internal interface INativePlatformApi
{
    bool PumpEvents();
}

internal interface INativeTimingApi
{
    FrameTiming NextFrameTiming();
}

internal interface INativePhysicsApi
{
    void SyncToPhysics(World world);

    void Step(TimeSpan deltaTime);

    void SyncFromPhysics(World world);

    bool Raycast(in PhysicsRaycastQuery query, out PhysicsRaycastHit hit);

    bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit);

    int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits);
}

internal interface INativeUiApi
{
    void Update(World world, in FrameTiming timing);
}

internal interface INativeRenderingApi
{
    FrameArena BeginFrame(int requestedBytes, int alignment);

    void Submit(RenderPacket packet);

    void Present();

    RenderingFrameStats GetLastFrameStats();
}
