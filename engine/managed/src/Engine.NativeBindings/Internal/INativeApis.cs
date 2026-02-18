using System;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings.Internal.Interop;
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

internal interface INativeAudioApi
{
    ulong CreateSoundFromBlob(ReadOnlySpan<byte> blob);

    ulong PlaySound(ulong sound, in EngineNativeAudioPlayDesc playDesc);

    void SetAudioListener(in EngineNativeListenerDesc listenerDesc);

    void SetAudioEmitterParams(ulong emitterId, in EngineNativeEmitterParams emitterParams);
}

internal interface INativeContentApi
{
    void ContentMountPak(string pakPath);

    void ContentMountDirectory(string directoryPath);

    byte[] ContentReadFile(string assetPath);
}

internal readonly record struct NativeNetEventData(
    byte Kind,
    byte Channel,
    uint PeerId,
    byte[] Payload);

internal interface INativeNetApi
{
    IReadOnlyList<NativeNetEventData> NetPump();

    void NetSend(uint peerId, byte channel, ReadOnlySpan<byte> payload);
}

internal interface INativeRenderingApi
{
    FrameArena BeginFrame(int requestedBytes, int alignment);

    void Submit(RenderPacket packet);

    void Present();

    RenderingFrameStats GetLastFrameStats();

    MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob);

    MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices);

    TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob);

    TextureHandle CreateTextureFromCpu(uint width, uint height, ReadOnlySpan<byte> rgba8, uint strideBytes = 0);

    MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob);

    void DestroyResource(ulong handle);

    byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true);
}
