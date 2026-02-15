using System;
using System.Numerics;
using Engine.Core.Handles;

namespace Engine.Physics;

public readonly record struct PhysicsSweepHit
{
    public PhysicsSweepHit(
        BodyHandle body,
        float distance,
        Vector3 point,
        Vector3 normal,
        bool isTrigger)
    {
        if (!body.IsValid)
        {
            throw new ArgumentException("Body handle must be valid.", nameof(body));
        }

        if (!float.IsFinite(distance) || distance < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), "Distance must be finite and non-negative.");
        }

        if (normal == Vector3.Zero)
        {
            throw new ArgumentException("Normal cannot be zero.", nameof(normal));
        }

        Body = body;
        Distance = distance;
        Point = point;
        Normal = Vector3.Normalize(normal);
        IsTrigger = isTrigger;
    }

    public BodyHandle Body { get; }

    public float Distance { get; }

    public Vector3 Point { get; }

    public Vector3 Normal { get; }

    public bool IsTrigger { get; }
}
