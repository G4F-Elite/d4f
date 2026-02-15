using System;
using Engine.Core.Timing;
using Engine.ECS;

namespace Engine.Rendering;

public sealed class DefaultRenderPacketBuilder : IRenderPacketBuilder
{
    public static DefaultRenderPacketBuilder Instance { get; } = new();

    private DefaultRenderPacketBuilder()
    {
    }

    public RenderPacket Build(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);
        return RenderPacket.Empty(timing.FrameNumber);
    }
}
