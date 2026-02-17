using System;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;
using Xunit;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadePointerMoveTests
{
    [Fact]
    public void QueuePointerMove_InvokesHoverCallbacksForTopmostElement()
    {
        var frontEnter = 0;
        var frontLeave = 0;
        var frontMove = 0;
        var backEnter = 0;
        var backLeave = 0;
        var backMove = 0;

        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 120,
            Height = 120
        };

        var back = new UiButton("back", new TextureHandle(2), "Back")
        {
            X = 0,
            Y = 0,
            Width = 50,
            Height = 50,
            OnPointerEnter = () => backEnter++,
            OnPointerLeave = () => backLeave++,
            OnPointerMove = (_, _) => backMove++
        };
        root.AddChild(back);

        var front = new UiButton("front", new TextureHandle(3), "Front")
        {
            X = 10,
            Y = 10,
            Width = 50,
            Height = 50,
            OnPointerEnter = () => frontEnter++,
            OnPointerLeave = () => frontLeave++,
            OnPointerMove = (_, _) => frontMove++
        };
        root.AddChild(front);

        var document = new UiDocument();
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueuePointerMove(20, 20);
        facade.QueuePointerMove(25, 25);
        facade.QueuePointerMove(5, 5);
        facade.QueuePointerMove(200, 200);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(1, frontEnter);
        Assert.Equal(1, frontLeave);
        Assert.Equal(2, frontMove);
        Assert.Equal(1, backEnter);
        Assert.Equal(1, backLeave);
        Assert.Equal(1, backMove);
        Assert.False(front.IsHovered);
        Assert.False(back.IsHovered);
    }

    [Fact]
    public void QueuePointerMove_UsesScrollOffsetForHitTesting()
    {
        var bottomEnter = 0;
        var bottom = new UiButton("bottom", new TextureHandle(12), "Bottom")
        {
            Y = 50,
            Width = 80,
            Height = 20,
            OnPointerEnter = () => bottomEnter++
        };

        var scrollView = new UiScrollView("scroll", new TextureHandle(11))
        {
            Width = 100,
            Height = 40,
            ScrollStep = 20
        };
        scrollView.AddChild(bottom);

        var document = new UiDocument();
        document.AddRoot(scrollView);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueueScroll(10, 10, -1);
        facade.QueuePointerMove(10, 35);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(20, scrollView.ScrollOffsetY);
        Assert.Equal(1, bottomEnter);
        Assert.True(bottom.IsHovered);
    }

    [Fact]
    public void QueuePointerMove_RejectsNonFiniteCoordinates()
    {
        var facade = new RetainedUiFacade();

        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerMove(float.NaN, 0.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerMove(0.0f, float.PositiveInfinity));
    }

    [Fact]
    public void Update_ClearsHoverWhenElementBecomesInvisible()
    {
        var leaveCount = 0;
        var button = new UiButton("button", new TextureHandle(22), "Button")
        {
            Width = 60,
            Height = 20,
            OnPointerLeave = () => leaveCount++
        };

        var document = new UiDocument();
        document.AddRoot(button);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueuePointerMove(10, 10);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        Assert.True(button.IsHovered);

        button.Visible = false;
        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));

        Assert.False(button.IsHovered);
        Assert.Equal(1, leaveCount);
    }

    [Fact]
    public void Update_ClearsHoverWhenElementIsDetached()
    {
        var leaveCount = 0;
        var root = new UiPanel("root", new TextureHandle(31))
        {
            Width = 80,
            Height = 60
        };
        var child = new UiButton("child", new TextureHandle(32), "Child")
        {
            Width = 40,
            Height = 20,
            OnPointerLeave = () => leaveCount++
        };
        root.AddChild(child);

        var document = new UiDocument();
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueuePointerMove(10, 10);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        Assert.True(child.IsHovered);

        root.RemoveChild(child);
        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));

        Assert.False(child.IsHovered);
        Assert.Equal(1, leaveCount);
    }
}
