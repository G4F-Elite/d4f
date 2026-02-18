using System;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.Rendering;

public sealed class UiDrawCommandTests
{
    [Fact]
    public void Constructor_StoresAbiFields()
    {
        var command = new UiDrawCommand(new TextureHandle(55), 1, 2, 3, 4);

        Assert.Equal((ulong)55, command.Texture.Value);
        Assert.Equal((uint)1, command.VertexOffset);
        Assert.Equal((uint)2, command.VertexCount);
        Assert.Equal((uint)3, command.IndexOffset);
        Assert.Equal((uint)4, command.IndexCount);
        Assert.Equal(RectF.Empty, command.Bounds);
        Assert.Equal(RectF.Empty, command.ScissorRect);
    }

    [Fact]
    public void Constructor_StoresBounds_WhenProvided()
    {
        RectF bounds = new(10, 20, 30, 40);
        var command = new UiDrawCommand(new TextureHandle(55), 1, 2, 3, 4, bounds);

        Assert.Equal(bounds, command.Bounds);
        Assert.Equal(bounds, command.ScissorRect);
    }

    [Fact]
    public void Constructor_StoresCustomScissor_WhenProvided()
    {
        RectF bounds = new(10, 20, 30, 40);
        RectF scissor = new(12, 24, 15, 16);
        var command = new UiDrawCommand(new TextureHandle(55), 1, 2, 3, 4, bounds, scissor);

        Assert.Equal(bounds, command.Bounds);
        Assert.Equal(scissor, command.ScissorRect);
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() => new UiDrawCommand(TextureHandle.Invalid, 0, 1, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiDrawCommand(new TextureHandle(1), 0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiDrawCommand(new TextureHandle(1), 0, 1, 0, 0));
    }

    [Fact]
    public void Constructor_RejectsNonFiniteBoundsAndScissor()
    {
        RectF valid = new(0, 0, 10, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UiDrawCommand(new TextureHandle(1), 0, 1, 0, 1, new RectF(float.NaN, 0, 10, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UiDrawCommand(new TextureHandle(1), 0, 1, 0, 1, new RectF(0, float.PositiveInfinity, 10, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UiDrawCommand(new TextureHandle(1), 0, 1, 0, 1, valid, new RectF(0, 0, float.NaN, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UiDrawCommand(new TextureHandle(1), 0, 1, 0, 1, valid, new RectF(0, 0, 10, float.NegativeInfinity)));
    }
}
