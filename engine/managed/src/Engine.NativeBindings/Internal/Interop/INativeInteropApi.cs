using System;

namespace Engine.NativeBindings.Internal.Interop;

internal interface INativeInteropApi
{
    EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine);

    EngineNativeStatus EngineDestroy(IntPtr engine);

    EngineNativeStatus EnginePumpEvents(
        IntPtr engine,
        out EngineNativeInputSnapshot input,
        out EngineNativeWindowEvents windowEvents);

    EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer);

    EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics);

    EngineNativeStatus RendererBeginFrame(
        IntPtr renderer,
        nuint requestedBytes,
        nuint alignment,
        out IntPtr frameMemory);

    EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet);

    EngineNativeStatus RendererPresent(IntPtr renderer);

    EngineNativeStatus PhysicsStep(IntPtr physics, double deltaSeconds);

    EngineNativeStatus PhysicsSyncFromWorld(IntPtr physics, IntPtr writes, uint writeCount);

    EngineNativeStatus PhysicsSyncToWorld(
        IntPtr physics,
        IntPtr reads,
        uint readCapacity,
        out uint readCount);

    EngineNativeStatus PhysicsRaycast(
        IntPtr physics,
        in EngineNativeRaycastQuery query,
        out EngineNativeRaycastHit hit);
}
