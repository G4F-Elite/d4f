namespace Engine.Rendering;

public interface IRenderingFacade
{
    FrameArena BeginFrame(int requestedBytes, int alignment);

    void Submit(RenderPacket packet);

    void Present();

    RenderingFrameStats GetLastFrameStats();

    byte[] CaptureFrameRgba8(uint width, uint height, bool includeAlpha = true);
}
