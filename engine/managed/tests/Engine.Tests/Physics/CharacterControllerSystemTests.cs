using System;
using System.Numerics;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class CharacterControllerSystemTests
{
    [Fact]
    public void Update_MovesBodyByDesiredVelocityWhenSweepDoesNotHit()
    {
        var physics = new TestPhysicsFacade();
        var system = new CharacterControllerSystem(physics);
        var world = new World();
        EntityId entity = world.CreateEntity();
        world.AddComponent(entity, CreateBody(PhysicsBodyType.Kinematic, position: Vector3.Zero));
        world.AddComponent(entity, new CharacterController(0.5f, 1.8f, 0.05f, new Vector3(4.0f, 0.0f, 0.0f)));

        system.Update(world, new FrameTiming(0, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.5)));

        Assert.True(world.TryGetComponent(entity, out PhysicsBody body));
        Assert.Equal(new Vector3(2.0f, 0.0f, 0.0f), body.Position);
        Assert.Equal(new Vector3(4.0f, 0.0f, 0.0f), body.LinearVelocity);

        Assert.True(world.TryGetComponent(entity, out CharacterController controller));
        Assert.False(controller.IsGrounded);
        Assert.Single(physics.SweepQueries);
    }

    [Fact]
    public void Update_ResolvesMovementAgainstSweepHitAndMarksGrounded()
    {
        var physics = new TestPhysicsFacade();
        physics.SweepResults.Enqueue((
            true,
            new PhysicsSweepHit(
                new BodyHandle(99),
                distance: 1.5f,
                point: new Vector3(1.5f, 0.0f, 0.0f),
                normal: Vector3.UnitY,
                isTrigger: false)));
        var system = new CharacterControllerSystem(physics);
        var world = new World();
        EntityId entity = world.CreateEntity();
        world.AddComponent(entity, CreateBody(PhysicsBodyType.Dynamic, position: Vector3.Zero));
        world.AddComponent(entity, new CharacterController(0.5f, 1.8f, 0.1f, new Vector3(4.0f, 0.0f, 0.0f)));

        system.Update(world, new FrameTiming(0, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.5)));

        Assert.True(world.TryGetComponent(entity, out PhysicsBody body));
        Assert.Equal(1.4f, body.Position.X, 3);
        Assert.Equal(2.8f, body.LinearVelocity.X, 3);

        Assert.True(world.TryGetComponent(entity, out CharacterController controller));
        Assert.True(controller.IsGrounded);

        Assert.Single(physics.SweepQueries);
        PhysicsSweepQuery sweepQuery = physics.SweepQueries[0];
        Assert.Equal(ColliderShapeType.Capsule, sweepQuery.ShapeType);
        Assert.Equal(new Vector3(0.5f, 1.8f, 0.5f), sweepQuery.ShapeDimensions);
    }

    [Fact]
    public void Update_SkipsStaticBodies()
    {
        var physics = new TestPhysicsFacade();
        var system = new CharacterControllerSystem(physics);
        var world = new World();
        EntityId entity = world.CreateEntity();
        world.AddComponent(entity, CreateBody(PhysicsBodyType.Static, position: Vector3.Zero));
        world.AddComponent(entity, new CharacterController(0.5f, 1.8f, 0.05f, new Vector3(10.0f, 0.0f, 0.0f)));

        system.Update(world, new FrameTiming(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

        Assert.True(world.TryGetComponent(entity, out PhysicsBody body));
        Assert.Equal(Vector3.Zero, body.Position);
        Assert.Empty(physics.SweepQueries);
    }

    private static PhysicsBody CreateBody(PhysicsBodyType type, Vector3 position)
    {
        return new PhysicsBody(
            new BodyHandle(1),
            type,
            new PhysicsCollider(ColliderShapeType.Capsule, new Vector3(0.5f, 2.0f, 0.5f), false, PhysicsMaterial.Default),
            position,
            Quaternion.Identity,
            Vector3.Zero,
            Vector3.Zero,
            isActive: true);
    }
}
