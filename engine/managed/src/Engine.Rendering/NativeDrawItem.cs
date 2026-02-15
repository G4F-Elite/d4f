namespace Engine.Rendering;

public readonly record struct NativeDrawItem
{
    public NativeDrawItem(int entityIndex, uint entityGeneration, uint mesh, uint material, uint texture)
    {
        EntityIndex = entityIndex;
        EntityGeneration = entityGeneration;
        Mesh = mesh;
        Material = material;
        Texture = texture;
    }

    public int EntityIndex { get; }

    public uint EntityGeneration { get; }

    public uint Mesh { get; }

    public uint Material { get; }

    public uint Texture { get; }

    public static NativeDrawItem From(in DrawCommand command)
        => new(
            command.EntityId.Index,
            command.EntityId.Generation,
            command.Mesh.Value,
            command.Material.Value,
            command.Texture.Value);
}
