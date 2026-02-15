using System;
using System.Numerics;

namespace Engine.Physics;

internal static class PhysicsShapeValidation
{
    public static void ValidateDimensions(ColliderShapeType shapeType, Vector3 dimensions, string paramName)
    {
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
}
