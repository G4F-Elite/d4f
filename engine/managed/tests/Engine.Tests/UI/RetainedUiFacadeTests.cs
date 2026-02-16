using System;
using System.Linq;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;
using Engine.UI;
using Xunit;

namespace Engine.Tests.UI;

public sealed class RetainedUiFacadeTests
{
    [Fact]
    public void Update_BuildsUiDrawCommandsWithMonotonicOffsets()
    {
        var document = new UiDocument();
        var panel = new UiPanel("panel", new TextureHandle(10))
        {
            X = 10,
            Y = 20,
            Width = 200,
            Height = 120,
            Padding = new UiThickness(5, 6, 0, 0)
        };
        panel.AddChild(new UiText("title", new TextureHandle(20), "AB")
        {
            X = 2,
            Y = 3,
            Width = 40,
            Height = 12
        });
        panel.AddChild(new UiButton("start", new TextureHandle(30), "Start")
        {
            X = 4,
            Y = 20,
            Width = 60,
            Height = 24
        });
        document.AddRoot(panel);

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        var batches = world.Query<UiRenderBatch>().ToList();
        UiRenderBatch batch = Assert.Single(batches).Component;
        Assert.Equal(3, batch.Commands.Count);

        UiDrawCommand panelCommand = batch.Commands[0];
        Assert.Equal((uint)10, panelCommand.Texture.Value);
        Assert.Equal((uint)0, panelCommand.VertexOffset);
        Assert.Equal((uint)4, panelCommand.VertexCount);
        Assert.Equal((uint)0, panelCommand.IndexOffset);
        Assert.Equal((uint)6, panelCommand.IndexCount);
        Assert.Equal(new RectF(10, 20, 200, 120), panelCommand.Bounds);

        UiDrawCommand textCommand = batch.Commands[1];
        Assert.Equal((uint)20, textCommand.Texture.Value);
        Assert.Equal((uint)4, textCommand.VertexOffset);
        Assert.Equal((uint)8, textCommand.VertexCount);
        Assert.Equal((uint)6, textCommand.IndexOffset);
        Assert.Equal((uint)12, textCommand.IndexCount);
        Assert.Equal(new RectF(17, 29, 40, 12), textCommand.Bounds);

        UiDrawCommand buttonCommand = batch.Commands[2];
        Assert.Equal((uint)30, buttonCommand.Texture.Value);
        Assert.Equal((uint)12, buttonCommand.VertexOffset);
        Assert.Equal((uint)4, buttonCommand.VertexCount);
        Assert.Equal((uint)18, buttonCommand.IndexOffset);
        Assert.Equal((uint)6, buttonCommand.IndexCount);
        Assert.Equal(new RectF(19, 46, 60, 24), buttonCommand.Bounds);
    }

    [Fact]
    public void Update_SkipsInvisibleAndEmptyTextElements()
    {
        var document = new UiDocument();
        document.AddRoot(new UiPanel("hidden-panel", new TextureHandle(10)) { Visible = false });
        document.AddRoot(new UiText("empty-text", new TextureHandle(20), string.Empty));
        document.AddRoot(new UiButton("visible-button", new TextureHandle(30), "OK"));

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        UiRenderBatch batch = Assert.Single(world.Query<UiRenderBatch>()).Component;
        UiDrawCommand command = Assert.Single(batch.Commands);
        Assert.Equal((uint)30, command.Texture.Value);
        Assert.Equal((uint)0, command.VertexOffset);
        Assert.Equal((uint)4, command.VertexCount);
    }

