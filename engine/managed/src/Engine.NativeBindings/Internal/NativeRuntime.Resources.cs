using System.Runtime.InteropServices;
using Engine.Core.Handles;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    private delegate EngineNativeStatus RendererCreateFromBlobDelegate(
        IntPtr renderer,
        IntPtr data,
        nuint size,
        out ulong handle);

    public MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob)
    {
        ulong handle = CreateResourceFromBlob(
            blob,
            _interop.RendererCreateMeshFromBlob,
            "renderer_create_mesh_from_blob");
        return new MeshHandle(handle);
    }

    public TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob)
    {
        ulong handle = CreateResourceFromBlob(
            blob,
            _interop.RendererCreateTextureFromBlob,
            "renderer_create_texture_from_blob");
        return new TextureHandle(handle);
    }

    public MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob)
    {
        ulong handle = CreateResourceFromBlob(
            blob,
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
        RendererCreateFromBlobDelegate createDelegate,
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
                createDelegate(_renderer, payloadPtr, checked((nuint)payload.Length), out ulong handle),
                callName);
            return handle;
        }
        finally
        {
            pinned.Free();
        }
    }
}
