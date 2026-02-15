using System;
using Engine.ECS;

namespace Engine.Physics;

public sealed class NoopPhysicsFacade : IPhysicsFacade
{
    public static NoopPhysicsFacade Instance { get; } = new();

    private NoopPhysicsFacade()
    {
    }

    public void SyncToPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
    }

    public void Step(TimeSpan deltaTime)
    {
        if (deltaTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }
    }

    public void SyncFromPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
    }

    public bool Raycast(in PhysicsRaycastQuery query, out PhysicsRaycastHit hit)
    {
        hit = default;
        return false;
    }

    public bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit)
    {
        hit = default;
        return false;
    }

    public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
    {
        return 0;
    }
}
