using System;
using System.Diagnostics;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.NativeBindings.Internal.Interop;
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

    public bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit)
    {
        hit = default;
        return false;
    }

    public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
    {
        return 0;
    }
}

internal sealed class NativeUiApiStub : INativeUiApi
{
    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);
    }
}

internal sealed class NativeAudioApiStub : INativeAudioApi
{
    private readonly HashSet<ulong> _sounds = [];
    private readonly HashSet<ulong> _emitters = [];
    private ulong _nextSoundHandle = 1u;
    private ulong _nextEmitterId = 1u;

    public ulong CreateSoundFromBlob(ReadOnlySpan<byte> blob)
    {
        if (blob.IsEmpty)
        {
            throw new ArgumentException("Sound blob cannot be empty.", nameof(blob));
        }

        ulong handle = _nextSoundHandle++;
        _sounds.Add(handle);
        return handle;
    }

    public ulong PlaySound(ulong sound, in EngineNativeAudioPlayDesc playDesc)
    {
        if (sound == 0u || !_sounds.Contains(sound))
        {
            throw new KeyNotFoundException($"Sound handle '{sound}' is not known.");
        }

        if (playDesc.Pitch <= 0f || playDesc.Volume < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(playDesc), "Play descriptor has invalid scalar values.");
        }

        ulong emitter = _nextEmitterId++;
        _emitters.Add(emitter);
        return emitter;
    }

    public void SetAudioListener(in EngineNativeListenerDesc listenerDesc)
    {
        _ = listenerDesc;
    }

    public void SetAudioEmitterParams(ulong emitterId, in EngineNativeEmitterParams emitterParams)
    {
        if (emitterId == 0u || !_emitters.Contains(emitterId))
        {
            throw new KeyNotFoundException($"Emitter id '{emitterId}' is not known.");
        }

        if (emitterParams.Pitch <= 0f || emitterParams.Volume < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(emitterParams), "Emitter params have invalid scalar values.");
        }
    }
}

internal sealed class NativeContentApiStub : INativeContentApi
{
    private readonly HashSet<string> _mountedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public void ContentMountPak(string pakPath)
    {
        if (string.IsNullOrWhiteSpace(pakPath))
        {
            throw new ArgumentException("Pak path cannot be empty.", nameof(pakPath));
        }

        _mountedPaths.Add(pakPath.Trim());
    }

    public void ContentMountDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));
        }

        _mountedPaths.Add(directoryPath.Trim());
    }

    public byte[] ContentReadFile(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new ArgumentException("Asset path cannot be empty.", nameof(assetPath));
        }

        string normalizedPath = assetPath.Trim().Replace('\\', '/');
        if (_files.TryGetValue(normalizedPath, out byte[]? payload))
        {
            return payload.ToArray();
        }

        throw new FileNotFoundException($"Asset '{normalizedPath}' was not found in mounted content sources.");
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

    public RenderingFrameStats GetLastFrameStats() => RenderingFrameStats.Empty;

    public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
        => NoopRenderingFacade.Instance.CreateMeshFromBlob(blob);

    public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
        => NoopRenderingFacade.Instance.CreateMeshFromCpu(positions, indices);

    public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
        => NoopRenderingFacade.Instance.CreateTextureFromBlob(blob);

    public TextureHandle CreateTextureFromCpu(
        uint width,
        uint height,
        ReadOnlySpan<byte> rgba8,
        uint strideBytes = 0)
        => NoopRenderingFacade.Instance.CreateTextureFromCpu(width, height, rgba8, strideBytes);

    public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
        => NoopRenderingFacade.Instance.CreateMaterialFromBlob(blob);

    public void DestroyResource(ulong handle)
        => NoopRenderingFacade.Instance.DestroyResource(handle);

    public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
        => NoopRenderingFacade.Instance.CaptureFrameRgba8(width, height, includeAlpha);
}
