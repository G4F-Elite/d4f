using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

internal sealed class FakeNativeInteropApi : INativeInteropApi
{
    private readonly IntPtr _engineHandle = new(101);
    private readonly IntPtr _rendererHandle = new(202);
    private readonly IntPtr _physicsHandle = new(303);
    private readonly IntPtr _audioHandle = new(404);
    private readonly IntPtr _netHandle = new(505);

    public List<string> Calls { get; } = [];

    public IntPtr RendererBeginFrameMemory { get; set; } = new(4096);

    public EngineNativeRenderPacket LastRendererSubmitPacket { get; private set; }

    public EngineNativeDrawItem? LastSubmittedDrawItem { get; private set; }

    public EngineNativeUiDrawItem? LastSubmittedUiItem { get; private set; }

    public uint LastPhysicsWriteCount { get; private set; }

    public EngineNativeBodyWrite? LastPhysicsWrite { get; private set; }

    public EngineNativeBodyRead[] PhysicsReadsToReturn { get; set; } = Array.Empty<EngineNativeBodyRead>();

    public EngineNativeRaycastQuery? LastPhysicsRaycastQuery { get; private set; }

    public EngineNativeRaycastHit PhysicsRaycastHitToReturn { get; set; }

    public EngineNativeSweepQuery? LastPhysicsSweepQuery { get; private set; }

    public EngineNativeSweepHit PhysicsSweepHitToReturn { get; set; }

    public EngineNativeOverlapQuery? LastPhysicsOverlapQuery { get; private set; }

    public EngineNativeOverlapHit[] PhysicsOverlapHitsToReturn { get; set; } = Array.Empty<EngineNativeOverlapHit>();

    public EngineNativeStatus EngineCreateStatus { get; set; } = EngineNativeStatus.Ok;

    public uint NativeApiVersionToReturn { get; set; } = EngineNativeConstants.ApiVersion;

    public EngineNativeStatus EngineDestroyStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus EnginePumpEventsStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus EngineGetRendererStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus EngineGetPhysicsStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus EngineGetAudioStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus EngineGetNetStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus ContentMountPakStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus ContentMountDirectoryStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus ContentReadFileStatus { get; set; } = EngineNativeStatus.Ok;

    public Dictionary<string, byte[]> ContentFilesToReturn { get; } = new(StringComparer.Ordinal);

    public EngineNativeStatus RendererBeginFrameStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererSubmitStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererPresentStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererCreateMeshFromBlobStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererCreateMeshFromCpuStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererCreateTextureFromBlobStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererCreateTextureFromCpuStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererCreateMaterialFromBlobStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus RendererDestroyResourceStatus { get; set; } = EngineNativeStatus.Ok;

    public ulong RendererMeshHandleToReturn { get; set; } = 0x1_0000_0001UL;

    public ulong RendererTextureHandleToReturn { get; set; } = 0x1_0000_0002UL;

    public ulong RendererMaterialHandleToReturn { get; set; } = 0x1_0000_0003UL;

    public ulong LastDestroyedRendererResource { get; private set; }

    public EngineNativeStatus RendererGetLastFrameStatsStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeRendererFrameStats RendererFrameStatsToReturn { get; set; }

    public EngineNativeStatus CaptureRequestStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus CapturePollStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus CaptureFreeResultStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus AudioCreateSoundFromBlobStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus AudioPlayStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus AudioSetListenerStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus AudioSetEmitterParamsStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus NetCreateStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus NetDestroyStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus NetPumpStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus NetSendStatus { get; set; } = EngineNativeStatus.Ok;

    public ulong CaptureRequestIdToReturn { get; set; } = 1u;

    public ulong AudioSoundHandleToReturn { get; set; } = 0x1_0000_1001UL;

    public ulong AudioEmitterIdToReturn { get; set; } = 0x1_0000_1002UL;

    public EngineNativeCaptureRequest? LastCaptureRequest { get; private set; }

    public ulong LastCapturePollRequestId { get; private set; }

    public byte CapturePollReadyToReturn { get; set; } = 1;

