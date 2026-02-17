using System;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiFlexLayoutTests
{
    [Fact]
    public void Update_AppliesRowFlexWithSpaceBetweenAndCenterAlign()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 120,
            Height = 40,
            LayoutMode = UiLayoutMode.Flex,
            FlexDirection = UiFlexDirection.Row,
            JustifyContent = UiJustifyContent.SpaceBetween,
            AlignItems = UiAlignItems.Center
        };
        var first = new UiButton("first", new TextureHandle(2), "A")
        {
            Width = 20,
            Height = 10
        };
        var second = new UiButton("second", new TextureHandle(3), "B")
        {
            Width = 20,
            Height = 20
        };
        root.AddChild(first);
        root.AddChild(second);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(0, 5, 20, 10), first.LayoutBounds);
        Assert.Equal(new RectF(100, 0, 20, 20), second.LayoutBounds);
    }

    [Fact]
    public void Update_AppliesRowFlexWrapAcrossMultipleLines()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(10))
        {
            Width = 100,
            Height = 100,
            LayoutMode = UiLayoutMode.Flex,
            FlexDirection = UiFlexDirection.Row,
            Wrap = true,
            LayoutGap = 5
        };
        var first = new UiButton("first", new TextureHandle(11), "A") { Width = 40, Height = 10 };
        var second = new UiButton("second", new TextureHandle(12), "B") { Width = 40, Height = 10 };
        var third = new UiButton("third", new TextureHandle(13), "C") { Width = 40, Height = 10 };
        root.AddChild(first);
        root.AddChild(second);
        root.AddChild(third);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(0, 0, 40, 10), first.LayoutBounds);
        Assert.Equal(new RectF(45, 0, 40, 10), second.LayoutBounds);
        Assert.Equal(new RectF(0, 15, 40, 10), third.LayoutBounds);
    }

    [Fact]
    public void Update_AppliesColumnFlexEndAndStretch()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(20))
        {
            Width = 60,
            Height = 100,
            LayoutMode = UiLayoutMode.Flex,
            FlexDirection = UiFlexDirection.Column,
            JustifyContent = UiJustifyContent.End,
            AlignItems = UiAlignItems.Stretch
        };
        var first = new UiButton("first", new TextureHandle(21), "A")
        {
            Height = 20
        };
        var second = new UiButton("second", new TextureHandle(22), "B")
        {
            Height = 20
        };
        root.AddChild(first);
        root.AddChild(second);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        facade.Update(new World(), new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(0, 60, 60, 20), first.LayoutBounds);
        Assert.Equal(new RectF(0, 80, 60, 20), second.LayoutBounds);
    }

    [Fact]
    public void Element_RejectsUnsupportedFlexEnums()
    {
        var panel = new UiPanel("root", new TextureHandle(30));

        Assert.Throws<ArgumentOutOfRangeException>(() => panel.FlexDirection = (UiFlexDirection)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.JustifyContent = (UiJustifyContent)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => panel.AlignItems = (UiAlignItems)99);
    }
}
