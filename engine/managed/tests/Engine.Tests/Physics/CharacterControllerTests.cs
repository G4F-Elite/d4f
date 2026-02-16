using System;
using System.Numerics;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class CharacterControllerTests
{
    [Fact]
    public void Constructor_AssignsFields()
    {
        var controller = new CharacterController(
            radius: 0.4f,
            height: 1.8f,
            skinWidth: 0.05f,
            desiredVelocity: new Vector3(1.0f, 0.0f, 2.0f),
            isGrounded: true);

        Assert.Equal(0.4f, controller.Radius);
        Assert.Equal(1.8f, controller.Height);
        Assert.Equal(0.05f, controller.SkinWidth);
        Assert.Equal(new Vector3(1.0f, 0.0f, 2.0f), controller.DesiredVelocity);
        Assert.True(controller.IsGrounded);
    }

    [Fact]
    public void Constructor_ValidatesDimensionsAndSkinWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterController(
            radius: 0.0f,
            height: 1.8f,
            skinWidth: 0.05f,
            desiredVelocity: Vector3.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterController(
            radius: 0.5f,
            height: 1.0f,
            skinWidth: 0.05f,
            desiredVelocity: Vector3.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterController(
            radius: 0.5f,
            height: 1.8f,
            skinWidth: 0.5f,
            desiredVelocity: Vector3.Zero));
    }
}
