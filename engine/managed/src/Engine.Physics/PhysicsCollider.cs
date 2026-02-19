using System;
using System.Numerics;
using Engine.Core.Handles;

namespace Engine.Physics;

public readonly record struct PhysicsCollider
{
    public PhysicsCollider(
        ColliderShapeType shapeType,
        Vector3 dimensions,
        bool isTrigger,
        PhysicsMaterial material)
        : this(shapeType, dimensions, isTrigger, material, MeshHandle.Invalid)
    {
    }

    public PhysicsCollider(
        ColliderShapeType shapeType,
        Vector3 dimensions,
        bool isTrigger,
        PhysicsMaterial material,
        MeshHandle staticMeshHandle)
    {
        PhysicsShapeValidation.ValidateDimensions(shapeType, dimensions, nameof(dimensions));
        PhysicsShapeValidation.ValidateStaticMeshBinding(shapeType, staticMeshHandle, nameof(staticMeshHandle));

        ShapeType = shapeType;
        Dimensions = dimensions;
        IsTrigger = isTrigger;
        Material = material;
        StaticMeshHandle = staticMeshHandle;
    }

    public ColliderShapeType ShapeType { get; }

    public Vector3 Dimensions { get; }

    public bool IsTrigger { get; }

    public PhysicsMaterial Material { get; }

    public MeshHandle StaticMeshHandle { get; }

    public static PhysicsCollider Default =>
        new(ColliderShapeType.Box, new Vector3(1.0f, 1.0f, 1.0f), false, PhysicsMaterial.Default);
}
