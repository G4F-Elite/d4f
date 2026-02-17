using Engine.Core.Handles;

namespace Engine.Rendering;

public interface IRenderingFacade
{
    FrameArena BeginFrame(int requestedBytes, int alignment);

    void Submit(RenderPacket packet);

    void Present();

    RenderingFrameStats GetLastFrameStats();

    MeshHandle CreateMeshFromBlob(ReadOnlySpan<byte> blob);

    TextureHandle CreateTextureFromBlob(ReadOnlySpan<byte> blob);

    MaterialHandle CreateMaterialFromBlob(ReadOnlySpan<byte> blob);

    void DestroyResource(ulong handle);

    byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true);
}
