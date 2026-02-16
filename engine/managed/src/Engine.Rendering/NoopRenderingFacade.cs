using System;

namespace Engine.Rendering;

public sealed class NoopRenderingFacade : IRenderingFacade
{
    public static NoopRenderingFacade Instance { get; } = new();

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
}
