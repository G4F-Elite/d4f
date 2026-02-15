using System;
using Engine.Core.Handles;

namespace Engine.Physics;

public readonly record struct PhysicsOverlapHit
{
    public PhysicsOverlapHit(BodyHandle body, bool isTrigger)
    {
        if (!body.IsValid)
        {
            throw new ArgumentException("Body handle must be valid.", nameof(body));
        }

        Body = body;
        IsTrigger = isTrigger;
    }

    public BodyHandle Body { get; }

    public bool IsTrigger { get; }
}
