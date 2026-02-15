namespace Engine.Rendering;

public readonly record struct NativeUiDrawItem
{
    public NativeUiDrawItem(uint drawListId, uint textureId, int indexOffset, int elementCount)
    {
        DrawListId = drawListId;
        TextureId = textureId;
        IndexOffset = indexOffset;
        ElementCount = elementCount;
    }

    public uint DrawListId { get; }

    public uint TextureId { get; }

    public int IndexOffset { get; }

    public int ElementCount { get; }

    public static NativeUiDrawItem From(in UiDrawCommand command)
        => new(command.DrawListId, command.TextureId, command.IndexOffset, command.ElementCount);
}
