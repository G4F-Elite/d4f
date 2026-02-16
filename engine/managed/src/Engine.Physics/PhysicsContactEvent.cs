using System;
using Engine.Core.Handles;

namespace Engine.Physics;

public readonly record struct PhysicsContactEvent
{
    public PhysicsContactEvent(
        BodyHandle triggerBody,
        BodyHandle otherBody,
        PhysicsContactEventType eventType,
        bool otherIsTrigger)
    {
        if (!triggerBody.IsValid)
        {
            throw new ArgumentException("Trigger body handle must be valid.", nameof(triggerBody));
        }

        if (!otherBody.IsValid)
        {
            throw new ArgumentException("Other body handle must be valid.", nameof(otherBody));
        }

        TriggerBody = triggerBody;
        OtherBody = otherBody;
        EventType = eventType;
        OtherIsTrigger = otherIsTrigger;
    }

    public BodyHandle TriggerBody { get; }

    public BodyHandle OtherBody { get; }

    public PhysicsContactEventType EventType { get; }

    public bool OtherIsTrigger { get; }
}
