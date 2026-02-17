using System;
using System.Collections.Generic;
using System.Numerics;
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
    public void CreateNative_RejectsInvalidDebugViewMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RenderPacket.CreateNative(
            0,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            (RenderDebugViewMode)255));
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
            CreateDrawCommand(1, 7, 11, 12, 13, CreateWorldMatrix(10), 100, 200),
            CreateDrawCommand(2, 8, 21, 22, 23, CreateWorldMatrix(20), 300, 400)
        ];
        IReadOnlyList<UiDrawCommand> uiCommands =
        [
            new UiDrawCommand(12, 4, 6, 3, 8)
        ];

        var packet = RenderPacketMarshaller.Marshal(
            42,
            arena,
            drawCommands,
            uiCommands,
            RenderDebugViewMode.Normals);

        Assert.Equal(42, packet.FrameNumber);
        Assert.Equal(RenderDebugViewMode.Normals, packet.DebugViewMode);
        Assert.Equal(2, packet.NativeDrawItemCount);
        Assert.NotEqual(IntPtr.Zero, packet.NativeDrawItemsPointer);
        Assert.Equal(1, packet.NativeUiDrawItemCount);
        Assert.NotEqual(IntPtr.Zero, packet.NativeUiDrawItemsPointer);

        var draw0 = packet.NativeDrawItems[0];
        Assert.Equal((ulong)11, draw0.Mesh);
        Assert.Equal((ulong)12, draw0.Material);
        Assert.Equal(11.0f, draw0.World00);
        Assert.Equal(12.0f, draw0.World01);
        Assert.Equal(13.0f, draw0.World02);
        Assert.Equal(14.0f, draw0.World03);
        Assert.Equal(100u, draw0.SortKeyHigh);
        Assert.Equal(200u, draw0.SortKeyLow);

        var ui0 = packet.NativeUiDrawItems[0];
        Assert.Equal((ulong)12, ui0.Texture);
        Assert.Equal((uint)4, ui0.VertexOffset);
        Assert.Equal((uint)6, ui0.VertexCount);
        Assert.Equal((uint)3, ui0.IndexOffset);
        Assert.Equal((uint)8, ui0.IndexCount);
    }

    [Fact]
    public void EmptyPacket_HasNoNativeData()
    {
        var packet = RenderPacket.Empty(0);
        Assert.Equal(0, packet.NativeDrawItemCount);
        Assert.Equal(IntPtr.Zero, packet.NativeDrawItemsPointer);
        Assert.Equal(0, packet.NativeUiDrawItemCount);
        Assert.Equal(IntPtr.Zero, packet.NativeUiDrawItemsPointer);
        Assert.Equal(RenderDebugViewMode.None, packet.DebugViewMode);
    }

    private static DrawCommand CreateDrawCommand(
        int entityIndex,
        uint generation,
        uint mesh,
        uint material,
        uint texture,
        Matrix4x4 worldMatrix,
        uint sortKeyHigh,
        uint sortKeyLow)
        => new(
            new EntityId(entityIndex, generation),
            new MeshHandle(mesh),
            new MaterialHandle(material),
            new TextureHandle(texture),
            worldMatrix,
            sortKeyHigh,
            sortKeyLow);

    private static IReadOnlyList<DrawCommand> CreateDrawCommands(int count)
    {
        var commands = new DrawCommand[count];
        for (var i = 0; i < count; i++)
        {
            var id = checked((uint)(i + 1));
            commands[i] = CreateDrawCommand(i, 1, id, id + 100, id + 200, Matrix4x4.Identity, id + 100, id);
        }

        return commands;
    }

    private static Matrix4x4 CreateWorldMatrix(float start)
        => new(
            start + 1.0f, start + 2.0f, start + 3.0f, start + 4.0f,
            start + 5.0f, start + 6.0f, start + 7.0f, start + 8.0f,
            start + 9.0f, start + 10.0f, start + 11.0f, start + 12.0f,
            start + 13.0f, start + 14.0f, start + 15.0f, start + 16.0f);
}
