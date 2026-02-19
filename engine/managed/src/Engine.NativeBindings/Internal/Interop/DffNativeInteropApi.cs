using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;

namespace Engine.NativeBindings.Internal.Interop;

internal sealed partial class DffNativeInteropApi : INativeInteropApi
{
    static DffNativeInteropApi()
    {
        EnsureHandleInteropSupported();
        NativeLibrary.SetDllImportResolver(typeof(DffNativeInteropApi).Assembly, ResolveNativeLibrary);
    }

    public static DffNativeInteropApi Instance { get; } = new();

    private DffNativeInteropApi()
    {
    }

    private static IntPtr ResolveNativeLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        _ = assembly;
        _ = searchPath;

        if (!string.Equals(libraryName, EngineNativeConstants.LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        string? configuredPath = Environment.GetEnvironmentVariable(
            PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return IntPtr.Zero;
        }

        string normalizedPath = Path.GetFullPath(configuredPath);
        if (!File.Exists(normalizedPath))
        {
            throw new DllNotFoundException(
                $"Configured native library path '{normalizedPath}' does not exist. " +
                $"Set '{PackagedRuntimeNativeBootstrap.NativeLibraryPathEnvironmentVariable}' to a valid file path.");
        }

        PackagedRuntimeNativeBootstrap.ApplyConfiguredSearchPathForCurrentPlatform();

        if (NativeLibrary.TryLoad(normalizedPath, out IntPtr handle))
        {
            return handle;
        }

        throw new DllNotFoundException(
            $"Failed to load native library from configured path '{normalizedPath}'.");
    }

    public uint EngineGetNativeApiVersion()
        => NativeMethods.EngineGetNativeApiVersion();

    public EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine)
    {
        engine = IntPtr.Zero;
        EngineNativeStatus status = NativeMethods.EngineCreateHandle(in createDesc, out ulong outEngine);
        if (status == EngineNativeStatus.Ok)
        {
            engine = TokenFromHandle(outEngine);
        }

        return status;
    }

    public EngineNativeStatus EngineDestroy(IntPtr engine)
        => NativeMethods.EngineDestroyHandle(HandleFromToken(engine));

    public EngineNativeStatus EnginePumpEvents(
        IntPtr engine,
        out EngineNativeInputSnapshot input,
        out EngineNativeWindowEvents windowEvents)
        => NativeMethods.EnginePumpEventsHandle(
            HandleFromToken(engine),
            out input,
            out windowEvents);

