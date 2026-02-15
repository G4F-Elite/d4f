using Engine.Core.Timing;
using Engine.ECS;

namespace Engine.UI;

public interface IUiFacade
{
    void Update(World world, in FrameTiming timing);
}
