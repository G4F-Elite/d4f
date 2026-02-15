using Engine.Core.Timing;

namespace Engine.ECS;

public interface IWorldSystem
{
    void Update(World world, in FrameTiming timing);
}
