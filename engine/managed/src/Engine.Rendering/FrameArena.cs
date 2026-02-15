using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine.Rendering;

public sealed unsafe class FrameArena : IDisposable
{
    private readonly nuint _capacity;
    private readonly nuint _alignment;
    private byte* _buffer;
    private nuint _offset;

    public FrameArena(int capacityBytes, int alignment)
    {
        if (capacityBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityBytes), "Capacity must be positive.");
        }

        if (alignment <= 0 || !IsPowerOfTwo((nuint)alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be a positive power of two.");
        }

        _capacity = (nuint)capacityBytes;
        _alignment = (nuint)alignment;
        _buffer = (byte*)NativeMemory.AlignedAlloc(_capacity, _alignment);

        if (_buffer is null)
        {
            throw new OutOfMemoryException($"Failed to allocate frame arena with capacity {_capacity} bytes.");
        }
    }

    public int CapacityBytes => checked((int)_capacity);

    public int UsedBytes
    {
        get
        {
            ThrowIfDisposed();
            return checked((int)_offset);
        }
    }

    public IntPtr BasePointer
    {
        get
        {
            ThrowIfDisposed();
            return (IntPtr)_buffer;
        }
    }

    public Span<T> Alloc<T>(int count) where T : unmanaged
    {
        ThrowIfDisposed();

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
        }

        if (count == 0)
        {
            return Span<T>.Empty;
        }

        var bytes = checked((nuint)Unsafe.SizeOf<T>() * (nuint)count);
        var elementAlignment = (nuint)Math.Min(Unsafe.SizeOf<T>(), checked((int)_alignment));
        var alignment = elementAlignment == 0 ? (nuint)1 : elementAlignment;
        var alignedOffset = AlignUp(_offset, alignment);

        if (alignedOffset > _capacity || bytes > _capacity - alignedOffset)
        {
            throw new InvalidOperationException(
                $"Frame arena out of memory. Requested {bytes} bytes, available {_capacity - _offset} bytes.");
        }

        var pointer = _buffer + alignedOffset;
        _offset = alignedOffset + bytes;
        return new Span<T>(pointer, count);
    }

    public void Reset()
    {
        ThrowIfDisposed();
        _offset = 0;
    }

    public void Dispose()
    {
        if (_buffer is null)
        {
            return;
        }

        NativeMemory.AlignedFree(_buffer);
        _buffer = null;
        _offset = 0;
    }

    private static nuint AlignUp(nuint value, nuint alignment)
    {
        var mask = alignment - 1;
        return (value + mask) & ~mask;
    }

    private static bool IsPowerOfTwo(nuint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private void ThrowIfDisposed()
    {
        if (_buffer is null)
        {
            throw new ObjectDisposedException(nameof(FrameArena));
        }
    }
}
