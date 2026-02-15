using System;
using Engine.Core.Handles;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsOverlapHitTests
{
    [Fact]
    public void Constructor_AssignsFields()
    {
        var hit = new PhysicsOverlapHit(new BodyHandle(41), isTrigger: true);

        Assert.Equal(new BodyHandle(41), hit.Body);
        Assert.True(hit.IsTrigger);
    }

    [Fact]
    public void Constructor_ValidatesBodyHandle()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsOverlapHit(BodyHandle.Invalid, false));
    }
}
