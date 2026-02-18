using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadePointerDownUpTests
{
    [Fact]
    public void QueuePointerDownAndUp_DispatchesToTopmostElementInOrder()
    {
        var events = new List<string>();
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 120,
            Height = 120
        };

        var back = new UiButton("back", new TextureHandle(2), "Back")
        {
            Width = 60,
            Height = 60,
            OnPointerDown = (_, _) => events.Add("back:down"),
            OnPointerUp = (_, _) => events.Add("back:up")
        };
        var front = new UiButton("front", new TextureHandle(3), "Front")
        {
            X = 10,
            Y = 10,
            Width = 60,
            Height = 60,
            OnPointerDown = (_, _) => events.Add("front:down"),
            OnPointerUp = (_, _) => events.Add("front:up")
        };

        root.AddChild(back);
        root.AddChild(front);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerDown(20, 20);
        facade.QueuePointerUp(20, 20);
        facade.QueuePointerDown(5, 5);
        facade.QueuePointerUp(5, 5);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(["front:down", "front:up", "back:down", "back:up"], events);
    }

    [Fact]
    public void QueuePointerDownAndUp_UsesScrollAdjustedHitTesting()
    {
        var events = new List<string>();
        var document = new UiDocument();
        var scrollView = new UiScrollView("scroll", new TextureHandle(10))
        {
            Width = 100,
            Height = 40,
            ScrollStep = 20
        };
        var child = new UiButton("child", new TextureHandle(11), "Child")
        {
            Y = 50,
            Width = 80,
            Height = 20,
            OnPointerDown = (_, _) => events.Add("child:down"),
            OnPointerUp = (_, _) => events.Add("child:up")
        };
        scrollView.AddChild(child);
        document.AddRoot(scrollView);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueScroll(10, 10, -1);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        facade.QueuePointerDown(10, 35);
        facade.QueuePointerUp(10, 35);
        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(["child:down", "child:up"], events);
    }

    [Fact]
    public void QueuePointerDownAndUp_IgnoresHiddenAndMissedElements()
    {
        var downCount = 0;
        var upCount = 0;
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(20))
        {
            Width = 100,
            Height = 100
        };
        root.AddChild(new UiButton("hidden", new TextureHandle(21), "Hidden")
        {
            Width = 80,
            Height = 30,
            Visible = false,
            OnPointerDown = (_, _) => downCount++,
            OnPointerUp = (_, _) => upCount++
        });
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerDown(10, 10);
        facade.QueuePointerUp(10, 10);
        facade.QueuePointerDown(500, 500);
        facade.QueuePointerUp(500, 500);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(0, downCount);
        Assert.Equal(0, upCount);
    }

    [Fact]
    public void QueuePointerDownAndUp_RejectsInvalidCoordinates()
    {
        var facade = new RetainedUiFacade();

        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerDown(float.NaN, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerDown(0f, float.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerUp(float.PositiveInfinity, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerUp(0f, float.NaN));
    }
}
