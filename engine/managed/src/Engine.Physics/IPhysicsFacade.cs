using System;
using Engine.ECS;

namespace Engine.Physics;

public interface IPhysicsFacade
{
    void SyncToPhysics(World world);

    void Step(TimeSpan deltaTime);

    void SyncFromPhysics(World world);
}