    public uint CaptureResultWidthToReturn { get; set; } = 1u;

    public uint CaptureResultHeightToReturn { get; set; } = 1u;

    public uint CaptureResultStrideToReturn { get; set; } = 4u;

    public uint CaptureResultFormatToReturn { get; set; } = (uint)EngineNativeCaptureFormat.Rgba8Unorm;

    public byte[] CapturePixelsToReturn { get; set; } = [0, 0, 0, 255];

    public bool CaptureResultFreed { get; private set; }

    public EngineNativeAudioPlayDesc? LastAudioPlayDesc { get; private set; }

    public ulong LastAudioPlaySoundHandle { get; private set; }

    public ulong LastAudioSetEmitterId { get; private set; }

    public EngineNativeListenerDesc? LastAudioListenerDesc { get; private set; }

    public EngineNativeEmitterParams? LastAudioEmitterParams { get; private set; }

    public string? LastMountedPakPath { get; private set; }

    public string? LastMountedDirectoryPath { get; private set; }

    public EngineNativeStatus PhysicsStepStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus PhysicsSyncFromWorldStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus PhysicsSyncToWorldStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus PhysicsRaycastStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus PhysicsSweepStatus { get; set; } = EngineNativeStatus.Ok;

    public EngineNativeStatus PhysicsOverlapStatus { get; set; } = EngineNativeStatus.Ok;

    public uint EngineGetNativeApiVersion()
    {
        Calls.Add("engine_get_native_api_version");
        return NativeApiVersionToReturn;
    }

    public EngineNativeStatus EngineCreate(in EngineNativeCreateDesc createDesc, out IntPtr engine)
    {
        Calls.Add("engine_create");
        engine = EngineCreateStatus == EngineNativeStatus.Ok ? _engineHandle : IntPtr.Zero;
        return EngineCreateStatus;
    }

    public EngineNativeStatus EngineDestroy(IntPtr engine)
    {
        Calls.Add("engine_destroy");
        return EngineDestroyStatus;
    }

    public EngineNativeStatus EnginePumpEvents(
        IntPtr engine,
        out EngineNativeInputSnapshot input,
        out EngineNativeWindowEvents windowEvents)
    {
        Calls.Add("engine_pump_events");

        input = new EngineNativeInputSnapshot
        {
            FrameIndex = 1,
            ButtonsMask = 0,
            MouseX = 0,
            MouseY = 0
        };

        windowEvents = new EngineNativeWindowEvents
        {
            ShouldClose = 0,
            Width = 1280,
            Height = 720
        };

        return EnginePumpEventsStatus;
    }

    public EngineNativeStatus EngineGetRenderer(IntPtr engine, out IntPtr renderer)
    {
        Calls.Add("engine_get_renderer");
        renderer = EngineGetRendererStatus == EngineNativeStatus.Ok ? _rendererHandle : IntPtr.Zero;
        return EngineGetRendererStatus;
    }

    public EngineNativeStatus EngineGetPhysics(IntPtr engine, out IntPtr physics)
    {
        Calls.Add("engine_get_physics");
        physics = EngineGetPhysicsStatus == EngineNativeStatus.Ok ? _physicsHandle : IntPtr.Zero;
        return EngineGetPhysicsStatus;
    }

    public EngineNativeStatus EngineGetAudio(IntPtr engine, out IntPtr audio)
    {
        Calls.Add("engine_get_audio");
        audio = EngineGetAudioStatus == EngineNativeStatus.Ok ? _audioHandle : IntPtr.Zero;
        return EngineGetAudioStatus;
    }

    public EngineNativeStatus EngineGetNet(IntPtr engine, out IntPtr net)
    {
        Calls.Add("engine_get_net");
        net = EngineGetNetStatus == EngineNativeStatus.Ok ? _netHandle : IntPtr.Zero;
        return EngineGetNetStatus;
    }

    public EngineNativeStatus ContentMountPak(IntPtr engine, string pakPath)
    {
        Calls.Add("content_mount_pak");
        LastMountedPakPath = pakPath;
        return ContentMountPakStatus;
    }

