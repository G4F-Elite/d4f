using System;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiAnchorsLayoutTests
{
    [Fact]
    public void Update_RightBottomAnchors_PositionElementFromParentEdges()
    {
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 200,
            Height = 100,
            LayoutMode = UiLayoutMode.Absolute
        };
        var child = new UiButton("child", new TextureHandle(2), "Child")
        {
            Width = 40,
            Height = 20,
            X = 10,
            Y = 5,
            Anchors = UiAnchor.Right | UiAnchor.Bottom
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);
        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(150, 75, 40, 20), child.LayoutBounds);
    }

    [Fact]
    public void Update_RightBottomAnchors_RespectParentPadding()
    {
        var root = new UiPanel("root", new TextureHandle(10))
        {
            Width = 200,
            Height = 100,
            LayoutMode = UiLayoutMode.Absolute,
            Padding = new UiThickness(10, 10, 10, 10)
        };
        var child = new UiButton("child", new TextureHandle(11), "Child")
        {
            Width = 20,
            Height = 10,
            Anchors = UiAnchor.Right | UiAnchor.Bottom
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);
        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(170, 80, 20, 10), child.LayoutBounds);
    }

    [Fact]
    public void Update_DefaultAnchors_KeepLegacyLeftTopBehavior()
    {
        var root = new UiPanel("root", new TextureHandle(20))
        {
            Width = 120,
            Height = 80,
            LayoutMode = UiLayoutMode.Absolute
        };
        var child = new UiButton("child", new TextureHandle(21), "Child")
        {
            Width = 30,
            Height = 12,
            X = 7,
            Y = 9
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);
        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(7, 9, 30, 12), child.LayoutBounds);
    }

    [Fact]
    public void Update_LeftRightAnchors_StretchAutoWidth()
    {
        var root = new UiPanel("root", new TextureHandle(22))
        {
            Width = 180,
            Height = 60,
            LayoutMode = UiLayoutMode.Absolute
        };
        var child = new UiButton("child", new TextureHandle(23), "Child")
        {
            X = 10,
            Y = 8,
            Height = 12,
            Anchors = UiAnchor.Left | UiAnchor.Right | UiAnchor.Top
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);
        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(10, 8, 160, 12), child.LayoutBounds);
    }

    [Fact]
    public void Update_TopBottomAnchors_StretchAutoHeight()
    {
        var root = new UiPanel("root", new TextureHandle(24))
        {
            Width = 90,
            Height = 120,
            LayoutMode = UiLayoutMode.Absolute
        };
        var child = new UiButton("child", new TextureHandle(25), "Child")
        {
            X = 6,
            Y = 7,
            Width = 20,
            Anchors = UiAnchor.Left | UiAnchor.Top | UiAnchor.Bottom
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);
        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(6, 7, 20, 106), child.LayoutBounds);
    }

    [Fact]
    public void Update_PairedAnchors_DoNotStretchWhenFixedSizeProvided()
    {
        var root = new UiPanel("root", new TextureHandle(26))
        {
            Width = 180,
            Height = 80,
            LayoutMode = UiLayoutMode.Absolute
        };
        var child = new UiButton("child", new TextureHandle(27), "Child")
        {
            X = 10,
            Y = 5,
            Width = 40,
            Height = 16,
            Anchors = UiAnchor.Left | UiAnchor.Right | UiAnchor.Top | UiAnchor.Bottom
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);
        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(10, 5, 40, 16), child.LayoutBounds);
    }

    [Fact]
    public void Anchors_SetterShouldRejectUnsupportedFlags()
    {
        var child = new UiButton("child", new TextureHandle(30), "Child");

        Assert.Throws<ArgumentOutOfRangeException>(() => child.Anchors = (UiAnchor)128);
    }
}
