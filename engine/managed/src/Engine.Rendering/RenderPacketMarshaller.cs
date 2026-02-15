using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine.Rendering;

public static unsafe class RenderPacketMarshaller
{
    public static RenderPacket Marshal(
        long frameNumber,
        FrameArena frameArena,
        IReadOnlyList<DrawCommand> drawCommands,
        IReadOnlyList<UiDrawCommand> uiDrawCommands)
    {
        ArgumentNullException.ThrowIfNull(frameArena);
        ArgumentNullException.ThrowIfNull(drawCommands);
        ArgumentNullException.ThrowIfNull(uiDrawCommands);

        var nativeDrawBatch = MarshalDrawBatch(drawCommands, frameArena);
        var nativeUiBatch = MarshalUiDrawBatch(uiDrawCommands, frameArena);

        return RenderPacket.CreateNative(
            frameNumber,
            drawCommands,
            uiDrawCommands,
            nativeDrawBatch.Pointer,
            nativeDrawBatch.Count,
            nativeUiBatch.Pointer,
            nativeUiBatch.Count);
    }

    private static NativeBatch MarshalDrawBatch(IReadOnlyList<DrawCommand> drawCommands, FrameArena frameArena)
    {
        var count = drawCommands.Count;
        if (count == 0)
        {
            return NativeBatch.Empty;
        }

        var nativeItems = frameArena.Alloc<NativeDrawItem>(count);
        for (var i = 0; i < count; i++)
        {
            nativeItems[i] = NativeDrawItem.From(drawCommands[i]);
        }

        return new NativeBatch(GetSpanPointer(nativeItems), count);
    }

    private static NativeBatch MarshalUiDrawBatch(IReadOnlyList<UiDrawCommand> uiDrawCommands, FrameArena frameArena)
    {
        var count = uiDrawCommands.Count;
        if (count == 0)
        {
            return NativeBatch.Empty;
        }

        var nativeItems = frameArena.Alloc<NativeUiDrawItem>(count);
        for (var i = 0; i < count; i++)
        {
            nativeItems[i] = NativeUiDrawItem.From(uiDrawCommands[i]);
        }

        return new NativeBatch(GetSpanPointer(nativeItems), count);
    }

    private static IntPtr GetSpanPointer<T>(Span<T> span) where T : unmanaged
    {
        ref var first = ref MemoryMarshal.GetReference(span);
        return (IntPtr)Unsafe.AsPointer(ref first);
    }

    private readonly struct NativeBatch
    {
        public static NativeBatch Empty => default;

        public NativeBatch(IntPtr pointer, int count)
        {
            Pointer = pointer;
            Count = count;
        }

        public IntPtr Pointer { get; }

        public int Count { get; }
    }
}
