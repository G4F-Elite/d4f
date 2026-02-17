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
            Height = 20
        };
        var input = new UiInputField("input", new TextureHandle(12), new TextureHandle(13))
        {
            X = 10,
            Y = 40,
            Width = 100,
            Height = 20
        };
        root.AddChild(toggle);
        root.AddChild(input);
        document.AddRoot(root);

        var host = new UiPreviewHost(document);
        host.QueuePointerMove(15, 15);
        host.QueuePointerClick(15, 15);
        host.QueuePointerClick(15, 45);
        host.QueueTextInput("abc");
        host.QueueBackspace();
        host.BuildDrawData(new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.True(toggle.IsOn);
        Assert.Equal("ab", input.Text);
        Assert.True(input.IsFocused);
        Assert.True(toggle.IsHovered);
    }

    [Fact]
    public void BuildDrawData_ReturnsEmptyForEmptyDocument()
    {
        var host = new UiPreviewHost();

        var commands = host.BuildDrawData(new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Empty(commands);
        Assert.Equal(string.Empty, host.DumpTree());
    }
}
