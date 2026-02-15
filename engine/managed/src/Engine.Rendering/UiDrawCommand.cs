using System;

namespace Engine.Rendering;

public readonly record struct UiDrawCommand
{
    public UiDrawCommand(uint drawListId, uint textureId, int indexOffset, int elementCount)
    {
        if (drawListId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(drawListId), "Draw list id must be non-zero.");
        }

        if (indexOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indexOffset), "Index offset cannot be negative.");
        }

        if (elementCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be positive.");
        }

        DrawListId = drawListId;
        TextureId = textureId;
        IndexOffset = indexOffset;
        ElementCount = elementCount;
    }

    public uint DrawListId { get; }

    public uint TextureId { get; }

    public int IndexOffset { get; }

    public int ElementCount { get; }
}
