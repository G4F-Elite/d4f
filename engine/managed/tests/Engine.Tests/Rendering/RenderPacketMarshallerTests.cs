using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.Rendering;

public sealed class RenderPacketMarshallerTests
{
    [Fact]
    public void Marshal_ThrowsWhenDrawCommandsListIsNull()
    {
        using var arena = new FrameArena(256, 64);
        Assert.Throws<ArgumentNullException>(() => RenderPacketMarshaller.Marshal(0, arena, null!, Array.Empty<UiDrawCommand>()));
    }

    [Fact]
    public void Marshal_ThrowsWhenUiCommandsListIsNull()
    {
        using var arena = new FrameArena(256, 64);
        Assert.Throws<ArgumentNullException>(() => RenderPacketMarshaller.Marshal(0, arena, Array.Empty<DrawCommand>(), null!));
    }

    [Fact]
    public void CreateNative_RejectsInvalidNativeDrawPair()
    {
        Assert.Throws<ArgumentException>(() => RenderPacket.CreateNative(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            IntPtr.Zero,
            1,
            IntPtr.Zero,
            0));

        Assert.Throws<ArgumentException>(() => RenderPacket.CreateNative(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            new IntPtr(1),
            0,
            IntPtr.Zero,
            0));

        Assert.Throws<ArgumentOutOfRangeException>(() => RenderPacket.CreateNative(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            IntPtr.Zero,
            -1,
            IntPtr.Zero,
            0));
    }

    [Fact]
    public void CreateNative_RejectsInvalidNativeUiPair()
    {
        Assert.Throws<ArgumentException>(() => RenderPacket.CreateNative(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            1));

        Assert.Throws<ArgumentException>(() => RenderPacket.CreateNative(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            IntPtr.Zero,
            0,
            new IntPtr(1),
            0));
    }

    [Fact]
    public void Marshal_ThrowsWhenArenaOutOfMemory()
    {
        using var arena = new FrameArena(64, 16);
        var drawCommands = CreateDrawCommands(8);

        var error = Assert.Throws<InvalidOperationException>(
            () => RenderPacketMarshaller.Marshal(1, arena, drawCommands, Array.Empty<UiDrawCommand>()));

        Assert.Contains("out of memory", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marshal_WritesNativeDataIntoFrameArena()
    {
        using var arena = new FrameArena(1024, 64);
        IReadOnlyList<DrawCommand> drawCommands =
        [
            CreateDrawCommand(1, 7, 11, 12, 13),
            CreateDrawCommand(2, 8, 21, 22, 23)
        ];
        IReadOnlyList<UiDrawCommand> uiCommands =
        [
            new UiDrawCommand(101, 12, 3, 8)
        ];

        var packet = RenderPacketMarshaller.Marshal(42, arena, drawCommands, uiCommands);

        Assert.Equal(42, packet.FrameNumber);
        Assert.Equal(2, packet.NativeDrawItemCount);
        Assert.NotEqual(IntPtr.Zero, packet.NativeDrawItemsPointer);
        Assert.Equal(1, packet.NativeUiDrawItemCount);
        Assert.NotEqual(IntPtr.Zero, packet.NativeUiDrawItemsPointer);

        var draw0 = packet.NativeDrawItems[0];
        Assert.Equal(1, draw0.EntityIndex);
        Assert.Equal((uint)7, draw0.EntityGeneration);
        Assert.Equal((uint)11, draw0.Mesh);
        Assert.Equal((uint)12, draw0.Material);
        Assert.Equal((uint)13, draw0.Texture);

        var ui0 = packet.NativeUiDrawItems[0];
        Assert.Equal((uint)101, ui0.DrawListId);
        Assert.Equal((uint)12, ui0.TextureId);
        Assert.Equal(3, ui0.IndexOffset);
        Assert.Equal(8, ui0.ElementCount);
    }

    [Fact]
    public void EmptyPacket_HasNoNativeData()
    {
        var packet = RenderPacket.Empty(0);
        Assert.Equal(0, packet.NativeDrawItemCount);
        Assert.Equal(IntPtr.Zero, packet.NativeDrawItemsPointer);
        Assert.Equal(0, packet.NativeUiDrawItemCount);
        Assert.Equal(IntPtr.Zero, packet.NativeUiDrawItemsPointer);
    }

    private static DrawCommand CreateDrawCommand(int entityIndex, uint generation, uint mesh, uint material, uint texture)
        => new(
            new EntityId(entityIndex, generation),
            new MeshHandle(mesh),
            new MaterialHandle(material),
            new TextureHandle(texture));

    private static IReadOnlyList<DrawCommand> CreateDrawCommands(int count)
    {
        var commands = new DrawCommand[count];
        for (var i = 0; i < count; i++)
        {
            var id = checked((uint)(i + 1));
            commands[i] = CreateDrawCommand(i, 1, id, id + 100, id + 200);
        }

        return commands;
    }
}
