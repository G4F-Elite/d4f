using System;
using System.Collections.Generic;

namespace Engine.Rendering;

public sealed class RenderPacket
{
    public RenderPacket(long frameNumber, IReadOnlyList<DrawCommand> drawCommands)
    {
        if (frameNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameNumber), "Frame number cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(drawCommands);

        FrameNumber = frameNumber;
        DrawCommands = drawCommands;
    }

    public long FrameNumber { get; }

    public IReadOnlyList<DrawCommand> DrawCommands { get; }

    public static RenderPacket Empty(long frameNumber) => new(frameNumber, Array.Empty<DrawCommand>());
}
