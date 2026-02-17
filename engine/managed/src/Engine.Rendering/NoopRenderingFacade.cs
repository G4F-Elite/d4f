using System;
using System.Collections.Generic;
using Engine.Core.Handles;

namespace Engine.Rendering;

public sealed class NoopRenderingFacade : IRenderingFacade
{
    public static NoopRenderingFacade Instance { get; } = new();
    private readonly object _resourceSync = new();
    private readonly HashSet<ulong> _resourceHandles = [];
    private ulong _nextResourceHandle = 1u;

    private NoopRenderingFacade()
    {
    }

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
    {
        return new MeshHandle(AllocateResource(blob));
    }

    public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
    {
        ValidateMeshCpuData(positions, indices);
        int positionBytes = checked(positions.Length * sizeof(float));
        int indexBytes = checked(indices.Length * sizeof(uint));
        var payload = new byte[checked(positionBytes + indexBytes)];
        return new MeshHandle(AllocateResource(payload));
    }

    public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
    {
        return new TextureHandle(AllocateResource(blob));
    }

    public TextureHandle CreateTextureFromCpu(
        uint width,
        uint height,
        ReadOnlySpan<byte> rgba8,
        uint strideBytes = 0)
    {
        ValidateTextureCpuData(width, height, rgba8, strideBytes);
        return new TextureHandle(AllocateResource(rgba8));
    }

    public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
    {
        return new MaterialHandle(AllocateResource(blob));
    }

    public void DestroyResource(ulong handle)
    {
        if (handle == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle value must be non-zero.");
        }

        lock (_resourceSync)
        {
            if (!_resourceHandles.Remove(handle))
            {
                throw new InvalidOperationException($"Resource handle '{handle}' does not exist.");
            }
        }
    }

    public byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true)
    {
        if (width == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Capture width must be greater than zero.");
        }

        if (height == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Capture height must be greater than zero.");
        }

        int rowStride = checked((int)width * 4);
        byte[] rgba = new byte[checked(rowStride * (int)height)];

        int widthDenominator = Math.Max(1, (int)width - 1);
        int heightDenominator = Math.Max(1, (int)height - 1);
        byte alpha = includeAlpha ? (byte)220 : (byte)255;

        for (int y = 0; y < (int)height; y++)
        {
            for (int x = 0; x < (int)width; x++)
            {
                int pixelOffset = y * rowStride + x * 4;
                byte red = (byte)(x * 255 / widthDenominator);
                byte green = (byte)(y * 255 / heightDenominator);
                byte blue = ((x / 8 + y / 8) & 1) == 0 ? (byte)192 : (byte)48;
                rgba[pixelOffset] = red;
                rgba[pixelOffset + 1] = green;
                rgba[pixelOffset + 2] = blue;
                rgba[pixelOffset + 3] = alpha;
            }
        }

        return rgba;
    }

    private ulong AllocateResource(ReadOnlySpan<byte> blob)
    {
        if (blob.Length == 0)
        {
            throw new ArgumentException("Blob payload must be non-empty.", nameof(blob));
        }

        lock (_resourceSync)
        {
            ulong handle = _nextResourceHandle++;
            if (handle == 0u)
            {
                throw new InvalidOperationException("Resource handle counter overflow.");
            }

            _resourceHandles.Add(handle);
            return handle;
        }
    }

    private static void ValidateMeshCpuData(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
    {
        if (positions.Length == 0 || (positions.Length % 3) != 0)
        {
            throw new ArgumentException("Mesh CPU positions must be non-empty and contain XYZ triplets.", nameof(positions));
        }

        if (indices.Length == 0 || (indices.Length % 3) != 0)
        {
            throw new ArgumentException("Mesh CPU indices must be non-empty and divisible by three.", nameof(indices));
        }

        uint vertexCount = checked((uint)(positions.Length / 3));
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] >= vertexCount)
            {
                throw new ArgumentException(
                    $"Mesh CPU index '{indices[i]}' is outside vertex range [0, {vertexCount - 1}].",
                    nameof(indices));
            }
        }
    }

    private static void ValidateTextureCpuData(
        uint width,
        uint height,
        ReadOnlySpan<byte> rgba8,
        uint strideBytes)
    {
        if (width == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture width must be greater than zero.");
        }

        if (height == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Texture height must be greater than zero.");
        }

        uint minimumStride = checked(width * 4u);
        uint stride = strideBytes == 0u ? minimumStride : strideBytes;
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(strideBytes), "Texture stride must cover width * 4 bytes.");
        }

        int expectedLength = checked((int)(stride * height));
        if (rgba8.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Texture CPU payload length {rgba8.Length} does not match dimensions {width}x{height} and stride {stride}.",
                nameof(rgba8));
        }
    }
}
