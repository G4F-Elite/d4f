using System;
using Engine.Core.Handles;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsContactEventTests
{
    [Fact]
    public void Constructor_AssignsFields()
    {
        var contactEvent = new PhysicsContactEvent(
            new BodyHandle(10),
            new BodyHandle(20),
            PhysicsContactEventType.Enter,
            otherIsTrigger: true);

        Assert.Equal(new BodyHandle(10), contactEvent.TriggerBody);
        Assert.Equal(new BodyHandle(20), contactEvent.OtherBody);
        Assert.Equal(PhysicsContactEventType.Enter, contactEvent.EventType);
        Assert.True(contactEvent.OtherIsTrigger);
    }

    [Fact]
    public void Constructor_ValidatesBodyHandles()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsContactEvent(
            BodyHandle.Invalid,
            new BodyHandle(1),
            PhysicsContactEventType.Enter,
            false));

        Assert.Throws<ArgumentException>(() => new PhysicsContactEvent(
            new BodyHandle(1),
            BodyHandle.Invalid,
            PhysicsContactEventType.Enter,
            false));
    }
}
