using Engine.Core.Timing;
using Engine.ECS;

namespace Engine.Rendering;

public interface IRenderPacketBuilder
{
    RenderPacket Build(World world, in FrameTiming timing, FrameArena frameArena);
}
