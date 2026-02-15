using Engine.ECS;

namespace Engine.Scenes;

public abstract class Scene
{
    public abstract void OnCreate(World world);

    public virtual void OnDestroy(World world)
    {
    }
}
