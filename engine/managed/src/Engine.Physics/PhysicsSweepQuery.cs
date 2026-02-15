using System;
using System.Numerics;

namespace Engine.Physics;

public readonly record struct PhysicsSweepQuery
{
    public PhysicsSweepQuery(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        ColliderShapeType shapeType,
        Vector3 shapeDimensions,
        bool includeTriggers = false)
    {
        if (direction == Vector3.Zero)
        {
            throw new ArgumentException("Sweep direction cannot be zero.", nameof(direction));
        }

        if (!float.IsFinite(maxDistance) || maxDistance <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistance), "Max distance must be finite and positive.");
        }

        PhysicsShapeValidation.ValidateDimensions(shapeType, shapeDimensions, nameof(shapeDimensions));

        Origin = origin;
        Direction = Vector3.Normalize(direction);
        MaxDistance = maxDistance;
        ShapeType = shapeType;
        ShapeDimensions = shapeDimensions;
        IncludeTriggers = includeTriggers;
    }

    public Vector3 Origin { get; }

    public Vector3 Direction { get; }

    public float MaxDistance { get; }

    public ColliderShapeType ShapeType { get; }

    public Vector3 ShapeDimensions { get; }

    public bool IncludeTriggers { get; }
}
