using System;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiPreviewHostTests
{
    [Fact]
    public void BuildDrawData_ReturnsCommandsAndTreeDump()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 200,
            Height = 100
        };
        root.AddChild(new UiButton("play", new TextureHandle(2), "Play")
        {
            X = 10,
            Y = 20,
            Width = 80,
            Height = 24
        });
        document.AddRoot(root);

        var host = new UiPreviewHost(document);

        var commands = host.BuildDrawData(new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        string dump = host.DumpTree();

        Assert.Equal(2, commands.Count);
        Assert.Contains("UiPanel id=\"root\" visible=true bounds=(0,0,200,100)", dump);
        Assert.Contains("UiButton id=\"play\" visible=true bounds=(10,20,80,24)", dump);
    }

    [Fact]
    public void QueueInteractionMethods_ApplyDuringPreviewUpdate()
    {
        var pointerDownCount = 0;
        var pointerUpCount = 0;
        var keyDownCount = 0;
        var keyUpCount = 0;
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(10))
        {
            Width = 200,
            Height = 120
        };
        var toggle = new UiToggle("toggle", new TextureHandle(11))
        {
            X = 10,
            Y = 10,
            Width = 20,
            Height = 20,
            OnPointerDown = (_, _) => pointerDownCount++,
            OnPointerUp = (_, _) => pointerUpCount++
        };
        var input = new UiInputField("input", new TextureHandle(12), new TextureHandle(13))
        {
            X = 10,
            Y = 40,
            Width = 100,
            Height = 20,
            OnKeyDown = _ => keyDownCount++,
            OnKeyUp = _ => keyUpCount++
        };
        root.AddChild(toggle);
        root.AddChild(input);
        document.AddRoot(root);

        var host = new UiPreviewHost(document);
        host.QueuePointerDown(15, 15);
        host.QueuePointerUp(15, 15);
        host.QueuePointerMove(15, 15);
        host.QueuePointerClick(15, 15);
        host.QueuePointerClick(15, 45);
        host.QueueTextInput("abc");
        host.QueueKeyDown(UiKey.Enter);
        host.QueueKeyUp(UiKey.Enter);
        host.QueueBackspace();
        host.BuildDrawData(new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.True(toggle.IsOn);
        Assert.Equal("ab", input.Text);
        Assert.True(input.IsFocused);
        Assert.True(toggle.IsHovered);
        Assert.Equal(1, pointerDownCount);
        Assert.Equal(1, pointerUpCount);
        Assert.Equal(1, keyDownCount);
        Assert.Equal(1, keyUpCount);
    }

    [Fact]
    public void BuildDrawData_ReturnsEmptyForEmptyDocument()
    {
        var host = new UiPreviewHost();

        var commands = host.BuildDrawData(new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Empty(commands);
        Assert.Equal(string.Empty, host.DumpTree());
    }

    [Fact]
    public void BuildDrawData_SupportsImageAndListComponents()
    {
        var clickedItems = new List<(int Index, string Value)>();
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(40))
        {
            Width = 220,
            Height = 160
        };
        root.AddChild(new UiImage("logo", new TextureHandle(41))
        {
            X = 10,
            Y = 10,
            Width = 60,
            Height = 24
        });
        var list = new UiList("list", new TextureHandle(42), new TextureHandle(43))
        {
            X = 10,
            Y = 50,
            Width = 120,
            Height = 40
        };
        list.SetItems(["first", "second", "third"]);
        list.OnItemClick = (index, value) => clickedItems.Add((index, value));
        root.AddChild(list);
        document.AddRoot(root);

        var host = new UiPreviewHost(document);
        host.QueuePointerClick(20, 75);
        var commands = host.BuildDrawData(new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        string dump = host.DumpTree();

        Assert.NotEmpty(commands);
        Assert.Equal([(1, "second")], clickedItems);
        Assert.Contains("UiImage id=\"logo\"", dump);
        Assert.Contains("UiList id=\"list\"", dump);
    }
}
