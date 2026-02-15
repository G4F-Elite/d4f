using System;
using System.Numerics;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsRaycastQueryTests
{
    [Fact]
    public void Constructor_NormalizesDirectionAndAssignsFields()
    {
        var query = new PhysicsRaycastQuery(
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            42.0f,
            includeTriggers: true);

        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), query.Origin);
        Assert.Equal(Vector3.UnitX, query.Direction);
        Assert.Equal(42.0f, query.MaxDistance);
        Assert.True(query.IncludeTriggers);
    }

    [Fact]
    public void Constructor_ValidatesInputs()
    {
        Assert.Throws<ArgumentException>(() => new PhysicsRaycastQuery(Vector3.Zero, Vector3.Zero, 1.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsRaycastQuery(Vector3.Zero, Vector3.UnitX, 0.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsRaycastQuery(Vector3.Zero, Vector3.UnitX, float.NaN));
    }
}
