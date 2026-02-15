using System;
using Engine.Core.Timing;
using Engine.ECS;
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
}
