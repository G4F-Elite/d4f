using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine.NativeBindings.Internal.Interop;

internal sealed partial class DffNativeInteropApi : INativeInteropApi
{
    public static DffNativeInteropApi Instance { get; } = new();

    private DffNativeInteropApi()
    {
    }

    public uint EngineGetNativeApiVersion()
        => NativeMethods.EngineGetNativeApiVersion();

    public EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine)
        => NativeMethods.EngineCreate(in createDesc, out engine);

    public EngineNativeStatus EngineDestroy(IntPtr engine)
        => NativeMethods.EngineDestroy(engine);

    public EngineNativeStatus EnginePumpEvents(
        IntPtr engine,
        out EngineNativeInputSnapshot input,
        out EngineNativeWindowEvents windowEvents)
        => NativeMethods.EnginePumpEvents(engine, out input, out windowEvents);

    public EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer)
        => NativeMethods.EngineGetRenderer(engine, out renderer);

    public EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics)
        => NativeMethods.EngineGetPhysics(engine, out physics);

    public EngineNativeStatus EngineGetAudio(IntPtr engine, out IntPtr audio)
        => NativeMethods.EngineGetAudio(engine, out audio);

    public EngineNativeStatus RendererBeginFrame(
        IntPtr renderer,
        nuint requestedBytes,
        nuint alignment,
        out IntPtr frameMemory)
        => NativeMethods.RendererBeginFrame(renderer, requestedBytes, alignment, out frameMemory);

    public EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet)
        => NativeMethods.RendererSubmit(renderer, in packet);

    public EngineNativeStatus RendererPresent(IntPtr renderer)
        => NativeMethods.RendererPresent(renderer);

    public EngineNativeStatus RendererCreateMeshFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong mesh)
        => NativeMethods.RendererCreateMeshFromBlob(renderer, data, size, out mesh);

    public EngineNativeStatus RendererCreateMeshFromCpu(
        IntPtr renderer,
        in EngineNativeMeshCpuData meshData,
        out ulong mesh)
        => NativeMethods.RendererCreateMeshFromCpu(renderer, in meshData, out mesh);

    public EngineNativeStatus RendererCreateTextureFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong texture)
        => NativeMethods.RendererCreateTextureFromBlob(renderer, data, size, out texture);

    public EngineNativeStatus RendererCreateTextureFromCpu(
        IntPtr renderer,
        in EngineNativeTextureCpuData textureData,
        out ulong texture)
        => NativeMethods.RendererCreateTextureFromCpu(renderer, in textureData, out texture);

    public EngineNativeStatus RendererCreateMaterialFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong material)
        => NativeMethods.RendererCreateMaterialFromBlob(renderer, data, size, out material);

    public EngineNativeStatus RendererDestroyResource(IntPtr renderer, ulong handle)
        => NativeMethods.RendererDestroyResource(renderer, handle);

    public EngineNativeStatus RendererGetLastFrameStats(
        IntPtr renderer,
        out EngineNativeRendererFrameStats stats)
        => NativeMethods.RendererGetLastFrameStats(renderer, out stats);

    public EngineNativeStatus CaptureRequest(
        IntPtr renderer,
        in EngineNativeCaptureRequest request,
        out ulong requestId)
        => NativeMethods.CaptureRequest(renderer, in request, out requestId);

    public EngineNativeStatus CapturePoll(
        ulong requestId,
        out EngineNativeCaptureResult result,
        out byte isReady)
        => NativeMethods.CapturePoll(requestId, out result, out isReady);

    public EngineNativeStatus CaptureFreeResult(ref EngineNativeCaptureResult result)
        => NativeMethods.CaptureFreeResult(ref result);

    public EngineNativeStatus AudioCreateSoundFromBlob(
        IntPtr audio,
        IntPtr data,
        nuint size,
        out ulong sound)
        => NativeMethods.AudioCreateSoundFromBlob(audio, data, size, out sound);

    public EngineNativeStatus AudioPlay(
        IntPtr audio,
        ulong sound,
        in EngineNativeAudioPlayDesc playDesc,
        out ulong emitterId)
        => NativeMethods.AudioPlay(audio, sound, in playDesc, out emitterId);

    public EngineNativeStatus AudioSetListener(IntPtr audio, in EngineNativeListenerDesc listenerDesc)
        => NativeMethods.AudioSetListener(audio, in listenerDesc);

    public EngineNativeStatus AudioSetEmitterParams(
        IntPtr audio,
        ulong emitterId,
        in EngineNativeEmitterParams emitterParams)
        => NativeMethods.AudioSetEmitterParams(audio, emitterId, in emitterParams);

    public EngineNativeStatus PhysicsStep(IntPtr physics, double deltaSeconds)
        => NativeMethods.PhysicsStep(physics, deltaSeconds);

    public EngineNativeStatus PhysicsSyncFromWorld(IntPtr physics, IntPtr writes, uint writeCount)
        => NativeMethods.PhysicsSyncFromWorld(physics, writes, writeCount);

    public EngineNativeStatus PhysicsSyncToWorld(
        IntPtr physics,
        IntPtr reads,
        uint readCapacity,
        out uint readCount)
        => NativeMethods.PhysicsSyncToWorld(physics, reads, readCapacity, out readCount);

    public EngineNativeStatus PhysicsRaycast(
        IntPtr physics,
        in EngineNativeRaycastQuery query,
        out EngineNativeRaycastHit hit)
        => NativeMethods.PhysicsRaycast(physics, in query, out hit);

    public EngineNativeStatus PhysicsSweep(
        IntPtr physics,
        in EngineNativeSweepQuery query,
        out EngineNativeSweepHit hit)
        => NativeMethods.PhysicsSweep(physics, in query, out hit);

    public EngineNativeStatus PhysicsOverlap(
        IntPtr physics,
        in EngineNativeOverlapQuery query,
        IntPtr hits,
        uint hitCapacity,
        out uint hitCount)
        => NativeMethods.PhysicsOverlap(physics, in query, hits, hitCapacity, out hitCount);

    private static partial class NativeMethods
    {
        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_native_api_version")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial uint EngineGetNativeApiVersion();

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_create")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineCreate(
            in EngineNativeCreateDesc createDesc,
            out IntPtr outEngine);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_destroy")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineDestroy(IntPtr engine);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_pump_events")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EnginePumpEvents(
            IntPtr engine,
            out EngineNativeInputSnapshot input,
            out EngineNativeWindowEvents windowEvents);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_renderer")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetRenderer(
            IntPtr engine,
            out IntPtr outRenderer);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_physics")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetPhysics(
            IntPtr engine,
            out IntPtr outPhysics);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_audio")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetAudio(
            IntPtr engine,
            out IntPtr outAudio);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_begin_frame")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererBeginFrame(
            IntPtr renderer,
            nuint requestedBytes,
            nuint alignment,
            out IntPtr outFrameMemory);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_submit")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererSubmit(
            IntPtr renderer,
            in EngineNativeRenderPacket packet);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_present")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererPresent(IntPtr renderer);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_mesh_from_blob")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateMeshFromBlob(
            IntPtr renderer,
            IntPtr data,
            nuint size,
            out ulong outMesh);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_mesh_from_cpu")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateMeshFromCpu(
            IntPtr renderer,
            in EngineNativeMeshCpuData meshData,
            out ulong outMesh);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_texture_from_blob")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateTextureFromBlob(
            IntPtr renderer,
            IntPtr data,
            nuint size,
            out ulong outTexture);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_texture_from_cpu")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateTextureFromCpu(
            IntPtr renderer,
            in EngineNativeTextureCpuData textureData,
            out ulong outTexture);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_material_from_blob")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateMaterialFromBlob(
            IntPtr renderer,
            IntPtr data,
            nuint size,
            out ulong outMaterial);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_destroy_resource")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererDestroyResource(
            IntPtr renderer,
            ulong handle);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_get_last_frame_stats")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererGetLastFrameStats(
            IntPtr renderer,
            out EngineNativeRendererFrameStats outStats);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "capture_request")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus CaptureRequest(
            IntPtr renderer,
            in EngineNativeCaptureRequest request,
            out ulong outRequestId);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "capture_poll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus CapturePoll(
            ulong requestId,
            out EngineNativeCaptureResult outResult,
            out byte outIsReady);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "capture_free_result")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus CaptureFreeResult(
            ref EngineNativeCaptureResult result);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_create_sound_from_blob")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioCreateSoundFromBlob(
            IntPtr audio,
            IntPtr data,
            nuint size,
            out ulong outSound);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_play")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioPlay(
            IntPtr audio,
            ulong sound,
            in EngineNativeAudioPlayDesc playDesc,
            out ulong outEmitterId);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_set_listener")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioSetListener(
            IntPtr audio,
            in EngineNativeListenerDesc listenerDesc);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_set_emitter_params")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioSetEmitterParams(
            IntPtr audio,
            ulong emitterId,
            in EngineNativeEmitterParams emitterParams);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_step")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsStep(IntPtr physics, double dtSeconds);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sync_from_world")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSyncFromWorld(
            IntPtr physics,
            IntPtr writes,
            uint writeCount);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sync_to_world")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSyncToWorld(
            IntPtr physics,
            IntPtr reads,
            uint readCapacity,
            out uint outReadCount);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_raycast")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsRaycast(
            IntPtr physics,
            in EngineNativeRaycastQuery query,
            out EngineNativeRaycastHit hit);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sweep")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSweep(
            IntPtr physics,
            in EngineNativeSweepQuery query,
            out EngineNativeSweepHit hit);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_overlap")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsOverlap(
            IntPtr physics,
            in EngineNativeOverlapQuery query,
            IntPtr hits,
            uint hitCapacity,
            out uint hitCount);
    }
}
