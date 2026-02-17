using System.Runtime.InteropServices;
using Engine.Core.Handles;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    private delegate EngineNativeStatus CreateResourceFromBlobDelegate(
        IntPtr subsystem,
        IntPtr data,
        nuint size,
        out ulong handle);

    public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
    {
        ulong handle = CreateResourceFromBlob(
            blob,
            _renderer,
            _interop.RendererCreateMeshFromBlob,
            "renderer_create_mesh_from_blob");
        return new MeshHandle(handle);
    }

    public MeshHandle CreateMeshFromCpu(ReadOnlySpan<float> positions, ReadOnlySpan<uint> indices)
    {
        ThrowIfDisposed();
        ValidateMeshCpuData(positions, indices);

        float[] positionsPayload = positions.ToArray();
        uint[] indicesPayload = indices.ToArray();
        GCHandle pinnedPositions = GCHandle.Alloc(positionsPayload, GCHandleType.Pinned);
        GCHandle pinnedIndices = GCHandle.Alloc(indicesPayload, GCHandleType.Pinned);
        try
        {
            var meshCpuData = new EngineNativeMeshCpuData
            {
                Positions = pinnedPositions.AddrOfPinnedObject(),
                VertexCount = checked((uint)(positionsPayload.Length / 3)),
                Indices = pinnedIndices.AddrOfPinnedObject(),
                IndexCount = checked((uint)indicesPayload.Length)
            };

            NativeStatusGuard.ThrowIfFailed(
                _interop.RendererCreateMeshFromCpu(_renderer, in meshCpuData, out ulong handle),
                "renderer_create_mesh_from_cpu");
            return new MeshHandle(handle);
        }
        finally
        {
            pinnedPositions.Free();
            pinnedIndices.Free();
        }
    }

    public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
    {
        ulong handle = CreateResourceFromBlob(
            blob,
            _renderer,
            _interop.RendererCreateTextureFromBlob,
            "renderer_create_texture_from_blob");
        return new TextureHandle(handle);
    }

    public TextureHandle CreateTextureFromCpu(
        uint width,
        uint height,
        ReadOnlySpan<byte> rgba8,
        uint strideBytes = 0)
    {
        ThrowIfDisposed();
        uint stride = ValidateTextureCpuData(width, height, rgba8, strideBytes);

        byte[] payload = rgba8.ToArray();
        GCHandle pinnedPayload = GCHandle.Alloc(payload, GCHandleType.Pinned);
        try
        {
            var textureCpuData = new EngineNativeTextureCpuData
            {
                Rgba8 = pinnedPayload.AddrOfPinnedObject(),
                Width = width,
                Height = height,
                Stride = stride
            };

            NativeStatusGuard.ThrowIfFailed(
                _interop.RendererCreateTextureFromCpu(_renderer, in textureCpuData, out ulong handle),
                "renderer_create_texture_from_cpu");
            return new TextureHandle(handle);
        }
        finally
        {
            pinnedPayload.Free();
        }
    }

    public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
    {
        ulong handle = CreateResourceFromBlob(
            blob,
            _renderer,
            _interop.RendererCreateMaterialFromBlob,
            "renderer_create_material_from_blob");
        return new MaterialHandle(handle);
    }

    public void DestroyResource(ulong handle)
    {
        ThrowIfDisposed();
        if (handle == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle value must be non-zero.");
        }
        NativeStatusGuard.ThrowIfFailed(
            _interop.RendererDestroyResource(_renderer, handle),
            "renderer_destroy_resource");
    }

    private ulong CreateResourceFromBlob(
        ReadOnlySpan<byte> blob,
        IntPtr subsystem,
        CreateResourceFromBlobDelegate createDelegate,
        string callName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(createDelegate);
        ArgumentException.ThrowIfNullOrWhiteSpace(callName);
        if (blob.Length == 0)
        {
            throw new ArgumentException("Blob payload must be non-empty.", nameof(blob));
        }

        byte[] payload = blob.ToArray();
        GCHandle pinned = GCHandle.Alloc(payload, GCHandleType.Pinned);
        try
        {
            IntPtr payloadPtr = pinned.AddrOfPinnedObject();
            NativeStatusGuard.ThrowIfFailed(
                createDelegate(subsystem, payloadPtr, checked((nuint)payload.Length), out ulong handle),
                callName);
            return handle;
        }
        finally
        {
            pinned.Free();
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

    private static uint ValidateTextureCpuData(
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

        return stride;
    }
}
