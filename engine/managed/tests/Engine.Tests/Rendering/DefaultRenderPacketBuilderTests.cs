using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.Rendering;

public sealed class DefaultRenderPacketBuilderTests
{
    [Fact]
    public void Build_RejectsNullArguments()
    {
        using var arena = new FrameArena(512, 64);
        var builder = DefaultRenderPacketBuilder.Instance;

        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero), arena));

        var world = new World();
        Assert.Throws<ArgumentNullException>(() => builder.Build(world, new FrameTiming(0, TimeSpan.Zero, TimeSpan.Zero), null!));
    }

    [Fact]
    public void Build_AggregatesUiCommandsFromAllBatches()
    {
        var world = new World();
        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();
        EntityId third = world.CreateEntity();

        world.AddComponent(first, new UiRenderBatch([
            new UiDrawCommand(11, 0, 4, 0, 6)
        ]));
        world.AddComponent(second, new UiRenderBatch([
            new UiDrawCommand(22, 4, 8, 6, 12),
            new UiDrawCommand(33, 12, 4, 18, 6)
        ]));
        world.AddComponent(third, new UiRenderBatch(Array.Empty<UiDrawCommand>()));

        using var arena = new FrameArena(2048, 64);
        var packet = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(9, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(64)),
            arena);

        Assert.Equal(9, packet.FrameNumber);
        Assert.Equal(0, packet.NativeDrawItemCount);
        Assert.Equal(3, packet.NativeUiDrawItemCount);

        var byTexture = new Dictionary<ulong, NativeUiDrawItem>(packet.NativeUiDrawItemCount);
        for (var i = 0; i < packet.NativeUiDrawItemCount; i++)
        {
            NativeUiDrawItem item = packet.NativeUiDrawItems[i];
            byTexture[item.Texture] = item;
        }

        Assert.Equal(3, byTexture.Count);
        Assert.Contains((ulong)11, byTexture.Keys);
        Assert.Contains((ulong)22, byTexture.Keys);
        Assert.Contains((ulong)33, byTexture.Keys);

        Assert.Equal((uint)0, byTexture[11].VertexOffset);
        Assert.Equal((uint)4, byTexture[11].VertexCount);
        Assert.Equal((uint)0, byTexture[11].IndexOffset);
        Assert.Equal((uint)6, byTexture[11].IndexCount);

        Assert.Equal((uint)4, byTexture[22].VertexOffset);
        Assert.Equal((uint)8, byTexture[22].VertexCount);
        Assert.Equal((uint)6, byTexture[22].IndexOffset);
        Assert.Equal((uint)12, byTexture[22].IndexCount);

        Assert.Equal((uint)12, byTexture[33].VertexOffset);
        Assert.Equal((uint)4, byTexture[33].VertexCount);
        Assert.Equal((uint)18, byTexture[33].IndexOffset);
        Assert.Equal((uint)6, byTexture[33].IndexCount);
    }

    [Fact]
    public void Build_IgnoresDefaultOrEmptyUiBatches()
    {
        var world = new World();
        EntityId entity = world.CreateEntity();
        world.AddComponent(entity, default(UiRenderBatch));

        using var arena = new FrameArena(1024, 64);
        var packet = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero),
            arena);

        Assert.Equal(0, packet.NativeDrawItemCount);
        Assert.Equal(0, packet.NativeUiDrawItemCount);
        Assert.Equal(IntPtr.Zero, packet.NativeUiDrawItemsPointer);
    }
}