    public EngineNativeStatus ContentMountDirectory(IntPtr engine, string directoryPath)
    {
        Calls.Add("content_mount_directory");
        LastMountedDirectoryPath = directoryPath;
        return ContentMountDirectoryStatus;
    }

    public EngineNativeStatus ContentReadFile(
        IntPtr engine,
        string assetPath,
        IntPtr buffer,
        nuint bufferSize,
        out nuint outSize)
    {
        Calls.Add("content_read_file");
        outSize = 0u;

        if (ContentReadFileStatus != EngineNativeStatus.Ok)
        {
            return ContentReadFileStatus;
        }

        if (!ContentFilesToReturn.TryGetValue(assetPath, out byte[]? bytes))
        {
            return EngineNativeStatus.NotFound;
        }

        outSize = checked((nuint)bytes.Length);
        if (buffer == IntPtr.Zero)
        {
            return bufferSize == 0u
                ? EngineNativeStatus.Ok
                : EngineNativeStatus.InvalidArgument;
        }

        if (bufferSize < outSize)
        {
            return EngineNativeStatus.InvalidArgument;
        }

        Marshal.Copy(bytes, 0, buffer, bytes.Length);
        return EngineNativeStatus.Ok;
    }

    public EngineNativeStatus RendererBeginFrame(
        IntPtr renderer,
        nuint requestedBytes,
        nuint alignment,
        out IntPtr frameMemory)
    {
        Calls.Add("renderer_begin_frame");
        frameMemory = RendererBeginFrameStatus == EngineNativeStatus.Ok ? RendererBeginFrameMemory : IntPtr.Zero;
        return RendererBeginFrameStatus;
    }

    public EngineNativeStatus RendererSubmit(IntPtr renderer, in EngineNativeRenderPacket packet)
    {
        Calls.Add("renderer_submit");
        LastRendererSubmitPacket = packet;
        LastSubmittedDrawItem = packet.DrawItemCount > 0 && packet.DrawItems != IntPtr.Zero
            ? Marshal.PtrToStructure<EngineNativeDrawItem>(packet.DrawItems)
            : null;
        LastSubmittedUiItem = packet.UiItemCount > 0 && packet.UiItems != IntPtr.Zero
            ? Marshal.PtrToStructure<EngineNativeUiDrawItem>(packet.UiItems)
            : null;
        return RendererSubmitStatus;
    }

    public EngineNativeStatus RendererPresent(IntPtr renderer)
    {
        Calls.Add("renderer_present");
        return RendererPresentStatus;
    }

