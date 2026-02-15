using System;
using System.Collections.Generic;

namespace Engine.Rendering;

public sealed unsafe class RenderPacket
{
    public RenderPacket(long frameNumber, IReadOnlyList<DrawCommand> drawCommands)
        : this(frameNumber, drawCommands, Array.Empty<UiDrawCommand>())
    {
    }

    public RenderPacket(long frameNumber, IReadOnlyList<DrawCommand> drawCommands, IReadOnlyList<UiDrawCommand> uiDrawCommands)
        : this(frameNumber, drawCommands, uiDrawCommands, IntPtr.Zero, 0, IntPtr.Zero, 0)
    {
    }

    private RenderPacket(
        long frameNumber,
        IReadOnlyList<DrawCommand> drawCommands,
        IReadOnlyList<UiDrawCommand> uiDrawCommands,
        IntPtr nativeDrawItemsPointer,
        int nativeDrawItemCount,
        IntPtr nativeUiDrawItemsPointer,
        int nativeUiDrawItemCount)
    {
        if (frameNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameNumber), "Frame number cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(drawCommands);
        ArgumentNullException.ThrowIfNull(uiDrawCommands);
        ValidatePointerCountPair(nameof(nativeDrawItemsPointer), nativeDrawItemsPointer, nameof(nativeDrawItemCount), nativeDrawItemCount);
        ValidatePointerCountPair(nameof(nativeUiDrawItemsPointer), nativeUiDrawItemsPointer, nameof(nativeUiDrawItemCount), nativeUiDrawItemCount);

        FrameNumber = frameNumber;
        DrawCommands = drawCommands;
        UiDrawCommands = uiDrawCommands;
        NativeDrawItemsPointer = nativeDrawItemsPointer;
        NativeDrawItemCount = nativeDrawItemCount;
        NativeUiDrawItemsPointer = nativeUiDrawItemsPointer;
        NativeUiDrawItemCount = nativeUiDrawItemCount;
    }

    public long FrameNumber { get; }

    public IReadOnlyList<DrawCommand> DrawCommands { get; }

    public IReadOnlyList<UiDrawCommand> UiDrawCommands { get; }

    public IntPtr NativeDrawItemsPointer { get; }

    public int NativeDrawItemCount { get; }

    public IntPtr NativeUiDrawItemsPointer { get; }

    public int NativeUiDrawItemCount { get; }

    public ReadOnlySpan<NativeDrawItem> NativeDrawItems
        => NativeDrawItemCount == 0
            ? ReadOnlySpan<NativeDrawItem>.Empty
            : new ReadOnlySpan<NativeDrawItem>((void*)NativeDrawItemsPointer, NativeDrawItemCount);

    public ReadOnlySpan<NativeUiDrawItem> NativeUiDrawItems
        => NativeUiDrawItemCount == 0
            ? ReadOnlySpan<NativeUiDrawItem>.Empty
            : new ReadOnlySpan<NativeUiDrawItem>((void*)NativeUiDrawItemsPointer, NativeUiDrawItemCount);

    public static RenderPacket CreateNative(
        long frameNumber,
        IReadOnlyList<DrawCommand> drawCommands,
        IReadOnlyList<UiDrawCommand> uiDrawCommands,
        IntPtr nativeDrawItemsPointer,
        int nativeDrawItemCount,
        IntPtr nativeUiDrawItemsPointer,
        int nativeUiDrawItemCount)
        => new(
            frameNumber,
            drawCommands,
            uiDrawCommands,
            nativeDrawItemsPointer,
            nativeDrawItemCount,
            nativeUiDrawItemsPointer,
            nativeUiDrawItemCount);

    public static RenderPacket Empty(long frameNumber)
        => new(
            frameNumber,
            Array.Empty<DrawCommand>(),
            Array.Empty<UiDrawCommand>(),
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0);

    private static void ValidatePointerCountPair(string pointerParam, IntPtr pointer, string countParam, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(countParam, "Native item count cannot be negative.");
        }

        var hasPointer = pointer != IntPtr.Zero;
        var hasItems = count > 0;

        if (hasPointer != hasItems)
        {
            throw new ArgumentException(
                $"Pointer '{pointerParam}' and count '{countParam}' must either both reference data or both be empty.");
        }
    }
}
