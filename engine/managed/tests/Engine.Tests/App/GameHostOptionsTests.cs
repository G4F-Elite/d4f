using System;
using Engine.App;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.App;

public sealed class GameHostOptionsTests
{
    [Fact]
    public void Constructor_StoresProvidedValues()
    {
        var options = new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(8),
            maxSubsteps: 6,
            frameArenaBytes: 4096,
            frameArenaAlignment: 128,
            maxAccumulatedTime: TimeSpan.FromMilliseconds(60),
            deterministicMode: new DeterministicModeOptions(
                enabled: true,
                seed: 42,
                fixedDeltaTimeOverride: TimeSpan.FromMilliseconds(16),
                disableAutoExposure: true,
                disableJitterEffects: true),
            renderSettings: new RenderSettings(
                RenderDebugViewMode.Albedo,
                RenderFeatureFlags.DisableAutoExposure));

        Assert.Equal(TimeSpan.FromMilliseconds(8), options.FixedDt);
        Assert.Equal(6, options.MaxSubsteps);
        Assert.Equal(4096, options.FrameArenaBytes);
        Assert.Equal(128, options.FrameArenaAlignment);
        Assert.Equal(TimeSpan.FromMilliseconds(60), options.MaxAccumulatedTime);
        Assert.True(options.DeterministicMode.Enabled);
        Assert.Equal(42UL, options.DeterministicMode.Seed);
        Assert.Equal(TimeSpan.FromMilliseconds(16), options.DeterministicMode.FixedDeltaTimeOverride);
        Assert.True(options.DeterministicMode.DisableAutoExposure);
        Assert.True(options.DeterministicMode.DisableJitterEffects);
        Assert.Equal(RenderDebugViewMode.Albedo, options.RenderSettings.DebugViewMode);
        Assert.True(options.RenderSettings.DisableAutoExposure);
    }

    [Fact]
    public void Default_ProvidesValidValues()
    {
        var options = GameHostOptions.Default;

        Assert.True(options.FixedDt > TimeSpan.Zero);
        Assert.True(options.MaxSubsteps > 0);
        Assert.True(options.FrameArenaBytes > 0);
        Assert.True(options.FrameArenaAlignment > 0);
        Assert.Equal(0, options.FrameArenaAlignment & (options.FrameArenaAlignment - 1));
        Assert.True(options.MaxAccumulatedTime >= options.FixedDt);
        Assert.False(options.DeterministicMode.Enabled);
        Assert.Equal(RenderDebugViewMode.None, options.RenderSettings.DebugViewMode);
        Assert.Equal(RenderFeatureFlags.None, options.RenderSettings.FeatureFlags);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveFixedDt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameHostOptions(
            fixedDt: TimeSpan.Zero,
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveMaxSubsteps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 0,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveFrameArenaBytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 0,
            frameArenaAlignment: 64));
    }

    [Fact]
    public void Constructor_RejectsInvalidFrameArenaAlignment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 96));
    }

    [Fact]
    public void Constructor_RejectsMaxAccumulatedTimeSmallerThanFixedDt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameHostOptions(
            fixedDt: TimeSpan.FromMilliseconds(16),
            maxSubsteps: 4,
            frameArenaBytes: 2048,
            frameArenaAlignment: 64,
            maxAccumulatedTime: TimeSpan.FromMilliseconds(8)));
    }

    [Fact]
    public void DeterministicModeOptions_RejectsInvalidFixedDeltaOverride()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeterministicModeOptions(
            enabled: true,
            seed: 1,
            fixedDeltaTimeOverride: TimeSpan.Zero,
            disableAutoExposure: true,
            disableJitterEffects: true));
    }
}
