using System;
using System.Numerics;
using Engine.Core.Handles;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsSweepHitTests
{
    [Fact]
    public void Constructor_AssignsAndNormalizesNormal()
    {
        var hit = new PhysicsSweepHit(
            new BodyHandle(17),
            4.0f,
            new Vector3(2.0f, 3.0f, 4.0f),
            new Vector3(0.0f, 3.0f, 0.0f),
            isTrigger: true);

        Assert.Equal(new BodyHandle(17), hit.Body);
        Assert.Equal(4.0f, hit.Distance);
        Assert.Equal(new Vector3(2.0f, 3.0f, 4.0f), hit.Point);
        Assert.Equal(Vector3.UnitY, hit.Normal);
        Assert.True(hit.IsTrigger);
    }

    [Fact]
    public void Constructor_ValidatesInputs()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsSweepHit(
            BodyHandle.Invalid,
            1.0f,
            Vector3.Zero,
            Vector3.UnitX,
            false));

        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsSweepHit(
            new BodyHandle(1),
            -1.0f,
            Vector3.Zero,
            Vector3.UnitX,
            false));

        Assert.Throws<ArgumentException>(() => new PhysicsSweepHit(
            new BodyHandle(1),
            1.0f,
            Vector3.Zero,
            Vector3.Zero,
            false));
    }
}
