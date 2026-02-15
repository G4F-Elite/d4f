using System;
using System.Numerics;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsOverlapQueryTests
{
    [Fact]
    public void Constructor_AssignsFields()
    {
        var query = new PhysicsOverlapQuery(
            new Vector3(5.0f, 6.0f, 7.0f),
            ColliderShapeType.Box,
            new Vector3(3.0f, 4.0f, 5.0f),
            includeTriggers: true);

        Assert.Equal(new Vector3(5.0f, 6.0f, 7.0f), query.Center);
        Assert.Equal(ColliderShapeType.Box, query.ShapeType);
        Assert.Equal(new Vector3(3.0f, 4.0f, 5.0f), query.ShapeDimensions);
        Assert.True(query.IncludeTriggers);
    }

    [Fact]
    public void Constructor_ValidatesShapeDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsOverlapQuery(
            Vector3.Zero,
            ColliderShapeType.Box,
            new Vector3(0.0f, 1.0f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsOverlapQuery(
            Vector3.Zero,
            ColliderShapeType.Sphere,
            new Vector3(1.0f, 2.0f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsOverlapQuery(
            Vector3.Zero,
            ColliderShapeType.Capsule,
            new Vector3(1.0f, 1.9f, 1.0f)));
    }
}
