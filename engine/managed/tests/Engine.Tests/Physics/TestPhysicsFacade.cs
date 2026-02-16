using System;
using System.Collections.Generic;
using Engine.ECS;
using Engine.Physics;

namespace Engine.Tests.Physics;

internal sealed class TestPhysicsFacade : IPhysicsFacade
{
    public Queue<(bool HasHit, PhysicsSweepHit Hit)> SweepResults { get; } = new();

    public Queue<PhysicsOverlapHit[]> OverlapResults { get; } = new();

    public List<PhysicsSweepQuery> SweepQueries { get; } = [];

    public List<PhysicsOverlapQuery> OverlapQueries { get; } = [];

    public void SyncToPhysics(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
    }

    public void Step(TimeSpan deltaTime)
    {
        if (deltaTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
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
        SweepQueries.Add(query);
        if (SweepResults.Count == 0)
        {
            hit = default;
            return false;
        }

        (bool hasHit, PhysicsSweepHit resolvedHit) = SweepResults.Dequeue();
        hit = resolvedHit;
        return hasHit;
    }

    public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
    {
        OverlapQueries.Add(query);
        if (OverlapResults.Count == 0)
        {
            return 0;
        }

        PhysicsOverlapHit[] result = OverlapResults.Dequeue();
        int hitCount = Math.Min(result.Length, hits.Length);
        result.AsSpan(0, hitCount).CopyTo(hits);
        return hitCount;
    }
}
