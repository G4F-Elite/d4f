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
        IReadOnlyList<UiDrawCommand> uiDrawCommands,
        RenderDebugViewMode debugViewMode = RenderDebugViewMode.None)
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
            nativeUiBatch.Count,
            debugViewMode);
    }

    private static NativeBatch MarshalDrawBatch(IReadOnlyList<DrawCommand> drawCommands, FrameArena frameArena)
        => MarshalBatch(drawCommands, frameArena, static command => NativeDrawItem.From(command));

    private static NativeBatch MarshalUiDrawBatch(IReadOnlyList<UiDrawCommand> uiDrawCommands, FrameArena frameArena)
        => MarshalBatch(uiDrawCommands, frameArena, static command => NativeUiDrawItem.From(command));

    private static NativeBatch MarshalBatch<TCommand, TNative>(
        IReadOnlyList<TCommand> commands,
        FrameArena frameArena,
        Func<TCommand, TNative> convert)
        where TNative : unmanaged
    {
        var count = commands.Count;
        if (count == 0)
        {
            return NativeBatch.Empty;
        }

        var nativeItems = frameArena.Alloc<TNative>(count);
        for (var i = 0; i < count; i++)
        {
            nativeItems[i] = convert(commands[i]);
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
