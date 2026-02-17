using System;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;

namespace Engine.Tests.Rendering;

public sealed class DefaultRenderPacketBuilderDeterminismTests
{
    [Fact]
    public void Build_OrdersUiBatchesByEntityIdRegardlessOfInsertionOrder()
    {
        var world = new World();
        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();
        EntityId third = world.CreateEntity();

        world.AddComponent(third, new UiRenderBatch([new UiDrawCommand(300, 0, 4, 0, 6)]));
        world.AddComponent(first, new UiRenderBatch([new UiDrawCommand(100, 4, 4, 6, 6)]));
        world.AddComponent(second, new UiRenderBatch([new UiDrawCommand(200, 8, 4, 12, 6)]));

        using var arena = new FrameArena(2048, 64);
        RenderPacket packet = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(3, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(32)),
            arena);

        Assert.Equal(3, packet.NativeUiDrawItemCount);
        Assert.Equal((ulong)100, packet.NativeUiDrawItems[0].Texture);
        Assert.Equal((ulong)200, packet.NativeUiDrawItems[1].Texture);
        Assert.Equal((ulong)300, packet.NativeUiDrawItems[2].Texture);
    }
}
