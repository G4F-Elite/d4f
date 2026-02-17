using System;
using System.Numerics;
using Engine.Core.Handles;

namespace Engine.Rendering;

public readonly struct RenderMeshInstance
{
    public RenderMeshInstance(MeshHandle mesh, MaterialHandle material, TextureHandle texture)
        : this(
            mesh,
            material,
            texture,
            Matrix4x4.Identity,
            SortKeyFromHandle(material.Value),
            SortKeyFromHandle(mesh.Value))
    {
    }

    public RenderMeshInstance(
        MeshHandle mesh,
        MaterialHandle material,
        TextureHandle texture,
        Matrix4x4 worldMatrix,
        uint sortKeyHigh,
        uint sortKeyLow)
    {
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

        Mesh = mesh;
        Material = material;
        Texture = texture;
        WorldMatrix = worldMatrix;
        SortKeyHigh = sortKeyHigh;
        SortKeyLow = sortKeyLow;
    }

    public MeshHandle Mesh { get; }

    public MaterialHandle Material { get; }

    public TextureHandle Texture { get; }

    public Matrix4x4 WorldMatrix { get; }

    public uint SortKeyHigh { get; }

    public uint SortKeyLow { get; }

    private static uint SortKeyFromHandle(ulong handle)
    {
        return (uint)(handle & uint.MaxValue);
    }
}
