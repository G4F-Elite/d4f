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

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadeImageListTests
{
    [Fact]
    public void Update_BuildsDrawCommandsForImageAndListWithMonotonicOffsets()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 300,
            Height = 220
        };
        root.AddChild(new UiImage("logo", new TextureHandle(2))
        {
            X = 10,
            Y = 10,
            Width = 80,
            Height = 40
        });

        var list = new UiList("list", new TextureHandle(3), new TextureHandle(4))
        {
            X = 10,
            Y = 60,
            Width = 120,
            Height = 60,
            ItemHeight = 20
        };
        list.SetItems(["one", string.Empty, "three"]);
        root.AddChild(list);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        UiRenderBatch batch = Assert.Single(world.Query<UiRenderBatch>()).Component;
        Assert.Equal(7, batch.Commands.Count);
        Assert.Equal(new RectF(0, 0, 300, 220), batch.Commands[0].Bounds);
        Assert.Equal(new RectF(10, 10, 80, 40), batch.Commands[1].Bounds);
        Assert.Equal(new RectF(10, 60, 120, 20), batch.Commands[2].Bounds);
        Assert.Equal(new RectF(10, 80, 120, 20), batch.Commands[4].Bounds);
        Assert.Equal(new RectF(10, 100, 120, 20), batch.Commands[5].Bounds);
        Assert.Equal((ulong)1, batch.Commands[0].Texture.Value);
        Assert.Equal((ulong)2, batch.Commands[1].Texture.Value);
        Assert.Equal((ulong)3, batch.Commands[2].Texture.Value);
        Assert.Equal((ulong)4, batch.Commands[3].Texture.Value);
        Assert.Equal((ulong)3, batch.Commands[4].Texture.Value);
        Assert.Equal((ulong)3, batch.Commands[5].Texture.Value);
        Assert.Equal((ulong)4, batch.Commands[6].Texture.Value);

        for (int i = 1; i < batch.Commands.Count; i++)
        {
            UiDrawCommand previous = batch.Commands[i - 1];
            UiDrawCommand current = batch.Commands[i];
            Assert.Equal(previous.VertexOffset + previous.VertexCount, current.VertexOffset);
            Assert.Equal(previous.IndexOffset + previous.IndexCount, current.IndexOffset);
        }
    }

    [Fact]
    public void QueuePointerClick_InvokesUiListItemsAndImageRemainsPassive()
    {
        var clickedItems = new List<(int Index, string Value)>();
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(10))
        {
            Width = 220,
            Height = 160
        };
        root.AddChild(new UiImage("hero", new TextureHandle(11))
        {
            X = 10,
            Y = 10,
            Width = 80,
            Height = 40
        });
        var list = new UiList("list", new TextureHandle(12), new TextureHandle(13))
        {
            X = 10,
            Y = 60,
            Width = 120,
            Height = 40
        };
        list.SetItems(["a", "b", "c"]);
        list.OnItemClick = (index, value) => clickedItems.Add((index, value));
        root.AddChild(list);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerClick(20, 20);
        facade.QueuePointerClick(20, 65);
        facade.QueuePointerClick(20, 85);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal([(0, "a"), (1, "b")], clickedItems);
    }

    [Fact]
    public void QueueScroll_ScrollsUiListAndShiftsDrawBounds()
    {
        var document = new UiDocument();
        var list = new UiList("list", new TextureHandle(20), new TextureHandle(21))
        {
            Width = 120,
            Height = 40,
            ItemHeight = 20,
            ScrollStep = 10
        };
        list.SetItems(["0", "1", "2", "3"]);
        document.AddRoot(list);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueScroll(10, 10, -1.0f);
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(10f, list.ScrollOffsetY);
        UiRenderBatch batch = Assert.Single(world.Query<UiRenderBatch>()).Component;
        Assert.Equal(new RectF(0, -10, 120, 20), batch.Commands[0].Bounds);
    }

    [Fact]
    public void ImageAndList_ValidateArgumentsAndRuntimeConstraints()
    {
        Assert.Throws<ArgumentException>(() => new UiImage("image", TextureHandle.Invalid));
        Assert.Throws<ArgumentException>(() => new UiList("list", TextureHandle.Invalid, new TextureHandle(1)));
        Assert.Throws<ArgumentException>(() => new UiList("list", new TextureHandle(1), TextureHandle.Invalid));

        var list = new UiList("list", new TextureHandle(2), new TextureHandle(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ItemHeight = 0f);
        Assert.Throws<ArgumentNullException>(() => list.SetItems(null!));

        list.SetItems(["0", "1"]);
        list.ScrollStep = 0f;

        var document = new UiDocument();
        document.AddRoot(list);
        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueueScroll(0, 0, -1f);

        Assert.Throws<InvalidDataException>(() => facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero)));
    }
}
