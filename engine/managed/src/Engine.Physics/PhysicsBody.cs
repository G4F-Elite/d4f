using System;
using System.Numerics;
using Engine.Core.Handles;

namespace Engine.Physics;

public struct PhysicsBody
{
    public PhysicsBody(
        BodyHandle body,
        Vector3 position,
        Quaternion rotation,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        bool isActive = true)
        : this(
            body,
            PhysicsBodyType.Dynamic,
            PhysicsCollider.Default,
            position,
            rotation,
            linearVelocity,
            angularVelocity,
            isActive)
    {
    }

    public PhysicsBody(
        BodyHandle body,
        PhysicsBodyType bodyType,
        PhysicsCollider collider,
        Vector3 position,
        Quaternion rotation,
        Vector3 linearVelocity,
        Vector3 angularVelocity,
        bool isActive = true)
    {
        if (!body.IsValid)
        {
            throw new ArgumentException("Body handle must be valid.", nameof(body));
        }

        if (!Enum.IsDefined(typeof(PhysicsBodyType), bodyType))
        {
            throw new ArgumentOutOfRangeException(nameof(bodyType), "Body type is not supported.");
        }

        PhysicsShapeValidation.ValidateFiniteVector(position, nameof(position));
        PhysicsShapeValidation.ValidateFiniteQuaternion(rotation, nameof(rotation));
        PhysicsShapeValidation.ValidateFiniteVector(linearVelocity, nameof(linearVelocity));
        PhysicsShapeValidation.ValidateFiniteVector(angularVelocity, nameof(angularVelocity));

        Body = body;
        BodyType = bodyType;
        Collider = collider;
        Position = position;
        Rotation = rotation;
        LinearVelocity = linearVelocity;
        AngularVelocity = angularVelocity;
        IsActive = isActive;
    }

    public BodyHandle Body;

    public PhysicsBodyType BodyType;

    public PhysicsCollider Collider;

    public Vector3 Position;

    public Quaternion Rotation;

    public Vector3 LinearVelocity;

    public Vector3 AngularVelocity;

    public bool IsActive;
}
