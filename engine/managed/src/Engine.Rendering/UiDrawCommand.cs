using System;
using Engine.Core.Handles;

namespace Engine.Rendering;

public readonly record struct UiDrawCommand
{
    public UiDrawCommand(TextureHandle texture, uint vertexOffset, uint vertexCount, uint indexOffset, uint indexCount)
    {
        if (!texture.IsValid)
        {
            throw new ArgumentException("Texture handle must be valid.", nameof(texture));
        }

        if (vertexCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount), "Vertex count must be positive.");
        }

        if (indexCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indexCount), "Index count must be positive.");
        }

        Texture = texture;
        VertexOffset = vertexOffset;
        VertexCount = vertexCount;
        IndexOffset = indexOffset;
        IndexCount = indexCount;
    }

    public UiDrawCommand(uint texture, uint vertexOffset, uint vertexCount, uint indexOffset, uint indexCount)
        : this(new TextureHandle(texture), vertexOffset, vertexCount, indexOffset, indexCount)
    {
    }

    public TextureHandle Texture { get; }

    public uint VertexOffset { get; }

    public uint VertexCount { get; }

    public uint IndexOffset { get; }

    public uint IndexCount { get; }
}
