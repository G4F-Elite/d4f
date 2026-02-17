using Engine.Core.Handles;

namespace Engine.Rendering;

public interface IRenderingFacade
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
