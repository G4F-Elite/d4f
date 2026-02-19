using System;
using System.Numerics;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsSweepQueryTests
{
    [Fact]
    public void Constructor_NormalizesDirectionAndAssignsFields()
    {
        var query = new PhysicsSweepQuery(
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(0.0f, 0.0f, 2.0f),
            16.0f,
            ColliderShapeType.Capsule,
            new Vector3(0.5f, 2.0f, 0.5f),
            includeTriggers: true);

        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), query.Origin);
        Assert.Equal(Vector3.UnitZ, query.Direction);
        Assert.Equal(16.0f, query.MaxDistance);
        Assert.Equal(ColliderShapeType.Capsule, query.ShapeType);
        Assert.Equal(new Vector3(0.5f, 2.0f, 0.5f), query.ShapeDimensions);
        Assert.True(query.IncludeTriggers);
    }

    [Fact]
    public void Constructor_ValidatesDirectionDistanceAndShapeDimensions()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            Vector3.Zero,
            1.0f,
            ColliderShapeType.Box,
            new Vector3(1.0f, 1.0f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsSweepQuery(
            new Vector3(float.NaN, 0.0f, 0.0f),
            Vector3.UnitX,
            1.0f,
            ColliderShapeType.Box,
            new Vector3(1.0f, 1.0f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            new Vector3(float.NaN, 0.0f, 0.0f),
            1.0f,
            ColliderShapeType.Box,
            new Vector3(1.0f, 1.0f, 1.0f)));

        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            Vector3.UnitX,
            0.0f,
            ColliderShapeType.Box,
            new Vector3(1.0f, 1.0f, 1.0f)));

        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            Vector3.UnitX,
            float.NaN,
            ColliderShapeType.Box,
            new Vector3(1.0f, 1.0f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            Vector3.UnitX,
            1.0f,
            ColliderShapeType.Sphere,
            new Vector3(1.0f, 2.0f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            Vector3.UnitX,
            1.0f,
            ColliderShapeType.Capsule,
            new Vector3(1.0f, 1.9f, 1.0f)));

        Assert.Throws<ArgumentException>(() => new PhysicsSweepQuery(
            Vector3.Zero,
            Vector3.UnitX,
            1.0f,
            ColliderShapeType.Box,
            new Vector3(float.NaN, 1.0f, 1.0f)));
    }
}
