using System;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.UI;

namespace Engine.Tests.UI;

public sealed class UiTreeDumperTests
{
    [Fact]
    public void DumpTree_IncludesHierarchyAndControlStates()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 220,
            Height = 180
        };
        root.AddChild(new UiToggle("toggle", new TextureHandle(2), isOn: true)
        {
            X = 10,
            Y = 10,
            Width = 20,
            Height = 20
        });
        root.AddChild(new UiSlider("slider", new TextureHandle(3), new TextureHandle(4), value: 0.25f)
        {
            X = 10,
            Y = 40,
            Width = 100,
            Height = 10
        });
        root.AddChild(new UiInputField("input", new TextureHandle(5), new TextureHandle(6), text: "Hi \"Q\"", placeholder: "Line\n2")
        {
            X = 10,
            Y = 60,
            Width = 120,
            Height = 24
        });

        var scrollView = new UiScrollView("scroll", new TextureHandle(7))
        {
            X = 140,
            Y = 10,
            Width = 60,
            Height = 40,
            ScrollStep = 10
        };
        scrollView.AddChild(new UiButton("inner-btn", new TextureHandle(8), "Inner")
        {
            Y = 60,
            Width = 40,
            Height = 20
        });
        root.AddChild(scrollView);

        var list = new UiVirtualizedList("list", new TextureHandle(9), new TextureHandle(10))
        {
            X = 10,
            Y = 100,
            Width = 120,
            Height = 40,
            ItemHeight = 20,
            ScrollStep = 10
        };
        list.SetItems(["one", "two", "three", "four"]);
        root.AddChild(list);

        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueScroll(15, 105, -1.0f);
        facade.QueueScroll(150, 15, -1.0f);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        string dump = facade.DumpTree();
        string[] lines = dump.Split('\n');

        Assert.Equal(7, lines.Length);
        Assert.Contains("UiPanel id=\"root\" visible=true bounds=(0,0,220,180) layout=Absolute", lines[0]);
        Assert.Contains("  UiToggle id=\"toggle\" visible=true bounds=(10,10,20,20) layout=Absolute isOn=true", lines[1]);
        Assert.Contains("  UiSlider id=\"slider\" visible=true bounds=(10,40,100,10) layout=Absolute value=0.25", lines[2]);
        Assert.Contains("  UiInputField id=\"input\" visible=true bounds=(10,60,120,24) layout=Absolute text=\"Hi \\\"Q\\\"\" placeholder=\"Line\\n2\" focused=false", lines[3]);
        Assert.Contains("  UiScrollView id=\"scroll\" visible=true bounds=(140,10,60,40) layout=Absolute scrollY=10 contentHeight=80", lines[4]);
        Assert.Contains("    UiButton id=\"inner-btn\" visible=true bounds=(140,70,40,20) layout=Absolute text=\"Inner\"", lines[5]);
        Assert.Contains("  UiVirtualizedList id=\"list\" visible=true bounds=(10,100,120,40) layout=Absolute items=4 itemHeight=20 scrollY=10 visibleStart=0 visibleCount=3", lines[6]);
    }

    [Fact]
    public void DumpTree_IsDeterministicAndReflectsLayoutChanges()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(11))
        {
            Width = 100,
            Height = 80
        };
        var text = new UiText("caption", new TextureHandle(12), "A")
        {
            X = 5,
            Y = 6,
            Width = 20,
            Height = 10
        };
        root.AddChild(text);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        string first = facade.DumpTree();
        string second = facade.DumpTree();
        Assert.Equal(first, second);

        text.Width = 30;

        string third = facade.DumpTree();
        Assert.NotEqual(first, third);
        Assert.Contains("UiText id=\"caption\" visible=true bounds=(5,6,30,10) layout=Absolute text=\"A\"", third);
    }

    [Fact]
    public void DumpTree_IncludesImageAndListMetadata()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(30))
        {
            Width = 220,
            Height = 150
        };
        root.AddChild(new UiImage("banner", new TextureHandle(31))
        {
            X = 8,
            Y = 6,
            Width = 50,
            Height = 20,
            PreserveAspectRatio = false
        });
        var list = new UiList("list", new TextureHandle(32), new TextureHandle(33))
        {
            X = 10,
            Y = 40,
            Width = 120,
            Height = 40,
            ItemHeight = 20,
            ScrollStep = 10
        };
        list.SetItems(["one", "two", "three"]);
        root.AddChild(list);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueScroll(20, 50, -1.0f);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        string dump = facade.DumpTree();
        Assert.Contains("UiImage id=\"banner\" visible=true bounds=(8,6,50,20) layout=Absolute texture=31 preserveAspect=false", dump);
        Assert.Contains("UiList id=\"list\" visible=true bounds=(10,40,120,40) layout=Absolute items=3 itemHeight=20 scrollY=10 visibleStart=0 visibleCount=3", dump);
    }

    [Fact]
    public void DumpTree_IncludesInvisibleParentsAndChildren()
    {
        var document = new UiDocument();
        var hidden = new UiPanel("hidden", new TextureHandle(20))
        {
            Visible = false
        };
        hidden.AddChild(new UiButton("child", new TextureHandle(21), "Child"));
        document.AddRoot(hidden);

        string dump = new RetainedUiFacade(document).DumpTree();
        string[] lines = dump.Split('\n');

        Assert.Equal(2, lines.Length);
        Assert.Equal("UiPanel id=\"hidden\" visible=false bounds=(0,0,0,0) layout=Absolute", lines[0]);
        Assert.Equal("  UiButton id=\"child\" visible=true bounds=(0,0,0,0) layout=Absolute text=\"Child\"", lines[1]);
    }

    [Fact]
    public void Dump_ThrowsOnNullDocumentAndReturnsEmptyForNoRoots()
    {
        Assert.Throws<ArgumentNullException>(() => UiTreeDumper.Dump(null!));
        Assert.Equal(string.Empty, UiTreeDumper.Dump(new UiDocument()));
    }

    [Fact]
    public void DumpTree_IncludesAnchors_WhenNonDefaultAnchorsAreUsed()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(40))
        {
            Width = 200,
            Height = 100
        };
        root.AddChild(new UiButton("anchored", new TextureHandle(41), "A")
        {
            Width = 30,
            Height = 12,
            X = 10,
            Y = 5,
            Anchors = UiAnchor.Right | UiAnchor.Bottom
        });
        document.AddRoot(root);

        string dump = new RetainedUiFacade(document).DumpTree();

        Assert.Contains("UiButton id=\"anchored\"", dump);
        Assert.Contains("anchors=Right|Bottom", dump);
    }
}
