using System;

namespace Engine.Physics;

public readonly record struct PhysicsMaterial
{
    public static PhysicsMaterial Default => new(0.5f, 0.1f);

    public PhysicsMaterial(float friction, float restitution)
    {
        if (friction is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(friction), "Friction must be in [0, 1].");
        }

        if (restitution is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(restitution), "Restitution must be in [0, 1].");
        }

        Friction = friction;
        Restitution = restitution;
    }

    public float Friction { get; }

    public float Restitution { get; }
}