    public EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer)
    {
        renderer = IntPtr.Zero;
        EngineNativeStatus status = NativeMethods.EngineGetRendererHandle(
            HandleFromToken(engine),
            out ulong outRenderer);
        if (status == EngineNativeStatus.Ok)
        {
            renderer = TokenFromHandle(outRenderer);
        }

        return status;
    }

    public EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics)
    {
        physics = IntPtr.Zero;
        EngineNativeStatus status = NativeMethods.EngineGetPhysicsHandle(
            HandleFromToken(engine),
            out ulong outPhysics);
        if (status == EngineNativeStatus.Ok)
        {
            physics = TokenFromHandle(outPhysics);
        }

        return status;
    }

    public EngineNativeStatus EngineGetAudio(IntPtr engine, out IntPtr audio)
    {
        audio = IntPtr.Zero;
        EngineNativeStatus status = NativeMethods.EngineGetAudioHandle(
            HandleFromToken(engine),
            out ulong outAudio);
        if (status == EngineNativeStatus.Ok)
        {
            audio = TokenFromHandle(outAudio);
        }

        return status;
    }

    public EngineNativeStatus EngineGetNet(IntPtr engine, out IntPtr net)
    {
        net = IntPtr.Zero;
        EngineNativeStatus status = NativeMethods.EngineGetNetHandle(
            HandleFromToken(engine),
            out ulong outNet);
        if (status == EngineNativeStatus.Ok)
        {
            net = TokenFromHandle(outNet);
        }

        return status;
    }

    public EngineNativeStatus ContentMountPak(IntPtr engine, string pakPath)
    {
        ulong engineHandle = HandleFromToken(engine);
        EngineNativeStringView pathView = CreateUtf8StringView(pakPath, out IntPtr allocatedUtf8);
        try
        {
            return NativeMethods.ContentMountPakViewHandle(engineHandle, in pathView);
        }
        finally
        {
            FreeUtf8StringViewBuffer(allocatedUtf8);
        }
    }

    public EngineNativeStatus ContentMountDirectory(IntPtr engine, string directoryPath)
    {
        ulong engineHandle = HandleFromToken(engine);
        EngineNativeStringView pathView = CreateUtf8StringView(directoryPath, out IntPtr allocatedUtf8);
        try
        {
            return NativeMethods.ContentMountDirectoryViewHandle(engineHandle, in pathView);
        }
        finally
        {
            FreeUtf8StringViewBuffer(allocatedUtf8);
        }
    }

    public EngineNativeStatus ContentReadFile(
        IntPtr engine,
        string assetPath,
        IntPtr buffer,
        nuint bufferSize,
        out nuint outSize)
    {
        ulong engineHandle = HandleFromToken(engine);
        EngineNativeStringView pathView = CreateUtf8StringView(assetPath, out IntPtr allocatedUtf8);
        try
        {
            return NativeMethods.ContentReadFileViewHandle(
                engineHandle,
                in pathView,
                buffer,
                bufferSize,
                out outSize);
        }
        finally
        {
            FreeUtf8StringViewBuffer(allocatedUtf8);
        }
    }

    public EngineNativeStatus RendererBeginFrame(
        IntPtr renderer,
        nuint requestedBytes,
        nuint alignment,
        out IntPtr frameMemory)
        => NativeMethods.RendererBeginFrameHandle(
            HandleFromToken(renderer),
            requestedBytes,
            alignment,
            out frameMemory);

    public EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet)
        => NativeMethods.RendererSubmitHandle(HandleFromToken(renderer), in packet);

    public EngineNativeStatus RendererPresent(IntPtr renderer)
        => NativeMethods.RendererPresentHandle(HandleFromToken(renderer));

    public EngineNativeStatus RendererPresentWithStats(
        IntPtr renderer,
        out EngineNativeRendererFrameStats stats)
        => NativeMethods.RendererPresentWithStatsHandle(HandleFromToken(renderer), out stats);

    public EngineNativeStatus RendererCreateMeshFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong mesh)
        => NativeMethods.RendererCreateMeshFromBlobHandle(
            HandleFromToken(renderer),
            data,
            size,
            out mesh);

    public EngineNativeStatus RendererCreateMeshFromCpu(
        IntPtr renderer,
        in EngineNativeMeshCpuData meshData,
        out ulong mesh)
        => NativeMethods.RendererCreateMeshFromCpuHandle(
            HandleFromToken(renderer),
            in meshData,
            out mesh);

    public EngineNativeStatus RendererCreateTextureFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong texture)
        => NativeMethods.RendererCreateTextureFromBlobHandle(
            HandleFromToken(renderer),
            data,
            size,
            out texture);

    public EngineNativeStatus RendererCreateTextureFromCpu(
        IntPtr renderer,
        in EngineNativeTextureCpuData textureData,
        out ulong texture)
        => NativeMethods.RendererCreateTextureFromCpuHandle(
            HandleFromToken(renderer),
            in textureData,
            out texture);

    public EngineNativeStatus RendererCreateMaterialFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong material)
        => NativeMethods.RendererCreateMaterialFromBlobHandle(
            HandleFromToken(renderer),
            data,
            size,
            out material);

    public EngineNativeStatus RendererDestroyResource(IntPtr renderer, ulong handle)
        => NativeMethods.RendererDestroyResourceHandle(HandleFromToken(renderer), handle);

    public EngineNativeStatus RendererGetLastFrameStats(
        IntPtr renderer,
        out EngineNativeRendererFrameStats stats)
        => NativeMethods.RendererGetLastFrameStatsHandle(HandleFromToken(renderer), out stats);

    public EngineNativeStatus CaptureRequest(
        IntPtr renderer,
        in EngineNativeCaptureRequest request,
        out ulong requestId)
        => NativeMethods.CaptureRequestHandle(HandleFromToken(renderer), in request, out requestId);

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
        => NativeMethods.AudioCreateSoundFromBlobHandle(
            HandleFromToken(audio),
            data,
            size,
            out sound);

    public EngineNativeStatus AudioPlay(
        IntPtr audio,
        ulong sound,
        in EngineNativeAudioPlayDesc playDesc,
        out ulong emitterId)
        => NativeMethods.AudioPlayHandle(HandleFromToken(audio), sound, in playDesc, out emitterId);

    public EngineNativeStatus AudioSetListener(IntPtr audio, in EngineNativeListenerDesc listenerDesc)
        => NativeMethods.AudioSetListenerHandle(HandleFromToken(audio), in listenerDesc);

    public EngineNativeStatus AudioSetEmitterParams(
        IntPtr audio,
        ulong emitterId,
        in EngineNativeEmitterParams emitterParams)
        => NativeMethods.AudioSetEmitterParamsHandle(
            HandleFromToken(audio),
            emitterId,
            in emitterParams);

    public EngineNativeStatus AudioSetBusParams(IntPtr audio, in EngineNativeAudioBusParams busParams)
        => NativeMethods.AudioSetBusParamsHandle(HandleFromToken(audio), in busParams);

    public EngineNativeStatus NetCreate(in EngineNativeNetDesc desc, out IntPtr net)
    {
        net = IntPtr.Zero;
        EngineNativeStatus status = NativeMethods.NetCreateHandle(in desc, out ulong outNet);
        if (status == EngineNativeStatus.Ok)
        {
            net = TokenFromHandle(outNet);
        }

        return status;
    }

    public EngineNativeStatus NetDestroy(IntPtr net)
        => NativeMethods.NetDestroyHandle(HandleFromToken(net));

    public EngineNativeStatus NetPump(IntPtr net, out EngineNativeNetEvents events)
        => NativeMethods.NetPumpHandle(HandleFromToken(net), out events);

    public EngineNativeStatus NetSend(IntPtr net, in EngineNativeNetSendDesc sendDesc)
        => NativeMethods.NetSendHandle(HandleFromToken(net), in sendDesc);

    public EngineNativeStatus PhysicsStep(IntPtr physics, double deltaSeconds)
        => NativeMethods.PhysicsStepHandle(HandleFromToken(physics), deltaSeconds);

    public EngineNativeStatus PhysicsSyncFromWorld(IntPtr physics, IntPtr writes, uint writeCount)
        => NativeMethods.PhysicsSyncFromWorldHandle(
            HandleFromToken(physics),
            writes,
            writeCount);

    public EngineNativeStatus PhysicsSyncToWorld(
        IntPtr physics,
        IntPtr reads,
        uint readCapacity,
        out uint readCount)
        => NativeMethods.PhysicsSyncToWorldHandle(
            HandleFromToken(physics),
            reads,
            readCapacity,
            out readCount);

    public EngineNativeStatus PhysicsRaycast(
        IntPtr physics,
        in EngineNativeRaycastQuery query,
        out EngineNativeRaycastHit hit)
        => NativeMethods.PhysicsRaycastHandle(HandleFromToken(physics), in query, out hit);

    public EngineNativeStatus PhysicsSweep(
        IntPtr physics,
        in EngineNativeSweepQuery query,
        out EngineNativeSweepHit hit)
        => NativeMethods.PhysicsSweepHandle(HandleFromToken(physics), in query, out hit);

    public EngineNativeStatus PhysicsOverlap(
        IntPtr physics,
        in EngineNativeOverlapQuery query,
        IntPtr hits,
        uint hitCapacity,
        out uint hitCount)
        => NativeMethods.PhysicsOverlapHandle(
            HandleFromToken(physics),
            in query,
            hits,
            hitCapacity,
            out hitCount);

    private static EngineNativeStringView CreateUtf8StringView(
        string value,
        out IntPtr allocatedUtf8)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            allocatedUtf8 = IntPtr.Zero;
            return default;
        }

        allocatedUtf8 = Marshal.StringToCoTaskMemUTF8(value);
        return new EngineNativeStringView
        {
            Data = allocatedUtf8,
            Length = checked((nuint)Encoding.UTF8.GetByteCount(value))
        };
    }

    private static void FreeUtf8StringViewBuffer(IntPtr allocatedUtf8)
    {
        if (allocatedUtf8 != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(allocatedUtf8);
        }
    }

    private static ulong HandleFromToken(IntPtr token)
        => token == IntPtr.Zero ? 0u : unchecked((ulong)token.ToInt64());

    private static IntPtr TokenFromHandle(ulong handle)
        => handle == 0u ? IntPtr.Zero : new IntPtr(unchecked((long)handle));

    private static void EnsureHandleInteropSupported()
    {
        if (IntPtr.Size < sizeof(long))
        {
            throw new PlatformNotSupportedException(
                "Handle-based native interop requires a 64-bit process.");
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_native_api_version")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial uint EngineGetNativeApiVersion();

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_create_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineCreateHandle(
            in EngineNativeCreateDesc createDesc,
            out ulong outEngine);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_destroy_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineDestroyHandle(ulong engine);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_pump_events_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EnginePumpEventsHandle(
            ulong engine,
            out EngineNativeInputSnapshot input,
            out EngineNativeWindowEvents windowEvents);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_renderer_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetRendererHandle(
            ulong engine,
            out ulong outRenderer);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_physics_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetPhysicsHandle(
            ulong engine,
            out ulong outPhysics);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_audio_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetAudioHandle(
            ulong engine,
            out ulong outAudio);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "engine_get_net_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus EngineGetNetHandle(
            ulong engine,
            out ulong outNet);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "content_mount_pak_view_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus ContentMountPakViewHandle(
            ulong engine,
            in EngineNativeStringView pakPath);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "content_mount_directory_view_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus ContentMountDirectoryViewHandle(
            ulong engine,
            in EngineNativeStringView directoryPath);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "content_read_file_view_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus ContentReadFileViewHandle(
            ulong engine,
            in EngineNativeStringView assetPath,
            IntPtr buffer,
            nuint bufferSize,
            out nuint outSize);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_begin_frame_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererBeginFrameHandle(
            ulong renderer,
            nuint requestedBytes,
            nuint alignment,
            out IntPtr outFrameMemory);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_submit_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererSubmitHandle(
            ulong renderer,
            in EngineNativeRenderPacket packet);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_present_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererPresentHandle(ulong renderer);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_present_with_stats_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererPresentWithStatsHandle(
            ulong renderer,
            out EngineNativeRendererFrameStats outStats);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_mesh_from_blob_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateMeshFromBlobHandle(
            ulong renderer,
            IntPtr data,
            nuint size,
            out ulong outMesh);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_mesh_from_cpu_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateMeshFromCpuHandle(
            ulong renderer,
            in EngineNativeMeshCpuData meshData,
            out ulong outMesh);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_texture_from_blob_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateTextureFromBlobHandle(
            ulong renderer,
            IntPtr data,
            nuint size,
            out ulong outTexture);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_texture_from_cpu_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateTextureFromCpuHandle(
            ulong renderer,
            in EngineNativeTextureCpuData textureData,
            out ulong outTexture);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_create_material_from_blob_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererCreateMaterialFromBlobHandle(
            ulong renderer,
            IntPtr data,
            nuint size,
            out ulong outMaterial);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_destroy_resource_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererDestroyResourceHandle(
            ulong renderer,
            ulong handle);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "renderer_get_last_frame_stats_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus RendererGetLastFrameStatsHandle(
            ulong renderer,
            out EngineNativeRendererFrameStats outStats);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "capture_request_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus CaptureRequestHandle(
            ulong renderer,
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

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_create_sound_from_blob_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioCreateSoundFromBlobHandle(
            ulong audio,
            IntPtr data,
            nuint size,
            out ulong outSound);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_play_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioPlayHandle(
            ulong audio,
            ulong sound,
            in EngineNativeAudioPlayDesc playDesc,
            out ulong outEmitterId);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_set_listener_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioSetListenerHandle(
            ulong audio,
            in EngineNativeListenerDesc listenerDesc);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_set_emitter_params_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioSetEmitterParamsHandle(
            ulong audio,
            ulong emitterId,
            in EngineNativeEmitterParams emitterParams);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "audio_set_bus_params_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus AudioSetBusParamsHandle(
            ulong audio,
            in EngineNativeAudioBusParams busParams);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "net_create_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus NetCreateHandle(
            in EngineNativeNetDesc desc,
            out ulong outNet);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "net_destroy_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus NetDestroyHandle(ulong net);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "net_pump_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus NetPumpHandle(
            ulong net,
            out EngineNativeNetEvents outEvents);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "net_send_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus NetSendHandle(
            ulong net,
            in EngineNativeNetSendDesc sendDesc);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_step_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsStepHandle(ulong physics, double dtSeconds);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sync_from_world_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSyncFromWorldHandle(
            ulong physics,
            IntPtr writes,
            uint writeCount);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sync_to_world_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSyncToWorldHandle(
            ulong physics,
            IntPtr reads,
            uint readCapacity,
            out uint outReadCount);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_raycast_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsRaycastHandle(
            ulong physics,
            in EngineNativeRaycastQuery query,
            out EngineNativeRaycastHit hit);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_sweep_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsSweepHandle(
            ulong physics,
            in EngineNativeSweepQuery query,
            out EngineNativeSweepHit hit);

        [LibraryImport(EngineNativeConstants.LibraryName, EntryPoint = "physics_overlap_handle")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        internal static partial EngineNativeStatus PhysicsOverlapHandle(
            ulong physics,
            in EngineNativeOverlapQuery query,
            IntPtr hits,
            uint hitCapacity,
            out uint hitCount);
    }
}
