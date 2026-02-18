using System;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.Rendering;

public sealed class RenderSettingsTests
{
    [Fact]
    public void Constructor_StoresDebugViewMode()
    {
        var settings = new RenderSettings(RenderDebugViewMode.Depth);

        Assert.Equal(RenderDebugViewMode.Depth, settings.DebugViewMode);
    }

    [Fact]
    public void Constructor_RejectsUnknownDebugViewMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderSettings((RenderDebugViewMode)255));
    }

    [Fact]
    public void Constructor_StoresFeatureFlags()
    {
        var settings = new RenderSettings(
            RenderDebugViewMode.None,
            RenderFeatureFlags.DisableAutoExposure | RenderFeatureFlags.DisableJitterEffects);

        Assert.Equal(
            RenderFeatureFlags.DisableAutoExposure | RenderFeatureFlags.DisableJitterEffects,
            settings.FeatureFlags);
        Assert.True(settings.DisableAutoExposure);
        Assert.True(settings.DisableJitterEffects);
    }

    [Fact]
    public void Constructor_RejectsUnknownFeatureFlags()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderSettings(
            RenderDebugViewMode.None,
            (RenderFeatureFlags)0x80));
    }

    [Fact]
    public void Default_UsesNoneDebugViewMode()
    {
        Assert.Equal(RenderDebugViewMode.None, RenderSettings.Default.DebugViewMode);
        Assert.Equal(RenderFeatureFlags.None, RenderSettings.Default.FeatureFlags);
        Assert.False(RenderSettings.Default.DisableAutoExposure);
        Assert.False(RenderSettings.Default.DisableJitterEffects);
    }
}
