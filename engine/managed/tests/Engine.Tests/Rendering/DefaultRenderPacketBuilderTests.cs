using System;
using System.Collections.Generic;
using System.Numerics;
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
    public void Build_PropagatesRenderSettingsToPacket()
    {
        var world = new World();
        using var arena = new FrameArena(1024, 64);

        RenderPacket packet = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(3, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(48)),
            arena,
            new RenderSettings(
                RenderDebugViewMode.Albedo,
                RenderFeatureFlags.DisableAutoExposure | RenderFeatureFlags.DisableJitterEffects));

        Assert.Equal(RenderDebugViewMode.Albedo, packet.DebugViewMode);
        Assert.True(packet.DisableAutoExposure);
        Assert.True(packet.DisableJitterEffects);
        Assert.Equal(
            RenderFeatureFlags.DisableAutoExposure | RenderFeatureFlags.DisableJitterEffects,
            packet.FeatureFlags);
    }

    [Fact]
    public void Build_AggregatesDrawCommandsFromRenderMeshInstances()
    {
        var world = new World();
        EntityId third = world.CreateEntity();
        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();

        world.AddComponent(third, new RenderMeshInstance(
            new MeshHandle(300),
            new MaterialHandle(30),
            new TextureHandle(3),
            CreateWorldMatrix(30),
            sortKeyHigh: 3,
            sortKeyLow: 30));
        world.AddComponent(first, new RenderMeshInstance(
            new MeshHandle(100),
            new MaterialHandle(10),
            new TextureHandle(1),
            CreateWorldMatrix(10),
            sortKeyHigh: 1,
            sortKeyLow: 10));
        world.AddComponent(second, new RenderMeshInstance(
            new MeshHandle(200),
            new MaterialHandle(20),
            new TextureHandle(2),
            CreateWorldMatrix(20),
            sortKeyHigh: 2,
            sortKeyLow: 20));

        using var arena = new FrameArena(2048, 64);
        RenderPacket packet = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(5, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(80)),
            arena);

        Assert.Equal(3, packet.NativeDrawItemCount);
        Assert.Equal((ulong)100, packet.NativeDrawItems[0].Mesh);
        Assert.Equal((ulong)200, packet.NativeDrawItems[1].Mesh);
        Assert.Equal((ulong)300, packet.NativeDrawItems[2].Mesh);

        Assert.Equal((uint)1, packet.DrawCommands[0].SortKeyHigh);
        Assert.Equal((uint)2, packet.DrawCommands[1].SortKeyHigh);
        Assert.Equal((uint)3, packet.DrawCommands[2].SortKeyHigh);

        Assert.Equal(11.0f, packet.NativeDrawItems[0].World00);
        Assert.Equal(34.0f, packet.NativeDrawItems[2].World03);
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

    [Fact]
    public void Build_IgnoresDefaultRenderMeshInstances()
    {
        var world = new World();
        EntityId invalidEntity = world.CreateEntity();
        EntityId validEntity = world.CreateEntity();
        world.AddComponent(invalidEntity, default(RenderMeshInstance));
        world.AddComponent(validEntity, new RenderMeshInstance(new MeshHandle(10), new MaterialHandle(20), new TextureHandle(30)));

        using var arena = new FrameArena(1024, 64);
        RenderPacket packet = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero),
            arena);

        Assert.Equal(1, packet.NativeDrawItemCount);
        Assert.Equal((ulong)10, packet.NativeDrawItems[0].Mesh);
    }

    [Fact]
    public void Build_ClearsReusableBuffersBetweenCalls()
    {
        var world = new World();
        EntityId renderEntity = world.CreateEntity();
        EntityId uiEntity = world.CreateEntity();
        world.AddComponent(renderEntity, new RenderMeshInstance(
            new MeshHandle(7),
            new MaterialHandle(8),
            new TextureHandle(9)));
        world.AddComponent(uiEntity, new UiRenderBatch([
            new UiDrawCommand(texture: 15, vertexOffset: 0, vertexCount: 4, indexOffset: 0, indexCount: 6)
        ]));

        using var firstArena = new FrameArena(1024, 64);
        RenderPacket first = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(1, TimeSpan.Zero, TimeSpan.Zero),
            firstArena);
        Assert.Equal(1, first.NativeDrawItemCount);
        Assert.Equal(1, first.NativeUiDrawItemCount);

        Assert.True(world.RemoveComponent<RenderMeshInstance>(renderEntity));
        Assert.True(world.RemoveComponent<UiRenderBatch>(uiEntity));

        using var secondArena = new FrameArena(1024, 64);
        RenderPacket second = DefaultRenderPacketBuilder.Instance.Build(
            world,
            new FrameTiming(2, TimeSpan.Zero, TimeSpan.Zero),
            secondArena);
        Assert.Equal(0, second.NativeDrawItemCount);
        Assert.Equal(0, second.NativeUiDrawItemCount);
    }

    private static Matrix4x4 CreateWorldMatrix(float start)
        => new(
            start + 1.0f, start + 2.0f, start + 3.0f, start + 4.0f,
            start + 5.0f, start + 6.0f, start + 7.0f, start + 8.0f,
            start + 9.0f, start + 10.0f, start + 11.0f, start + 12.0f,
            start + 13.0f, start + 14.0f, start + 15.0f, start + 16.0f);
}
