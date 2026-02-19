using System;
using System.Numerics;

namespace Engine.Physics;

public readonly record struct PhysicsRaycastQuery
{
    public PhysicsRaycastQuery(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        bool includeTriggers = false)
    {
        PhysicsShapeValidation.ValidateFiniteVector(origin, nameof(origin));
        PhysicsShapeValidation.ValidateFiniteVector(direction, nameof(direction));

        if (direction == Vector3.Zero)
        {
            throw new ArgumentException("Ray direction cannot be zero.", nameof(direction));
        }

        if (!float.IsFinite(maxDistance) || maxDistance <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistance), "Max distance must be finite and positive.");
        }

        Origin = origin;
        Direction = Vector3.Normalize(direction);
        MaxDistance = maxDistance;
        IncludeTriggers = includeTriggers;
    }

    public Vector3 Origin { get; }

    public Vector3 Direction { get; }

    public float MaxDistance { get; }

    public bool IncludeTriggers { get; }
}
