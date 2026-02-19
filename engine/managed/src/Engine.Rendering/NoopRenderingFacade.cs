using System;
using System.Collections.Generic;
using Engine.Core.Handles;

namespace Engine.Rendering;

public sealed class NoopRenderingFacade : IRenderingFacade, IAdvancedCaptureRenderingFacade
{
    public static NoopRenderingFacade Instance { get; } = new();
    private readonly object _resourceSync = new();
    private readonly Dictionary<ulong, ResourceMetadata> _resourceHandles = [];
    private ulong _nextResourceHandle = 1u;
    private uint _submittedDrawItemCount;
    private uint _submittedUiItemCount;
    private ulong _submittedTriangleCount;
    private ulong _presentCount;
    private ulong _pendingUploadBytes;
    private ulong _gpuMemoryBytes;
    private RenderingFrameStats _lastFrameStats = RenderingFrameStats.Empty;
    private RenderDebugViewMode _lastSubmittedDebugViewMode = RenderDebugViewMode.None;

    private NoopRenderingFacade()
    {
    }

    public FrameArena BeginFrame(int requestedBytes, int alignment)
    {
        lock (_resourceSync)
        {
            _submittedDrawItemCount = 0u;
            _submittedUiItemCount = 0u;
            _submittedTriangleCount = 0u;
        }

        return new FrameArena(requestedBytes, alignment);
    }

    public void Submit(RenderPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        lock (_resourceSync)
        {
            _submittedDrawItemCount = checked(_submittedDrawItemCount + ResolveSubmittedDrawItemCount(packet));
            _submittedUiItemCount = checked(_submittedUiItemCount + ResolveSubmittedUiItemCount(packet));
            _submittedTriangleCount = checked(_submittedTriangleCount + ResolveTriangleCount(packet.DrawCommands));
            _lastSubmittedDebugViewMode = packet.DebugViewMode;
        }
    }

    public void Present()
    {
        lock (_resourceSync)
        {
            _presentCount = checked(_presentCount + 1u);
            _lastFrameStats = new RenderingFrameStats(
                _submittedDrawItemCount,
                _submittedUiItemCount,
                0u,
                _presentCount,
                0u,
                0u,
                0u,
                _submittedTriangleCount,
                _pendingUploadBytes,
                _gpuMemoryBytes,
                RenderingBackendKind.Noop);
            _pendingUploadBytes = 0u;
            _submittedDrawItemCount = 0u;
            _submittedUiItemCount = 0u;
            _submittedTriangleCount = 0u;
        }
    }

    public RenderingFrameStats GetLastFrameStats()
    {
        lock (_resourceSync)
        {
            return _lastFrameStats;
        }
    }

    public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
    {
        return new MeshHandle(AllocateResource(blob.Length, triangleCount: 0u));
    }

    public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
    {
        ValidateMeshCpuData(positions, indices);
        int positionBytes = checked(positions.Length * sizeof(float));
        int indexBytes = checked(indices.Length * sizeof(uint));
        int payloadBytes = checked(positionBytes + indexBytes);
        ulong triangleCount = checked((ulong)(indices.Length / 3));
        return new MeshHandle(AllocateResource(payloadBytes, triangleCount));
    }

    public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
    {
        return new TextureHandle(AllocateResource(blob.Length, triangleCount: 0u));
    }

    public TextureHandle CreateTextureFromCpu(
        uint width,
        uint height,
        ReadOnlySpan<byte> rgba8,
        uint strideBytes = 0)
    {
        ValidateTextureCpuData(width, height, rgba8, strideBytes);
        return new TextureHandle(AllocateResource(rgba8.Length, triangleCount: 0u));
    }

    public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
    {
        return new MaterialHandle(AllocateResource(blob.Length, triangleCount: 0u));
    }

