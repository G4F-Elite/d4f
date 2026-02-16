namespace Engine.App;

public sealed class DeterministicModeOptions
{
    public static DeterministicModeOptions Disabled { get; } = new(false, 0UL, null, false, false);

    public DeterministicModeOptions(
        bool enabled,
        ulong seed,
        TimeSpan? fixedDeltaTimeOverride,
        bool disableAutoExposure,
        bool disableJitterEffects)
    {
        if (fixedDeltaTimeOverride.HasValue && fixedDeltaTimeOverride.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fixedDeltaTimeOverride),
                "Deterministic fixed delta override must be positive.");
        }

        Enabled = enabled;
        Seed = seed;
        FixedDeltaTimeOverride = fixedDeltaTimeOverride;
        DisableAutoExposure = disableAutoExposure;
        DisableJitterEffects = disableJitterEffects;
    }

    public bool Enabled { get; }

    public ulong Seed { get; }

    public TimeSpan? FixedDeltaTimeOverride { get; }

    public bool DisableAutoExposure { get; }

    public bool DisableJitterEffects { get; }
}