    public EngineNativeStatus RendererCreateMeshFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong mesh)
    {
        Calls.Add("renderer_create_mesh_from_blob");
        mesh = RendererCreateMeshFromBlobStatus == EngineNativeStatus.Ok ? RendererMeshHandleToReturn : 0u;
        return RendererCreateMeshFromBlobStatus;
    }

    public EngineNativeStatus RendererCreateMeshFromCpu(
        IntPtr renderer,
        in EngineNativeMeshCpuData meshData,
        out ulong mesh)
    {
        Calls.Add("renderer_create_mesh_from_cpu");
        mesh = RendererCreateMeshFromCpuStatus == EngineNativeStatus.Ok ? RendererMeshHandleToReturn : 0u;
        return RendererCreateMeshFromCpuStatus;
    }

    public EngineNativeStatus RendererCreateTextureFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong texture)
    {
        Calls.Add("renderer_create_texture_from_blob");
        texture = RendererCreateTextureFromBlobStatus == EngineNativeStatus.Ok ? RendererTextureHandleToReturn : 0u;
        return RendererCreateTextureFromBlobStatus;
    }

    public EngineNativeStatus RendererCreateTextureFromCpu(
        IntPtr renderer,
        in EngineNativeTextureCpuData textureData,
        out ulong texture)
    {
        Calls.Add("renderer_create_texture_from_cpu");
        texture = RendererCreateTextureFromCpuStatus == EngineNativeStatus.Ok ? RendererTextureHandleToReturn : 0u;
        return RendererCreateTextureFromCpuStatus;
    }

    public EngineNativeStatus RendererCreateMaterialFromBlob(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong material)
    {
        Calls.Add("renderer_create_material_from_blob");
        material = RendererCreateMaterialFromBlobStatus == EngineNativeStatus.Ok ? RendererMaterialHandleToReturn : 0u;
        return RendererCreateMaterialFromBlobStatus;
    }

    public EngineNativeStatus RendererDestroyResource(IntPtr renderer, ulong handle)
    {
        Calls.Add("renderer_destroy_resource");
        LastDestroyedRendererResource = handle;
        return RendererDestroyResourceStatus;
    }

    public EngineNativeStatus RendererGetLastFrameStats(
        IntPtr renderer,
        out EngineNativeRendererFrameStats stats)
    {
        Calls.Add("renderer_get_last_frame_stats");
        stats = RendererFrameStatsToReturn;
        return RendererGetLastFrameStatsStatus;
    }

    public EngineNativeStatus PhysicsStep(IntPtr physics, double deltaSeconds)
    {
        Calls.Add("physics_step");
        return PhysicsStepStatus;
    }

    public EngineNativeStatus CaptureRequest(
        IntPtr renderer,
        in EngineNativeCaptureRequest request,
        out ulong requestId)
    {
        Calls.Add("capture_request");
        LastCaptureRequest = request;
        requestId = CaptureRequestStatus == EngineNativeStatus.Ok ? CaptureRequestIdToReturn : 0u;
        return CaptureRequestStatus;
    }

    public EngineNativeStatus CapturePoll(
        ulong requestId,
        out EngineNativeCaptureResult result,
        out byte isReady)
    {
        Calls.Add("capture_poll");
        LastCapturePollRequestId = requestId;
        isReady = 0;
        result = default;

        if (CapturePollStatus != EngineNativeStatus.Ok)
        {
            return CapturePollStatus;
        }

        if (CapturePollReadyToReturn == 0)
        {
            return EngineNativeStatus.Ok;
        }

        isReady = 1;
        IntPtr pointer = IntPtr.Zero;
        if (CapturePixelsToReturn.Length > 0)
        {
            pointer = Marshal.AllocHGlobal(CapturePixelsToReturn.Length);
            Marshal.Copy(CapturePixelsToReturn, 0, pointer, CapturePixelsToReturn.Length);
        }

        result = new EngineNativeCaptureResult
        {
            Width = CaptureResultWidthToReturn,
            Height = CaptureResultHeightToReturn,
            Stride = CaptureResultStrideToReturn,
            Format = CaptureResultFormatToReturn,
            Pixels = pointer,
            PixelBytes = checked((nuint)CapturePixelsToReturn.Length)
        };
        return EngineNativeStatus.Ok;
    }

    public EngineNativeStatus CaptureFreeResult(ref EngineNativeCaptureResult result)
    {
        Calls.Add("capture_free_result");
        CaptureResultFreed = true;

        if (CaptureFreeResultStatus != EngineNativeStatus.Ok)
        {
            return CaptureFreeResultStatus;
        }

        if (result.Pixels != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(result.Pixels);
        }

        result = default;
        return EngineNativeStatus.Ok;
    }

    public EngineNativeStatus AudioCreateSoundFromBlob(
        IntPtr audio,
        IntPtr data,
        nuint size,
        out ulong sound)
    {
        Calls.Add("audio_create_sound_from_blob");
        sound = AudioCreateSoundFromBlobStatus == EngineNativeStatus.Ok ? AudioSoundHandleToReturn : 0u;
        return AudioCreateSoundFromBlobStatus;
    }

    public EngineNativeStatus AudioPlay(
        IntPtr audio,
        ulong sound,
        in EngineNativeAudioPlayDesc playDesc,
        out ulong emitterId)
    {
        Calls.Add("audio_play");
        LastAudioPlaySoundHandle = sound;
        LastAudioPlayDesc = playDesc;
        emitterId = AudioPlayStatus == EngineNativeStatus.Ok ? AudioEmitterIdToReturn : 0u;
        return AudioPlayStatus;
    }

    public EngineNativeStatus AudioSetListener(IntPtr audio, in EngineNativeListenerDesc listenerDesc)
    {
        Calls.Add("audio_set_listener");
        LastAudioListenerDesc = listenerDesc;
        return AudioSetListenerStatus;
    }

    public EngineNativeStatus AudioSetEmitterParams(
        IntPtr audio,
        ulong emitterId,
        in EngineNativeEmitterParams emitterParams)
    {
        Calls.Add("audio_set_emitter_params");
        LastAudioSetEmitterId = emitterId;
        LastAudioEmitterParams = emitterParams;
        return AudioSetEmitterParamsStatus;
    }

    public EngineNativeStatus NetCreate(in EngineNativeNetDesc desc, out IntPtr net)
    {
        Calls.Add("net_create");
        net = NetCreateStatus == EngineNativeStatus.Ok ? _netHandle : IntPtr.Zero;
        return NetCreateStatus;
    }

    public EngineNativeStatus NetDestroy(IntPtr net)
    {
        Calls.Add("net_destroy");
        return NetDestroyStatus;
    }

    public EngineNativeStatus NetPump(IntPtr net, out EngineNativeNetEvents events)
    {
        Calls.Add("net_pump");
        events = default;
        return NetPumpStatus;
    }

    public EngineNativeStatus NetSend(IntPtr net, in EngineNativeNetSendDesc sendDesc)
    {
        Calls.Add("net_send");
        return NetSendStatus;
    }

    public EngineNativeStatus PhysicsSyncFromWorld(IntPtr physics, IntPtr writes, uint writeCount)
    {
        Calls.Add("physics_sync_from_world");
        LastPhysicsWriteCount = writeCount;
        LastPhysicsWrite = writeCount > 0 && writes != IntPtr.Zero
            ? Marshal.PtrToStructure<EngineNativeBodyWrite>(writes)
            : null;
        return PhysicsSyncFromWorldStatus;
    }

    public EngineNativeStatus PhysicsSyncToWorld(
        IntPtr physics,
        IntPtr reads,
        uint readCapacity,
        out uint readCount)
    {
        Calls.Add("physics_sync_to_world");

        var writableCount = Math.Min((uint)PhysicsReadsToReturn.Length, readCapacity);
        for (var i = 0u; i < writableCount; i++)
        {
            var destination = reads + checked((int)(i * (uint)Marshal.SizeOf<EngineNativeBodyRead>()));
            Marshal.StructureToPtr(PhysicsReadsToReturn[i], destination, fDeleteOld: false);
        }

        readCount = writableCount;
        return PhysicsSyncToWorldStatus;
    }

    public EngineNativeStatus PhysicsRaycast(
        IntPtr physics,
        in EngineNativeRaycastQuery query,
        out EngineNativeRaycastHit hit)
    {
        Calls.Add("physics_raycast");
        LastPhysicsRaycastQuery = query;
        hit = PhysicsRaycastHitToReturn;
        return PhysicsRaycastStatus;
    }

    public EngineNativeStatus PhysicsSweep(
        IntPtr physics,
        in EngineNativeSweepQuery query,
        out EngineNativeSweepHit hit)
    {
        Calls.Add("physics_sweep");
        LastPhysicsSweepQuery = query;
        hit = PhysicsSweepHitToReturn;
        return PhysicsSweepStatus;
    }

    public EngineNativeStatus PhysicsOverlap(
        IntPtr physics,
        in EngineNativeOverlapQuery query,
        IntPtr hits,
        uint hitCapacity,
        out uint hitCount)
    {
        Calls.Add("physics_overlap");
        LastPhysicsOverlapQuery = query;

        var writableCount = Math.Min((uint)PhysicsOverlapHitsToReturn.Length, hitCapacity);
        for (var i = 0u; i < writableCount; i++)
        {
            var destination = hits + checked((int)(i * (uint)Marshal.SizeOf<EngineNativeOverlapHit>()));
            Marshal.StructureToPtr(PhysicsOverlapHitsToReturn[i], destination, fDeleteOld: false);
        }

        hitCount = writableCount;
        return PhysicsOverlapStatus;
    }

    public int CountCall(string callName)
    {
        var count = 0;
        foreach (var call in Calls)
        {
            if (string.Equals(call, callName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}

internal readonly record struct DummyComponent(int Value);
