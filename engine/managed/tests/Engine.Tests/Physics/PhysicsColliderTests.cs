using System;
using System.Numerics;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsColliderTests
{
    [Fact]
    public void Constructor_AssignsFields()
    {
        var material = new PhysicsMaterial(0.3f, 0.6f);
        var collider = new PhysicsCollider(
            ColliderShapeType.Box,
            new Vector3(1.0f, 2.0f, 3.0f),
            isTrigger: true,
            material);

        Assert.Equal(ColliderShapeType.Box, collider.ShapeType);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), collider.Dimensions);
        Assert.True(collider.IsTrigger);
        Assert.Equal(material, collider.Material);
    }

    [Fact]
    public void Constructor_ValidatesDimensionsAndShapeRules()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsCollider(
            ColliderShapeType.Box,
            new Vector3(0.0f, 1.0f, 1.0f),
            false,
            PhysicsMaterial.Default));

        Assert.Throws<ArgumentException>(() => new PhysicsCollider(
            ColliderShapeType.Sphere,
            new Vector3(1.0f, 2.0f, 1.0f),
            false,
            PhysicsMaterial.Default));

        Assert.Throws<ArgumentException>(() => new PhysicsCollider(
            ColliderShapeType.Capsule,
            new Vector3(1.0f, 1.9f, 1.0f),
            false,
            PhysicsMaterial.Default));
    }

    [Fact]
    public void Default_ReturnsValidBoxCollider()
    {
        PhysicsCollider collider = PhysicsCollider.Default;

        Assert.Equal(ColliderShapeType.Box, collider.ShapeType);
        Assert.Equal(new Vector3(1.0f, 1.0f, 1.0f), collider.Dimensions);
        Assert.False(collider.IsTrigger);
        Assert.Equal(PhysicsMaterial.Default, collider.Material);
    }
}
