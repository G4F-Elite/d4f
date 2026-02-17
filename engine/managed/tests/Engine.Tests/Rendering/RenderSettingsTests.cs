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
    public void Default_UsesNoneDebugViewMode()
    {
        Assert.Equal(RenderDebugViewMode.None, RenderSettings.Default.DebugViewMode);
    }
}
