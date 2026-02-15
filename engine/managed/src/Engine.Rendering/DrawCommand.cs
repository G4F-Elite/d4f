using System;
using Engine.Core.Handles;

namespace Engine.Rendering;

public readonly record struct DrawCommand
{
    public DrawCommand(EntityId entityId, MeshHandle mesh, MaterialHandle material, TextureHandle texture)
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
    }

    public EntityId EntityId { get; }

    public MeshHandle Mesh { get; }

    public MaterialHandle Material { get; }

    public TextureHandle Texture { get; }
}
