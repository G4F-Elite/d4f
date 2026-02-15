using System;
using System.Numerics;
using Engine.Core.Handles;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsRaycastHitTests
{
    [Fact]
    public void Constructor_AssignsAndNormalizesNormal()
    {
        var hit = new PhysicsRaycastHit(
            new BodyHandle(99),
            3.5f,
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(0.0f, 2.0f, 0.0f),
            isTrigger: false);

        Assert.Equal(new BodyHandle(99), hit.Body);
        Assert.Equal(3.5f, hit.Distance);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), hit.Point);
        Assert.Equal(Vector3.UnitY, hit.Normal);
        Assert.False(hit.IsTrigger);
    }

    [Fact]
    public void Constructor_ValidatesInputs()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsRaycastHit(
            BodyHandle.Invalid,
            1.0f,
            Vector3.Zero,
            Vector3.UnitX,
            false));

        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsRaycastHit(
            new BodyHandle(1),
            -1.0f,
            Vector3.Zero,
            Vector3.UnitX,
            false));

        Assert.Throws<ArgumentException>(() => new PhysicsRaycastHit(
            new BodyHandle(1),
            1.0f,
            Vector3.Zero,
            Vector3.Zero,
            false));
    }
}
