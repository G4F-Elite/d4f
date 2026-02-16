using System;
using System.Numerics;
using Engine.Core.Timing;
using Engine.ECS;

namespace Engine.Physics;

public sealed class CharacterControllerSystem : IWorldSystem
{
    private const float Epsilon = 0.00001f;
    private const float GroundedNormalThreshold = 0.65f;
    private readonly IPhysicsFacade _physics;

    public CharacterControllerSystem(IPhysicsFacade physics)
    {
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
    }

    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);

        float deltaTimeSeconds = checked((float)timing.DeltaTime.TotalSeconds);
        if (deltaTimeSeconds <= 0.0f)
        {
            return;
        }

        foreach (var (entity, body, controller) in world.Query<PhysicsBody, CharacterController>())
        {
            if (body.BodyType == PhysicsBodyType.Static)
            {
                continue;
            }

            Vector3 desiredDisplacement = controller.DesiredVelocity * deltaTimeSeconds;
            float desiredDistance = desiredDisplacement.Length();

            if (desiredDistance <= Epsilon)
            {
                if (controller.IsGrounded)
                {
                    var updatedIdleController = controller;
                    updatedIdleController.IsGrounded = false;
                    world.SetComponent(entity, updatedIdleController);
                }

                continue;
            }

            Vector3 direction = desiredDisplacement / desiredDistance;
            var sweepQuery = new PhysicsSweepQuery(
                body.Position,
                direction,
                desiredDistance + controller.SkinWidth,
                ColliderShapeType.Capsule,
                new Vector3(controller.Radius, controller.Height, controller.Radius),
                includeTriggers: false);

            Vector3 resolvedDisplacement = desiredDisplacement;
            bool isGrounded = false;
            if (_physics.Sweep(sweepQuery, out PhysicsSweepHit hit))
            {
                float maxMoveDistance = MathF.Max(0.0f, MathF.Min(desiredDistance, hit.Distance - controller.SkinWidth));
                resolvedDisplacement = direction * maxMoveDistance;
                isGrounded = hit.Normal.Y >= GroundedNormalThreshold;
            }

            var updatedBody = body;
            updatedBody.Position += resolvedDisplacement;
            updatedBody.LinearVelocity = resolvedDisplacement / deltaTimeSeconds;
            world.SetComponent(entity, updatedBody);

            var updatedController = controller;
            updatedController.IsGrounded = isGrounded;
            world.SetComponent(entity, updatedController);
        }
    }
}
