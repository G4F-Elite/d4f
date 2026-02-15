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
        ValidateDimensions(shapeType, dimensions);

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

    private static void ValidateDimensions(ColliderShapeType shapeType, Vector3 dimensions)
    {
        if (dimensions.X <= 0.0f || dimensions.Y <= 0.0f || dimensions.Z <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Collider dimensions must be positive.");
        }

        switch (shapeType)
        {
            case ColliderShapeType.Sphere when dimensions.X != dimensions.Y || dimensions.Y != dimensions.Z:
                throw new ArgumentException("Sphere dimensions must be uniform (X == Y == Z).", nameof(dimensions));
            case ColliderShapeType.Capsule when dimensions.Y <= dimensions.X * 2.0f:
                throw new ArgumentException("Capsule height (Y) must be greater than diameter (2*X).", nameof(dimensions));
        }
    }
}
