using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadeFocusEventsTests
{
    [Fact]
    public void QueuePointerClick_InvokesInputFocusAndBlurCallbacks()
    {
        var events = new List<string>();
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
            OnFocus = () => events.Add("focus"),
            OnBlur = () => events.Add("blur")
        };
        root.AddChild(input);
        root.AddChild(new UiButton("button", new TextureHandle(4), "Blur")
        {
            X = 10,
            Y = 40,
            Width = 80,
            Height = 20
        });
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerClick(15, 15);
        facade.QueuePointerClick(15, 45);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(["focus", "blur"], events);
        Assert.False(input.IsFocused);
    }

    [Fact]
    public void QueueClick_ByElementId_TriggersFocusCallbacksWithoutDuplicates()
    {
        var events = new List<string>();
        var document = new UiDocument();
        var input = new UiInputField("input", new TextureHandle(10), new TextureHandle(11))
        {
            Width = 120,
            Height = 20,
            OnFocus = () => events.Add("focus"),
            OnBlur = () => events.Add("blur")
        };
        document.AddRoot(input);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueClick("input");
        facade.QueueClick("input");
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(["focus"], events);
        Assert.True(input.IsFocused);
    }
}
