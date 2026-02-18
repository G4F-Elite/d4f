using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadeKeyboardInputTests
{
    [Fact]
    public void QueueKeyDownAndUp_DispatchesToFocusedInput()
    {
        var keyDown = new List<UiKey>();
        var keyUp = new List<UiKey>();

        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 200,
            Height = 120
        };
        var input = new UiInputField("input", new TextureHandle(2), new TextureHandle(3))
        {
            X = 10,
            Y = 10,
            Width = 100,
            Height = 20,
            OnKeyDown = key => keyDown.Add(key),
            OnKeyUp = key => keyUp.Add(key)
        };
        root.AddChild(input);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerClick(12, 12);
        facade.QueueKeyDown(UiKey.Enter);
        facade.QueueKeyUp(UiKey.Enter);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.True(input.IsFocused);
        Assert.Equal([UiKey.Enter], keyDown);
        Assert.Equal([UiKey.Enter], keyUp);
    }

    [Fact]
    public void QueueKeyDownAndUp_DispatchesToHoveredElementWhenNoFocus()
    {
        var events = new List<string>();

        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(10))
        {
            Width = 120,
            Height = 120
        };
        var button = new UiButton("button", new TextureHandle(11), "Button")
        {
            X = 5,
            Y = 5,
            Width = 40,
            Height = 20,
            OnKeyDown = key => events.Add($"down:{key}"),
            OnKeyUp = key => events.Add($"up:{key}")
        };
        root.AddChild(button);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerMove(10, 10);
        facade.QueueKeyDown(UiKey.Space);
        facade.QueueKeyUp(UiKey.Space);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(["down:Space", "up:Space"], events);
    }

    [Fact]
    public void QueueKeyDownAndUp_IgnoresKeyboardWhenNoTarget()
    {
        var keyEvents = 0;

        var document = new UiDocument();
        var hidden = new UiButton("hidden", new TextureHandle(20), "Hidden")
        {
            Visible = false,
            Width = 40,
            Height = 20,
            OnKeyDown = _ => keyEvents++,
            OnKeyUp = _ => keyEvents++
        };
        document.AddRoot(hidden);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerMove(10, 10);
        facade.QueueKeyDown(UiKey.Tab);
        facade.QueueKeyUp(UiKey.Tab);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(0, keyEvents);
    }

    [Fact]
    public void QueueKeyDownAndUp_RejectsUndefinedKeyValues()
    {
        var facade = new RetainedUiFacade();

        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueKeyDown((UiKey)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueKeyDown((UiKey)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueKeyUp((UiKey)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueKeyUp((UiKey)(-1)));
    }
}
