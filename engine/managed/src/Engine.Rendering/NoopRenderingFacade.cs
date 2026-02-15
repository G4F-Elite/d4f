using System;

namespace Engine.Rendering;

public sealed class NoopRenderingFacade : IRenderingFacade
{
    public static NoopRenderingFacade Instance { get; } = new();

    private NoopRenderingFacade()
    {
    }

    public void Submit(RenderPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
    }

    public void Present()
    {
    }
}
