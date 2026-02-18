using System;

namespace Engine.App;

public sealed class InteropBudgetOptions
{
    public static InteropBudgetOptions Disabled { get; } = new(
        enforce: false,
        maxRendererCallsPerFrame: int.MaxValue,
        maxPhysicsCallsPerTick: int.MaxValue);

    public static InteropBudgetOptions ReleaseStrict { get; } = new(
        enforce: true,
        maxRendererCallsPerFrame: 3,
        maxPhysicsCallsPerTick: 3);

    public InteropBudgetOptions(
        bool enforce,
        int maxRendererCallsPerFrame,
        int maxPhysicsCallsPerTick)
    {
        if (maxRendererCallsPerFrame <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRendererCallsPerFrame),
                "Max renderer interop calls per frame must be positive.");
        }

        if (maxPhysicsCallsPerTick <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPhysicsCallsPerTick),
                "Max physics interop calls per tick must be positive.");
        }

        Enforce = enforce;
        MaxRendererCallsPerFrame = maxRendererCallsPerFrame;
        MaxPhysicsCallsPerTick = maxPhysicsCallsPerTick;
    }

    public bool Enforce { get; }

    public int MaxRendererCallsPerFrame { get; }

    public int MaxPhysicsCallsPerTick { get; }
}