    public void DestroyResource(ulong handle)
    {
        if (handle == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle value must be non-zero.");
        }

        lock (_resourceSync)
        {
            if (!_resourceHandles.Remove(handle, out ResourceMetadata metadata))
            {
                throw new InvalidOperationException($"Resource handle '{handle}' does not exist.");
            }

            _gpuMemoryBytes = checked(_gpuMemoryBytes - metadata.ByteSize);
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

        switch (_lastSubmittedDebugViewMode)
        {
            case RenderDebugViewMode.Depth:
                FillDepthCapture(rgba, (int)width, (int)height, rowStride, widthDenominator, heightDenominator, alpha);
                break;
            case RenderDebugViewMode.Normals:
                FillNormalsCapture(rgba, (int)width, (int)height, rowStride, widthDenominator, heightDenominator, alpha);
                break;
            case RenderDebugViewMode.Albedo:
                FillAlbedoCapture(rgba, (int)width, (int)height, rowStride, widthDenominator, heightDenominator, alpha);
                break;
            case RenderDebugViewMode.Roughness:
                FillRoughnessCapture(rgba, (int)width, (int)height, rowStride, widthDenominator, heightDenominator, alpha);
                break;
            case RenderDebugViewMode.AmbientOcclusion:
                FillAmbientOcclusionCapture(rgba, (int)width, (int)height, rowStride, widthDenominator, heightDenominator, alpha);
                break;
            default:
                FillColorCapture(rgba, (int)width, (int)height, rowStride, widthDenominator, heightDenominator, alpha);
                break;
        }

        return rgba;
    }

    public bool TryCaptureFrameRgba16Float(uint width, uint height, out byte[] rgba16Float, bool includeAlpha = true)
    {
        byte[] rgba8 = CaptureFrameRgba8(width, height, includeAlpha);
        var rgba16 = new byte[checked(rgba8.Length * 2)];
        for (int pixelIndex = 0, outputIndex = 0; pixelIndex < rgba8.Length; pixelIndex++, outputIndex += 2)
        {
            ushort half = FloatToHalfBits(rgba8[pixelIndex] / 255f);
            rgba16[outputIndex] = (byte)(half & 0xFF);
            rgba16[outputIndex + 1] = (byte)((half >> 8) & 0xFF);
        }

        rgba16Float = rgba16;
        return true;
    }

    private static ushort FloatToHalfBits(float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        uint sign = (bits >> 16) & 0x8000u;
        int exponent = (int)((bits >> 23) & 0xFFu) - 127 + 15;
        uint mantissa = bits & 0x007FFFFFu;

        if (exponent <= 0)
        {
            if (exponent < -10)
            {
                return (ushort)sign;
            }

            mantissa = (mantissa | 0x00800000u) >> (1 - exponent);
            if ((mantissa & 0x00001000u) != 0u)
            {
                mantissa += 0x00002000u;
            }

            return (ushort)(sign | (mantissa >> 13));
        }

        if (exponent >= 31)
        {
            if (mantissa == 0u)
            {
                return (ushort)(sign | 0x7C00u);
            }

            mantissa >>= 13;
            return (ushort)(sign | 0x7C00u | mantissa | (mantissa == 0u ? 1u : 0u));
        }

        if ((mantissa & 0x00001000u) != 0u)
        {
            mantissa += 0x00002000u;
            if ((mantissa & 0x00800000u) != 0u)
            {
                mantissa = 0u;
                exponent++;
                if (exponent >= 31)
                {
                    return (ushort)(sign | 0x7C00u);
                }
            }
        }

        return (ushort)(sign | ((uint)exponent << 10) | (mantissa >> 13));
    }

    private static void FillColorCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
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
    }

