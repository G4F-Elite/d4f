using System.Numerics;

namespace Engine.Physics;

public readonly record struct PhysicsOverlapQuery
{
    public PhysicsOverlapQuery(
        Vector3 center,
        ColliderShapeType shapeType,
        Vector3 shapeDimensions,
        bool includeTriggers = false)
    {
        PhysicsShapeValidation.ValidateFiniteVector(center, nameof(center));
        PhysicsShapeValidation.ValidateDimensions(shapeType, shapeDimensions, nameof(shapeDimensions));

        Center = center;
        ShapeType = shapeType;
        ShapeDimensions = shapeDimensions;
        IncludeTriggers = includeTriggers;
    }

    public Vector3 Center { get; }

    public ColliderShapeType ShapeType { get; }

    public Vector3 ShapeDimensions { get; }

    public bool IncludeTriggers { get; }
}
