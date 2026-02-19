using System;
using System.Numerics;
using Engine.Core.Handles;

namespace Engine.Physics;

internal static class PhysicsShapeValidation
{
    public static void ValidateFiniteVector(Vector3 value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentException("Vector components must be finite.", paramName);
        }
    }

    public static void ValidateFiniteQuaternion(Quaternion value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
            !float.IsFinite(value.Z) || !float.IsFinite(value.W))
        {
            throw new ArgumentException("Quaternion components must be finite.", paramName);
        }

        float lengthSquared = value.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.0f)
        {
            throw new ArgumentException("Quaternion length must be positive and finite.", paramName);
        }
    }

    public static void ValidateDimensions(ColliderShapeType shapeType, Vector3 dimensions, string paramName)
    {
        ValidateFiniteVector(dimensions, paramName);

        if (dimensions.X <= 0.0f || dimensions.Y <= 0.0f || dimensions.Z <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(paramName, "Collider dimensions must be positive.");
        }

        switch (shapeType)
        {
            case ColliderShapeType.Sphere when dimensions.X != dimensions.Y || dimensions.Y != dimensions.Z:
                throw new ArgumentException("Sphere dimensions must be uniform (X == Y == Z).", paramName);
            case ColliderShapeType.Capsule when dimensions.Y <= dimensions.X * 2.0f:
                throw new ArgumentException("Capsule height (Y) must be greater than diameter (2*X).", paramName);
        }
    }

    public static void ValidateStaticMeshBinding(
        ColliderShapeType shapeType,
        MeshHandle staticMeshHandle,
        string paramName)
    {
        if (shapeType == ColliderShapeType.StaticMesh)
        {
            if (!staticMeshHandle.IsValid)
            {
                throw new ArgumentException("Static mesh collider requires a valid mesh handle.", paramName);
            }

            return;
        }

        if (staticMeshHandle.IsValid)
        {
            throw new ArgumentException("Static mesh handle is only supported for StaticMesh collider type.", paramName);
        }
    }
}
