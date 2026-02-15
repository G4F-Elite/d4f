using System;
using System.Runtime.InteropServices;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.Rendering;

public sealed class FrameArenaTests
{
    [Fact]
    public void Constructor_ValidatesCapacityAndAlignment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameArena(0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameArena(128, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameArena(128, 3));
    }

    [Fact]
    public void Alloc_RespectsCapacityAndSupportsReset()
    {
        using var arena = new FrameArena(256, 64);

        var first = arena.Alloc<int>(8);
        Assert.Equal(8, first.Length);
        Assert.True(arena.UsedBytes >= 32);

        var usedAfterFirstAlloc = arena.UsedBytes;
        var second = arena.Alloc<float>(4);
        Assert.Equal(4, second.Length);
        Assert.True(arena.UsedBytes > usedAfterFirstAlloc);

        arena.Reset();
        Assert.Equal(0, arena.UsedBytes);

        var third = arena.Alloc<long>(2);
        Assert.Equal(2, third.Length);
        Assert.True(arena.UsedBytes >= 16);
    }

    [Fact]
    public void Alloc_ThrowsWhenOutOfMemory()
    {
        using var arena = new FrameArena(64, 16);

        _ = arena.Alloc<byte>(64);
        var error = Assert.Throws<InvalidOperationException>(() => arena.Alloc<byte>(1));
        Assert.Contains("out of memory", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisposedArena_RejectsUsage()
    {
        var arena = new FrameArena(64, 16);
        arena.Dispose();

        Assert.Throws<ObjectDisposedException>(() => arena.Reset());
        Assert.Throws<ObjectDisposedException>(() => arena.Alloc<byte>(1));
        Assert.Throws<ObjectDisposedException>(() => _ = arena.BasePointer);
    }

    [Fact]
    public void WrapExternalMemory_ValidatesArguments()
    {
        Assert.Throws<ArgumentException>(() => FrameArena.WrapExternalMemory(IntPtr.Zero, 64, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameArena.WrapExternalMemory(new IntPtr(16), 0, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameArena.WrapExternalMemory(new IntPtr(16), 64, 3));
        Assert.Throws<ArgumentException>(() => FrameArena.WrapExternalMemory(new IntPtr(3), 64, 4));
    }

    [Fact]
    public void WrapExternalMemory_DoesNotOwnWrappedBuffer()
    {
        var memory = Marshal.AllocHGlobal(128);

        try
        {
            using (var arena = FrameArena.WrapExternalMemory(memory, 128, 1))
            {
                var bytes = arena.Alloc<byte>(4);
                bytes[0] = 10;
                bytes[1] = 20;
                bytes[2] = 30;
                bytes[3] = 40;
            }

            Marshal.WriteByte(memory, 0, 99);
            Assert.Equal(99, Marshal.ReadByte(memory, 0));
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }
}
