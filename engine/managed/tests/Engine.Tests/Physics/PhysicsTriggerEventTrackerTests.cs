using System;
using System.Numerics;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Physics;
using Xunit;

namespace Engine.Tests.Physics;

public sealed class PhysicsTriggerEventTrackerTests
{
    [Fact]
    public void Update_EmitsEnterStayExitEventsAcrossFrames()
    {
        var physics = new TestPhysicsFacade();
        var tracker = new PhysicsTriggerEventTracker(physics);
        var world = new World();
        EntityId triggerEntity = world.CreateEntity();
        EntityId otherEntity = world.CreateEntity();

        world.AddComponent(triggerEntity, CreateBody(new BodyHandle(101), isTrigger: true));
        world.AddComponent(otherEntity, CreateBody(new BodyHandle(202), isTrigger: false));

        physics.OverlapResults.Enqueue([
            new PhysicsOverlapHit(new BodyHandle(202), isTrigger: false)
        ]);
        tracker.Update(world, new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));
        Assert.Collection(
            tracker.Events,
            contact => Assert.Equal(PhysicsContactEventType.Enter, contact.EventType));

        physics.OverlapResults.Enqueue([
            new PhysicsOverlapHit(new BodyHandle(202), isTrigger: false)
        ]);
        tracker.Update(world, new FrameTiming(1, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(32)));
        Assert.Collection(
            tracker.Events,
            contact => Assert.Equal(PhysicsContactEventType.Stay, contact.EventType));

        physics.OverlapResults.Enqueue(Array.Empty<PhysicsOverlapHit>());
        tracker.Update(world, new FrameTiming(2, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(48)));
        Assert.Collection(
            tracker.Events,
            contact => Assert.Equal(PhysicsContactEventType.Exit, contact.EventType));
    }

    [Fact]
    public void Update_IgnoresSelfAndDuplicateOverlapHits()
    {
        var physics = new TestPhysicsFacade();
        var tracker = new PhysicsTriggerEventTracker(physics);
        var world = new World();
        EntityId triggerEntity = world.CreateEntity();
        EntityId otherEntity = world.CreateEntity();

        world.AddComponent(triggerEntity, CreateBody(new BodyHandle(101), isTrigger: true));
        world.AddComponent(otherEntity, CreateBody(new BodyHandle(202), isTrigger: false));

        physics.OverlapResults.Enqueue([
            new PhysicsOverlapHit(new BodyHandle(101), isTrigger: true),
            new PhysicsOverlapHit(new BodyHandle(202), isTrigger: false),
            new PhysicsOverlapHit(new BodyHandle(202), isTrigger: false)
        ]);

        tracker.Update(world, new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));

        Assert.Single(tracker.Events);
        Assert.Equal(new BodyHandle(101), tracker.Events[0].TriggerBody);
        Assert.Equal(new BodyHandle(202), tracker.Events[0].OtherBody);
        Assert.Equal(PhysicsContactEventType.Enter, tracker.Events[0].EventType);
    }

    private static PhysicsBody CreateBody(BodyHandle handle, bool isTrigger)
    {
        return new PhysicsBody(
            handle,
            PhysicsBodyType.Dynamic,
            new PhysicsCollider(ColliderShapeType.Sphere, new Vector3(1.0f, 1.0f, 1.0f), isTrigger, PhysicsMaterial.Default),
            Vector3.Zero,
            Quaternion.Identity,
            Vector3.Zero,
            Vector3.Zero,
            isActive: true);
    }
}
