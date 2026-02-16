using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;

namespace Engine.Physics;

public sealed class PhysicsTriggerEventTracker : IWorldSystem
{
    private readonly IPhysicsFacade _physics;
    private readonly HashSet<TriggerContactPair> _activePairs = [];
    private readonly List<PhysicsContactEvent> _events = [];
    private readonly Dictionary<BodyHandle, bool> _triggerFlagsByBody = [];
    private PhysicsOverlapHit[] _overlapHits = Array.Empty<PhysicsOverlapHit>();

    public PhysicsTriggerEventTracker(IPhysicsFacade physics)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
    }

    public IReadOnlyList<PhysicsContactEvent> Events => _events;

    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);

        _events.Clear();
        _triggerFlagsByBody.Clear();

        int bodyCount = world.GetComponentCount<PhysicsBody>();
        EnsureOverlapCapacity(bodyCount == 0 ? 1 : bodyCount);

        foreach (var (_, body) in world.Query<PhysicsBody>())
        {
            _triggerFlagsByBody[body.Body] = body.Collider.IsTrigger;
        }

        var currentPairs = new HashSet<TriggerContactPair>(_activePairs.Count);
        foreach (var (_, body) in world.Query<PhysicsBody>())
        {
            if (!body.Collider.IsTrigger)
            {
                continue;
            }

            var overlapQuery = new PhysicsOverlapQuery(
                body.Position,
                body.Collider.ShapeType,
                body.Collider.Dimensions,
                includeTriggers: true);

            int overlapCount = _physics.Overlap(overlapQuery, _overlapHits.AsSpan());
            for (var i = 0; i < overlapCount; i++)
            {
                PhysicsOverlapHit overlapHit = _overlapHits[i];
                if (overlapHit.Body == body.Body)
                {
                    continue;
                }

                var pair = new TriggerContactPair(body.Body, overlapHit.Body);
                if (!currentPairs.Add(pair))
                {
                    continue;
                }

                _events.Add(new PhysicsContactEvent(
                    pair.TriggerBody,
                    pair.OtherBody,
                    _activePairs.Contains(pair) ? PhysicsContactEventType.Stay : PhysicsContactEventType.Enter,
                    overlapHit.IsTrigger));
            }
        }

        foreach (TriggerContactPair previousPair in _activePairs)
        {
            if (currentPairs.Contains(previousPair))
            {
                continue;
            }

            bool otherIsTrigger = _triggerFlagsByBody.TryGetValue(previousPair.OtherBody, out bool isTrigger) && isTrigger;
            _events.Add(new PhysicsContactEvent(
                previousPair.TriggerBody,
                previousPair.OtherBody,
                PhysicsContactEventType.Exit,
                otherIsTrigger));
        }

        _activePairs.Clear();
        _activePairs.UnionWith(currentPairs);
    }

    private void EnsureOverlapCapacity(int required)
    {
        if (required <= _overlapHits.Length)
        {
            return;
        }

        _overlapHits = new PhysicsOverlapHit[required];
    }

    private readonly record struct TriggerContactPair(BodyHandle TriggerBody, BodyHandle OtherBody);
}
