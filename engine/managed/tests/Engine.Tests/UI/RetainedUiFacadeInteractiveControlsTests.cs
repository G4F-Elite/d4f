using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;
using Engine.UI;
using Xunit;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadeInteractiveControlsTests
{
    [Fact]
    public void Update_BuildsDrawCommandsForExtendedControls()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(100))
        {
            Width = 320,
            Height = 240
        };
        root.AddChild(new UiToggle("toggle", new TextureHandle(101))
        {
            X = 10,
            Y = 10,
            Width = 20,
            Height = 20
        });
        root.AddChild(new UiSlider("slider", new TextureHandle(102), new TextureHandle(103), 0.25f)
        {
            X = 10,
            Y = 40,
            Width = 100,
            Height = 10
        });
        root.AddChild(new UiInputField("input", new TextureHandle(104), new TextureHandle(105), text: "Hi")
        {
            X = 10,
            Y = 60,
            Width = 120,
            Height = 24,
            Padding = new UiThickness(4, 2, 0, 0)
        });

        var scrollView = new UiScrollView("scroll", new TextureHandle(106))
        {
            X = 150,
            Y = 10,
            Width = 100,
            Height = 50
        };
        scrollView.AddChild(new UiButton("scroll-a", new TextureHandle(107), "A")
        {
            Width = 80,
            Height = 20
        });
        scrollView.AddChild(new UiButton("scroll-b", new TextureHandle(108), "B")
        {
            Y = 40,
            Width = 80,
            Height = 20
        });
        root.AddChild(scrollView);

        var list = new UiVirtualizedList("list", new TextureHandle(109), new TextureHandle(110))
        {
            X = 10,
            Y = 100,
            Width = 100,
            Height = 40,
            ItemHeight = 20
        };
        list.SetItems(["One", "Two", "Three", "Four"]);
        root.AddChild(list);

        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        UiRenderBatch batch = Assert.Single(world.Query<UiRenderBatch>()).Component;
        Assert.Equal(15, batch.Commands.Count);
        Assert.Equal(new RectF(10, 40, 25, 10), batch.Commands[3].Bounds); // slider fill
        Assert.Equal(new RectF(14, 62, 116, 22), batch.Commands[5].Bounds); // input text padded bounds

        for (int i = 1; i < batch.Commands.Count; i++)
        {
            UiDrawCommand previous = batch.Commands[i - 1];
            UiDrawCommand current = batch.Commands[i];
            Assert.Equal(previous.VertexOffset + previous.VertexCount, current.VertexOffset);
            Assert.Equal(previous.IndexOffset + previous.IndexCount, current.IndexOffset);
        }
    }

    [Fact]
    public void QueuePointerClick_HandlesToggleSliderAndVirtualizedListItem()
    {
        bool? toggleState = null;
        float sliderState = -1.0f;
        var clickedItems = new List<(int Index, string Value)>();

        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(200))
        {
            Width = 300,
            Height = 200
        };
        root.AddChild(new UiToggle("toggle", new TextureHandle(201), onChanged: value => toggleState = value)
        {
            X = 10,
            Y = 10,
            Width = 20,
            Height = 20
        });
        root.AddChild(new UiSlider("slider", new TextureHandle(202), new TextureHandle(203), onChanged: value => sliderState = value)
        {
            X = 40,
            Y = 10,
            Width = 100,
            Height = 20
        });
        var list = new UiVirtualizedList("list", new TextureHandle(204), new TextureHandle(205))
        {
            X = 10,
            Y = 50,
            Width = 120,
            Height = 40
        };
        list.SetItems(["first", "second", "third", "fourth"]);
        list.OnItemClick = (index, value) => clickedItems.Add((index, value));
        root.AddChild(list);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueuePointerClick(15, 15); // toggle
        facade.QueuePointerClick(90, 15); // slider -> 0.5
        facade.QueuePointerClick(20, 55); // list item 0
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.True(toggleState);
        Assert.InRange(sliderState, 0.499f, 0.501f);
        Assert.Single(clickedItems);
        Assert.Equal((0, "first"), clickedItems[0]);
    }

    [Fact]
    public void QueueTextInput_RequiresFocusAndSupportsBackspace()
    {
        var textUpdates = new List<string>();
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(300))
        {
            Width = 300,
            Height = 200
        };
        var input = new UiInputField("input", new TextureHandle(301), new TextureHandle(302), onTextChanged: text => textUpdates.Add(text))
        {
            X = 10,
            Y = 10,
            Width = 120,
            Height = 24
        };
        root.AddChild(input);
        root.AddChild(new UiButton("button", new TextureHandle(303), "Blur")
        {
            X = 10,
            Y = 50,
            Width = 60,
            Height = 20
        });
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueueTextInput("abc");
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        Assert.Equal(string.Empty, input.Text);

        facade.QueuePointerClick(15, 15);
        facade.QueueTextInput("abc");
        facade.QueueBackspace();
        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));

        Assert.True(input.IsFocused);
        Assert.Equal("ab", input.Text);
        Assert.Equal(["abc", "ab"], textUpdates);

        facade.QueuePointerClick(15, 55);
        facade.QueueTextInput("z");
        facade.Update(world, new FrameTiming(2, TimeSpan.Zero, TimeSpan.Zero));

        Assert.False(input.IsFocused);
        Assert.Equal("ab", input.Text);
    }

    [Fact]
    public void QueueScroll_PrioritizesTopmostScrollableElement()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(400))
        {
            Width = 300,
            Height = 200
        };
        var scrollView = new UiScrollView("scroll", new TextureHandle(401))
        {
            Width = 120,
            Height = 80,
            ScrollStep = 10
        };
        var list = new UiVirtualizedList("list", new TextureHandle(402), new TextureHandle(403))
        {
            Width = 120,
            Height = 80,
            ScrollStep = 10
        };
        list.SetItems(["0", "1", "2", "3", "4", "5"]);
        scrollView.AddChild(list);
        root.AddChild(scrollView);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueueScroll(10, 10, -1);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(10, list.ScrollOffsetY);
        Assert.Equal(0, scrollView.ScrollOffsetY);

        UiRenderBatch batch = Assert.Single(world.Query<UiRenderBatch>()).Component;
        UiDrawCommand firstListItem = batch.Commands.First(command => command.Texture.Value == 402);
        Assert.Equal(0, firstListItem.Bounds.Y);
    }

    [Fact]
    public void ScrollView_AppliesScrollOffsetToChildHitTesting()
    {
        int topClicks = 0;
        int bottomClicks = 0;

        var document = new UiDocument();
        var scrollView = new UiScrollView("scroll", new TextureHandle(500))
        {
            Width = 100,
            Height = 40,
            ScrollStep = 20
        };
        scrollView.AddChild(new UiButton("top", new TextureHandle(501), "Top", () => topClicks++)
        {
            Width = 80,
            Height = 20
        });
        scrollView.AddChild(new UiButton("bottom", new TextureHandle(502), "Bottom", () => bottomClicks++)
        {
            Y = 50,
            Width = 80,
            Height = 20
        });
        document.AddRoot(scrollView);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueueScroll(10, 10, -1);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        Assert.Equal(20, scrollView.ScrollOffsetY);

        facade.QueuePointerClick(10, 35);
        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(0, topClicks);
        Assert.Equal(1, bottomClicks);
    }

    [Fact]
    public void QueueClick_CanToggleAndFocusInputByElementId()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(600))
        {
            Width = 200,
            Height = 100
        };
        var toggle = new UiToggle("toggle", new TextureHandle(601));
        var input = new UiInputField("input", new TextureHandle(602), new TextureHandle(603))
        {
            Y = 30,
            Width = 120,
            Height = 20
        };
        root.AddChild(toggle);
        root.AddChild(input);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueClick("toggle");
        facade.QueueClick("input");
        facade.QueueTextInput("ok");
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.True(toggle.IsOn);
        Assert.True(input.IsFocused);
        Assert.Equal("ok", input.Text);
    }

    [Fact]
    public void ExtendedControls_ValidateArgumentsAndConstraints()
    {
        Assert.Throws<ArgumentException>(() => new UiToggle("toggle", TextureHandle.Invalid));
        Assert.Throws<ArgumentException>(() => new UiSlider("slider", TextureHandle.Invalid, new TextureHandle(1)));
        Assert.Throws<ArgumentException>(() => new UiSlider("slider", new TextureHandle(1), TextureHandle.Invalid));
        Assert.Throws<ArgumentException>(() => new UiInputField("input", TextureHandle.Invalid, new TextureHandle(2)));
        Assert.Throws<ArgumentException>(() => new UiInputField("input", new TextureHandle(1), TextureHandle.Invalid));
        Assert.Throws<ArgumentException>(() => new UiScrollView("scroll", TextureHandle.Invalid));
        Assert.Throws<ArgumentException>(() => new UiVirtualizedList("list", TextureHandle.Invalid, new TextureHandle(2)));
        Assert.Throws<ArgumentException>(() => new UiVirtualizedList("list", new TextureHandle(1), TextureHandle.Invalid));

        var input = new UiInputField("input", new TextureHandle(10), new TextureHandle(11), text: "abcd");
        Assert.Throws<InvalidOperationException>(() => input.MaxLength = 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => input.MaxLength = 0);

        var list = new UiVirtualizedList("list", new TextureHandle(12), new TextureHandle(13));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ItemHeight = 0);
        Assert.Throws<ArgumentNullException>(() => list.SetItems(null!));

        var facade = new RetainedUiFacade();
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueScroll(float.NaN, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueScroll(0, float.PositiveInfinity, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueueScroll(0, 0, float.NegativeInfinity));
        Assert.Throws<ArgumentNullException>(() => facade.QueueTextInput(null!));

        var scrollDocument = new UiDocument();
        var scrollView = new UiScrollView("scroll", new TextureHandle(20))
        {
            Width = 100,
            Height = 40,
            ScrollStep = 0
        };
        scrollDocument.AddRoot(scrollView);
        var scrollFacade = new RetainedUiFacade(scrollDocument);
        var world = new World();
        scrollFacade.QueueScroll(10, 10, -1);
        Assert.Throws<InvalidDataException>(() => scrollFacade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero)));
    }
}
