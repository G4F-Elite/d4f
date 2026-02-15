using System;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsMaterialTests
{
    [Fact]
    public void Constructor_AssignsFields()
    {
        var material = new PhysicsMaterial(0.2f, 0.7f);

        Assert.Equal(0.2f, material.Friction);
        Assert.Equal(0.7f, material.Restitution);
    }

    [Fact]
    public void Constructor_ValidatesRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsMaterial(-0.1f, 0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsMaterial(0.5f, 1.1f));
    }

    [Fact]
    public void Default_IsStable()
    {
        var material = PhysicsMaterial.Default;

        Assert.Equal(0.5f, material.Friction);
        Assert.Equal(0.1f, material.Restitution);
    }
}