    [Fact]
    public void QueueClick_InvokesMatchingButtonCallbacksInQueuedOrder()
    {
        var clickCount = 0;
        var document = new UiDocument();
        document.AddRoot(new UiButton("start", new TextureHandle(44), "Start", () => clickCount++));

        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.QueueClick("start");
        facade.QueueClick("missing");
        facade.QueueClick("start");
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(2, clickCount);

        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));
        Assert.Equal(2, clickCount);
    }

    [Fact]
    public void QueuePointerClick_UsesLayoutHitTestingAndTopmostOrder()
    {
        var frontClicks = 0;
        var backClicks = 0;
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 120,
            Height = 120
        };
        root.AddChild(new UiButton("back", new TextureHandle(2), "Back", () => backClicks++)
        {
            X = 0,
            Y = 0,
            Width = 50,
            Height = 50
        });
        root.AddChild(new UiButton("front", new TextureHandle(3), "Front", () => frontClicks++)
        {
            X = 10,
            Y = 10,
            Width = 50,
            Height = 50
        });
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.QueuePointerClick(20, 20); // overlap, should hit front
        facade.QueuePointerClick(4, 4);   // only back
        facade.QueuePointerClick(100, 100); // no button hit
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(1, frontClicks);
        Assert.Equal(1, backClicks);
    }

    [Fact]
    public void Update_ComputesVerticalStackLayoutForChildren()
    {
        var document = new UiDocument();
        var root = new UiPanel("root", new TextureHandle(1))
        {
            Width = 100,
            Height = 100,
            LayoutMode = UiLayoutMode.VerticalStack,
            LayoutGap = 5,
            Padding = new UiThickness(10, 10, 10, 10)
        };
        var first = new UiButton("first", new TextureHandle(2), "First")
        {
            Height = 10,
            Margin = new UiThickness(2, 1, 3, 4)
        };
        var second = new UiButton("second", new TextureHandle(3), "Second")
        {
            Height = 10
        };
        root.AddChild(first);
        root.AddChild(second);
        document.AddRoot(root);

        var facade = new RetainedUiFacade(document);
        var world = new World();
        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));

        Assert.Equal(new RectF(12, 11, 75, 10), first.LayoutBounds);
        Assert.Equal(new RectF(10, 30, 80, 10), second.LayoutBounds);
    }

    [Fact]
    public void QueueClick_RejectsInvalidElementId()
    {
        var facade = new RetainedUiFacade();

        Assert.Throws<ArgumentException>(() => facade.QueueClick(string.Empty));
        Assert.Throws<ArgumentException>(() => facade.QueueClick("  "));
        Assert.Throws<ArgumentException>(() => facade.QueueClick(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerClick(float.NaN, 0.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => facade.QueuePointerClick(0.0f, float.PositiveInfinity));
    }

    [Fact]
    public void Update_RecreatesRenderBatchEntityAfterEntityDestroy()
    {
        var document = new UiDocument();
        document.AddRoot(new UiPanel("panel", new TextureHandle(10)));
        var facade = new RetainedUiFacade(document);
        var world = new World();

        facade.Update(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero));
        var firstPass = world.Query<UiRenderBatch>().Single();
        EntityId firstEntity = firstPass.Entity;
        world.DestroyEntity(firstEntity);

        facade.Update(world, new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero));
        var secondPass = world.Query<UiRenderBatch>().Single();

        Assert.NotEqual(firstEntity, secondPass.Entity);
        Assert.True(world.IsAlive(secondPass.Entity));
        Assert.Single(secondPass.Component.Commands);
    }

    [Fact]
    public void Elements_ValidateArgumentsAndParentRules()
    {
        Assert.Throws<ArgumentException>(() => new UiPanel("p", TextureHandle.Invalid));
        Assert.Throws<ArgumentException>(() => new UiText("t", TextureHandle.Invalid, "hello"));
        Assert.Throws<ArgumentException>(() => new UiButton("b", TextureHandle.Invalid, "click"));

        var parent = new UiPanel("parent", new TextureHandle(1));
        var child = new UiPanel("child", new TextureHandle(2));
        parent.AddChild(child);

        Assert.Throws<InvalidOperationException>(() => parent.AddChild(parent));
        Assert.Throws<InvalidOperationException>(() => new UiPanel("other", new TextureHandle(3)).AddChild(child));

        Assert.True(parent.RemoveChild(child));
        Assert.False(parent.RemoveChild(child));

        Assert.Throws<ArgumentOutOfRangeException>(() => parent.LayoutGap = -1.0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.MinWidth = -1.0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.MinHeight = -1.0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.MaxWidth = -1.0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.MaxHeight = -1.0f);
        parent.MaxWidth = 10;
        Assert.Throws<ArgumentOutOfRangeException>(() => parent.MinWidth = 11);
    }
}