    private static void FillDepthCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = y * rowStride + x * 4;
                byte depth = (byte)(255 - (y * 255 / heightDenominator));
                byte edge = (byte)(x * 24 / widthDenominator);
                byte value = (byte)Math.Clamp(depth - edge, 0, 255);
                rgba[pixelOffset] = value;
                rgba[pixelOffset + 1] = value;
                rgba[pixelOffset + 2] = value;
                rgba[pixelOffset + 3] = alpha;
            }
        }
    }

    private static void FillNormalsCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            float ny = (y / (float)heightDenominator) * 2f - 1f;
            for (int x = 0; x < width; x++)
            {
                float nx = (x / (float)widthDenominator) * 2f - 1f;
                float nz = MathF.Sqrt(MathF.Max(0f, 1f - MathF.Min(1f, nx * nx + ny * ny)));
                int pixelOffset = y * rowStride + x * 4;
                rgba[pixelOffset] = (byte)Math.Clamp((int)MathF.Round((nx * 0.5f + 0.5f) * 255f), 0, 255);
                rgba[pixelOffset + 1] = (byte)Math.Clamp((int)MathF.Round(((-ny) * 0.5f + 0.5f) * 255f), 0, 255);
                rgba[pixelOffset + 2] = (byte)Math.Clamp((int)MathF.Round(nz * 255f), 0, 255);
                rgba[pixelOffset + 3] = alpha;
            }
        }
    }

    private static void FillAlbedoCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = y * rowStride + x * 4;
                byte warm = (byte)(140 + (x * 80 / widthDenominator));
                byte mid = (byte)(95 + (y * 100 / heightDenominator));
                byte cool = (byte)(70 + (((x + y) * 70) / Math.Max(1, widthDenominator + heightDenominator)));
                rgba[pixelOffset] = warm;
                rgba[pixelOffset + 1] = mid;
                rgba[pixelOffset + 2] = cool;
                rgba[pixelOffset + 3] = alpha;
            }
        }
    }

    private static void FillShadowCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = y * rowStride + x * 4;
                bool lit = ((x / 6) + (y / 6)) % 2 == 0;
                byte baseValue = lit ? (byte)168 : (byte)54;
                byte horizon = (byte)(y * 56 / heightDenominator);
                byte value = (byte)Math.Clamp(baseValue - horizon, 0, 255);
                rgba[pixelOffset] = value;
                rgba[pixelOffset + 1] = value;
                rgba[pixelOffset + 2] = value;
                rgba[pixelOffset + 3] = alpha;
            }
        }
    }

    private static void FillRoughnessCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = y * rowStride + x * 4;
                int value = 20 + (x * 200 / widthDenominator) + (y * 35 / heightDenominator);
                byte roughness = (byte)Math.Clamp(value, 0, 255);
                rgba[pixelOffset] = roughness;
                rgba[pixelOffset + 1] = roughness;
                rgba[pixelOffset + 2] = roughness;
                rgba[pixelOffset + 3] = alpha;
            }
        }
    }

    private static void FillAmbientOcclusionCapture(
        byte[] rgba,
        int width,
        int height,
        int rowStride,
        int widthDenominator,
        int heightDenominator,
        byte alpha)
    {
        for (int y = 0; y < height; y++)
        {
            float uy = (y / (float)heightDenominator) - 0.5f;
            for (int x = 0; x < width; x++)
            {
                float ux = (x / (float)widthDenominator) - 0.5f;
                float radial = MathF.Sqrt(MathF.Min(1f, ux * ux + uy * uy));
                float value = Math.Clamp(0.9f - (radial * 1.4f), 0.12f, 0.9f);
                byte ao = (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
                int pixelOffset = y * rowStride + x * 4;
                rgba[pixelOffset] = ao;
                rgba[pixelOffset + 1] = ao;
                rgba[pixelOffset + 2] = ao;
                rgba[pixelOffset + 3] = alpha;
            }
        }
    }

    private ulong AllocateResource(int payloadLength, ulong triangleCount)
    {
        if (payloadLength == 0)
        {
            throw new ArgumentException("Blob payload must be non-empty.", nameof(payloadLength));
        }

        ulong byteSize = checked((ulong)payloadLength);
        lock (_resourceSync)
        {
            ulong handle = _nextResourceHandle++;
            if (handle == 0u)
            {
                throw new InvalidOperationException("Resource handle counter overflow.");
            }

            _resourceHandles.Add(handle, new ResourceMetadata(byteSize, triangleCount));
            _pendingUploadBytes = checked(_pendingUploadBytes + byteSize);
            _gpuMemoryBytes = checked(_gpuMemoryBytes + byteSize);
            return handle;
        }
    }

    private static uint ResolveSubmittedDrawItemCount(RenderPacket packet)
        => packet.NativeDrawItemCount > 0
            ? checked((uint)packet.NativeDrawItemCount)
            : checked((uint)packet.DrawCommands.Count);

    private static uint ResolveSubmittedUiItemCount(RenderPacket packet)
        => packet.NativeUiDrawItemCount > 0
            ? checked((uint)packet.NativeUiDrawItemCount)
            : checked((uint)packet.UiDrawCommands.Count);

    private ulong ResolveTriangleCount(IReadOnlyList<DrawCommand> drawCommands)
    {
        ulong triangleCount = 0u;
        for (int i = 0; i < drawCommands.Count; i++)
        {
            ulong meshHandle = drawCommands[i].Mesh.Value;
            if (!_resourceHandles.TryGetValue(meshHandle, out ResourceMetadata metadata))
            {
                continue;
            }

            triangleCount = checked(triangleCount + metadata.TriangleCount);
        }

        return triangleCount;
    }

    private readonly record struct ResourceMetadata(ulong ByteSize, ulong TriangleCount);

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
