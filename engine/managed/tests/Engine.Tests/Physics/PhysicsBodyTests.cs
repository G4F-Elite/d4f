using System;
using System.Numerics;
using Engine.Core.Handles;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsBodyTests
{
    [Fact]
    public void Constructor_ValidatesBodyHandle()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsBody(
            BodyHandle.Invalid,
            Vector3.Zero,
            Quaternion.Identity,
            Vector3.Zero,
            Vector3.Zero));
    }

    [Fact]
    public void Constructor_AssignsAllFields()
    {
        var collider = new PhysicsCollider(
            ColliderShapeType.Box,
            new Vector3(0.5f, 1.0f, 0.5f),
            isTrigger: true,
            new PhysicsMaterial(0.8f, 0.2f));

        var body = new PhysicsBody(
            new BodyHandle(42),
            PhysicsBodyType.Kinematic,
            collider,
            new Vector3(1, 2, 3),
            new Quaternion(0, 0, 0, 1),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9),
            isActive: false);

        Assert.Equal((uint)42, body.Body.Value);
        Assert.Equal(PhysicsBodyType.Kinematic, body.BodyType);
        Assert.Equal(collider, body.Collider);
        Assert.Equal(new Vector3(1, 2, 3), body.Position);
        Assert.Equal(new Quaternion(0, 0, 0, 1), body.Rotation);
        Assert.Equal(new Vector3(4, 5, 6), body.LinearVelocity);
        Assert.Equal(new Vector3(7, 8, 9), body.AngularVelocity);
        Assert.False(body.IsActive);
    }

    [Fact]
    public void LegacyConstructor_AssignsDefaultBodyTypeAndCollider()
    {
        var body = new PhysicsBody(
            new BodyHandle(7),
            Vector3.Zero,
            Quaternion.Identity,
            Vector3.Zero,
            Vector3.Zero);

        Assert.Equal(PhysicsBodyType.Dynamic, body.BodyType);
        Assert.Equal(PhysicsCollider.Default, body.Collider);
    }
}
