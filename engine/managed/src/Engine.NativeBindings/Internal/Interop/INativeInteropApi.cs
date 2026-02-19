using System;

namespace Engine.NativeBindings.Internal.Interop;

internal interface INativeInteropApi
{
    uint EngineGetNativeApiVersion();

    EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine);

    EngineNativeStatus EngineDestroy(IntPtr engine);

    EngineNativeStatus EnginePumpEvents(
        IntPtr engine,
        out EngineNativeInputSnapshot input,
        out EngineNativeWindowEvents windowEvents);

    EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer);

    EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics);

    EngineNativeStatus EngineGetAudio(IntPtr engine, out IntPtr audio);

    EngineNativeStatus EngineGetNet(IntPtr engine, out IntPtr net);

    EngineNativeStatus ContentMountPak(IntPtr engine, string pakPath);

    EngineNativeStatus ContentMountDirectory(IntPtr engine, string directoryPath);

    EngineNativeStatus ContentReadFile(
        IntPtr engine,
        string assetPath,
        IntPtr buffer,
        nuint bufferSize,
        out nuint outSize);

    EngineNativeStatus RendererBeginFrame(
        IntPtr renderer,
        nuint requestedBytes,
        nuint alignment,
        out IntPtr frameMemory);

    EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet);

    EngineNativeStatus RendererPresent(IntPtr renderer);

    EngineNativeStatus RendererPresentWithStats(
        IntPtr renderer,
        out EngineNativeRendererFrameStats stats);

    EngineNativeStatus RendererCreateMeshFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong mesh);

    EngineNativeStatus RendererCreateMeshFromCpu(
        IntPtr renderer,
        in EngineNativeMeshCpuData meshData,
        out ulong mesh);

    EngineNativeStatus RendererCreateTextureFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong texture);

    EngineNativeStatus RendererCreateTextureFromCpu(
        IntPtr renderer,
        in EngineNativeTextureCpuData textureData,
        out ulong texture);

    EngineNativeStatus RendererCreateMaterialFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong material);

    EngineNativeStatus RendererDestroyResource(IntPtr renderer, ulong handle);

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

    EngineNativeStatus AudioCreateSoundFromBlob(
        IntPtr audio,
        IntPtr data,
        nuint size,
        out ulong sound);

    EngineNativeStatus AudioPlay(
        IntPtr audio,
        ulong sound,
        in EngineNativeAudioPlayDesc playDesc,
        out ulong emitterId);

    EngineNativeStatus AudioSetListener(IntPtr audio, in EngineNativeListenerDesc listenerDesc);

    EngineNativeStatus AudioSetEmitterParams(
        IntPtr audio,
        ulong emitterId,
        in EngineNativeEmitterParams emitterParams);

    EngineNativeStatus AudioSetBusParams(IntPtr audio, in EngineNativeAudioBusParams busParams);

    EngineNativeStatus NetCreate(in EngineNativeNetDesc desc, out IntPtr net);

    EngineNativeStatus NetDestroy(IntPtr net);

    EngineNativeStatus NetPump(IntPtr net, out EngineNativeNetEvents events);

    EngineNativeStatus NetSend(IntPtr net, in EngineNativeNetSendDesc sendDesc);

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
