using System;
using System.Numerics;
using Engine.Core.Handles;

namespace Engine.Rendering;

public readonly record struct DrawCommand
{
    public DrawCommand(EntityId entityId, MeshHandle mesh, MaterialHandle material, TextureHandle texture)
        : this(entityId, mesh, material, texture, Matrix4x4.Identity, material.Value, mesh.Value)
    {
    }

    public DrawCommand(
        EntityId entityId,
        MeshHandle mesh,
        MaterialHandle material,
        TextureHandle texture,
        Matrix4x4 worldMatrix,
        uint sortKeyHigh,
        uint sortKeyLow)
    {
        if (!entityId.IsValid)
        {
            throw new ArgumentException("Entity id must be valid.", nameof(entityId));
        }

        if (!mesh.IsValid)
        {
            throw new ArgumentException("Mesh handle must be valid.", nameof(mesh));
        }

        if (!material.IsValid)
        {
            throw new ArgumentException("Material handle must be valid.", nameof(material));
        }

        if (!texture.IsValid)
        {
            throw new ArgumentException("Texture handle must be valid.", nameof(texture));
        }

        EntityId = entityId;
        Mesh = mesh;
        Material = material;
        Texture = texture;
        WorldMatrix = worldMatrix;
        SortKeyHigh = sortKeyHigh;
        SortKeyLow = sortKeyLow;
    }

    public EntityId EntityId { get; }

    public MeshHandle Mesh { get; }

    public MaterialHandle Material { get; }

    public TextureHandle Texture { get; }

    public Matrix4x4 WorldMatrix { get; }

    public uint SortKeyHigh { get; }

    public uint SortKeyLow { get; }
}
