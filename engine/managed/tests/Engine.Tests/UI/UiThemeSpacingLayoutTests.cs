using System;
using System.Numerics;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiThemeSpacingLayoutTests
{
    [Fact]
    public void Update_UsesThemeSpacing_WhenLayoutGapIsUnset()
    {
        var document = new UiDocument
        {
            Theme = new UiTheme(new UiResolvedStyle(
                fontFamily: "Default",
                fontSize: 16f,
                foregroundColor: Vector4.One,
                backgroundColor: Vector4.Zero,
                borderRadius: 0f,
                shadow: UiShadowStyle.None,
                spacing: 7f))
        };
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 120,
            Height = 120,
            LayoutMode = UiLayoutMode.VerticalStack
        };
        var a = new UiButton("a", new TextureHandle(2), "A")
        {
            Height = 10
        };
        var b = new UiButton("b", new TextureHandle(3), "B")
        {
            Height = 10
        };
        root.AddChild(a);
        root.AddChild(b);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(0, 0, 120, 10), a.LayoutBounds);
        Assert.Equal(new RectF(0, 17, 120, 10), b.LayoutBounds);
    }

    [Fact]
    public void Update_ExplicitLayoutGapOverridesThemeSpacing()
    {
        var document = new UiDocument
        {
            Theme = new UiTheme(new UiResolvedStyle(
                fontFamily: UiResolvedStyle.Default.FontFamily,
                fontSize: UiResolvedStyle.Default.FontSize,
                foregroundColor: UiResolvedStyle.Default.ForegroundColor,
                backgroundColor: UiResolvedStyle.Default.BackgroundColor,
                borderRadius: UiResolvedStyle.Default.BorderRadius,
                shadow: UiResolvedStyle.Default.Shadow,
                spacing: 9f))
        };
        var root = new UiPanel("root", new TextureHandle(10))
        {
            Width = 100,
            Height = 100,
            LayoutMode = UiLayoutMode.VerticalStack,
            LayoutGap = 3f
        };
        var a = new UiButton("a", new TextureHandle(11), "A") { Height = 10 };
        var b = new UiButton("b", new TextureHandle(12), "B") { Height = 10 };
        root.AddChild(a);
        root.AddChild(b);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(0, 13, 100, 10), b.LayoutBounds);
    }

    [Fact]
    public void Update_UsesLocalStyleSpacingForNestedContainer()
    {
        var document = new UiDocument
        {
            Theme = new UiTheme(new UiResolvedStyle(
                fontFamily: UiResolvedStyle.Default.FontFamily,
                fontSize: UiResolvedStyle.Default.FontSize,
                foregroundColor: UiResolvedStyle.Default.ForegroundColor,
                backgroundColor: UiResolvedStyle.Default.BackgroundColor,
                borderRadius: UiResolvedStyle.Default.BorderRadius,
                shadow: UiResolvedStyle.Default.Shadow,
                spacing: 8f))
        };
        var outer = new UiPanel("outer", new TextureHandle(20))
        {
            Width = 140,
            Height = 140,
            LayoutMode = UiLayoutMode.Absolute
        };
        var inner = new UiPanel("inner", new TextureHandle(21))
        {
            Width = 120,
            Height = 80,
            LayoutMode = UiLayoutMode.VerticalStack,
            StyleOverride = new UiStyle
            {
                Spacing = 2f
            }
        };
        var first = new UiButton("first", new TextureHandle(22), "First")
        {
            Height = 10
        };
        var second = new UiButton("second", new TextureHandle(23), "Second")
        {
            Height = 10
        };
        inner.AddChild(first);
        inner.AddChild(second);
        outer.AddChild(inner);
        document.AddRoot(outer);

        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(0, 0, 120, 10), first.LayoutBounds);
        Assert.Equal(new RectF(0, 12, 120, 10), second.LayoutBounds);
    }
}
