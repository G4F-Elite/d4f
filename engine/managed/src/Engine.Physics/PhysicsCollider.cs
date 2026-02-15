using System;
using System.Numerics;

namespace Engine.Physics;

public readonly record struct PhysicsCollider
{
    public PhysicsCollider(
        ColliderShapeType shapeType,
        Vector3 dimensions,
        bool isTrigger,
        PhysicsMaterial material)
    {
        PhysicsShapeValidation.ValidateDimensions(shapeType, dimensions, nameof(dimensions));

        ShapeType = shapeType;
        Dimensions = dimensions;
        IsTrigger = isTrigger;
        Material = material;
    }

    public ColliderShapeType ShapeType { get; }

    public Vector3 Dimensions { get; }

    public bool IsTrigger { get; }

    public PhysicsMaterial Material { get; }

    public static PhysicsCollider Default =>
        new(ColliderShapeType.Box, new Vector3(1.0f, 1.0f, 1.0f), false, PhysicsMaterial.Default);
}
