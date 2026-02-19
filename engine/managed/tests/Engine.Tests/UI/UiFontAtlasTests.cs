using System;
using System.Collections.Generic;
using Engine.UI;
using Xunit;

namespace Engine.Tests.UI;

public sealed class UiFontAtlasTests
{
    [Fact]
    public void GetKerning_ShouldReturnConfiguredPairAdjustment()
    {
        var atlas = new UiFontAtlas(
            lineHeight: 20f,
            defaultAdvance: 8f,
            kerningPairs: new Dictionary<UiKerningPair, float>
            {
                [new UiKerningPair('A', 'V')] = -1.5f
            });

        Assert.Equal(-1.5f, atlas.GetKerning('A', 'V'));
        Assert.Equal(0f, atlas.GetKerning('V', 'A'));
    }

    [Fact]
    public void Constructor_ShouldRejectNonFiniteKerningValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiFontAtlas(
            lineHeight: 20f,
            defaultAdvance: 8f,
            kerningPairs: new Dictionary<UiKerningPair, float>
            {
                [new UiKerningPair('A', 'V')] = float.NaN
            }));
    }

    [Fact]
    public void GetAdvance_ShouldFallbackToDefault_WhenGlyphIsMissing()
    {
        var atlas = new UiFontAtlas(lineHeight: 20f, defaultAdvance: 7f);

        Assert.Equal(7f, atlas.GetAdvance('Z'));
    }
}
