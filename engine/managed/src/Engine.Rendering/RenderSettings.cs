using System;

namespace Engine.Rendering;

public readonly struct RenderSettings
{
    public static RenderSettings Default { get; } = new(RenderDebugViewMode.None);

    public RenderSettings(
        RenderDebugViewMode debugViewMode,
        RenderFeatureFlags featureFlags = RenderFeatureFlags.None)
    {
        if (!Enum.IsDefined(debugViewMode))
        {
            throw new ArgumentOutOfRangeException(nameof(debugViewMode), $"Unsupported debug view mode value: {debugViewMode}.");
        }

        if (!AreFeatureFlagsValid(featureFlags))
        {
            throw new ArgumentOutOfRangeException(nameof(featureFlags), $"Unsupported render feature flags value: {featureFlags}.");
        }

        DebugViewMode = debugViewMode;
        FeatureFlags = featureFlags;
    }

    public RenderDebugViewMode DebugViewMode { get; }

    public RenderFeatureFlags FeatureFlags { get; }

    public bool DisableAutoExposure => (FeatureFlags & RenderFeatureFlags.DisableAutoExposure) != 0;

    public bool DisableJitterEffects => (FeatureFlags & RenderFeatureFlags.DisableJitterEffects) != 0;

    private static bool AreFeatureFlagsValid(RenderFeatureFlags flags)
    {
        const RenderFeatureFlags supported =
            RenderFeatureFlags.DisableAutoExposure |
            RenderFeatureFlags.DisableJitterEffects;
        return (flags & ~supported) == 0;
    }
}
