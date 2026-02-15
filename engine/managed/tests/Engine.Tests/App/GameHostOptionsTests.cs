using System;
using Engine.App;
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
            frameArenaAlignment: 128);

        Assert.Equal(TimeSpan.FromMilliseconds(8), options.FixedDt);
        Assert.Equal(6, options.MaxSubsteps);
        Assert.Equal(4096, options.FrameArenaBytes);
        Assert.Equal(128, options.FrameArenaAlignment);
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
}
