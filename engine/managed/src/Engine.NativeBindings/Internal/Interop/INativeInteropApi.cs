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

    EngineNativeStatus RendererGetLastFrameStats(
        IntPtr renderer,
        out EngineNativeRendererFrameStats stats);

    EngineNativeStatus CaptureRequest(
        IntPtr renderer,
        in EngineNativeCaptureRequest request,
        out ulong requestId);

    EngineNativeStatus CapturePoll(
        ulong requestId,
        out EngineNativeCaptureResult result,
        out byte isReady);

    EngineNativeStatus CaptureFreeResult(ref EngineNativeCaptureResult result);

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

    EngineNativeStatus PhysicsSweep(
        IntPtr physics,
        in EngineNativeSweepQuery query,
        out EngineNativeSweepHit hit);

    EngineNativeStatus PhysicsOverlap(
        IntPtr physics,
        in EngineNativeOverlapQuery query,
        IntPtr hits,
        uint hitCapacity,
        out uint hitCount);
}
