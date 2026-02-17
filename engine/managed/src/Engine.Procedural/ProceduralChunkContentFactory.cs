namespace Engine.Procedural;

public sealed record ProceduralChunkContent(
    int NodeId,
    LevelNodeType NodeType,
    int Variant,
    ProcMeshData Mesh,
    ProceduralLitMaterialBundle MaterialBundle)
{
    public ProceduralChunkContent Validate()
    {
        if (NodeId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NodeId), "Node id cannot be negative.");
        }

        if (!Enum.IsDefined(NodeType))
        {
            throw new InvalidDataException($"Unsupported node type '{NodeType}'.");
        }

        if (Variant < 0 || Variant > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(Variant), "Variant must be within [0,3].");
        }

        ArgumentNullException.ThrowIfNull(Mesh);
        ArgumentNullException.ThrowIfNull(MaterialBundle);

        if (Mesh.Vertices.Count == 0 || Mesh.Indices.Count == 0)
        {
            throw new InvalidDataException("Procedural chunk mesh is empty.");
        }

        _ = MaterialBundle.Validate();
        return this;
    }
}

public static class ProceduralChunkContentFactory
{
    public static ProceduralChunkContent Build(
        LevelMeshChunk chunk,
        uint seed,
        int surfaceWidth = 128,
        int surfaceHeight = 128)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        LevelChunkTag tag = LevelChunkTag.Parse(chunk.MeshTag);

        ProcMeshData mesh = ProceduralMeshCatalog.BuildChunkMesh(chunk, seed);
        ProceduralTextureSurface surface = ProceduralChunkSurfaceCatalog.BuildChunkSurface(
            chunk,
            seed,
            surfaceWidth,
            surfaceHeight);
        (float roughness, float metallic) = SelectMaterialParams(tag);
        string textureKeyPrefix = $"proc/chunk/{tag.TypeTag}/v{tag.Variant}/n{chunk.NodeId}";
        ProceduralLitMaterialBundle materialBundle = ProceduralMaterialFactory.CreateLitPbrFromSurface(
            surface,
            textureKeyPrefix,
            roughness,
            metallic);

        return new ProceduralChunkContent(
            chunk.NodeId,
            tag.NodeType,
            tag.Variant,
            mesh,
            materialBundle).Validate();
    }

    public static IReadOnlyList<ProceduralChunkContent> BuildAll(
        LevelGenResult level,
        uint seed,
        int surfaceWidth = 128,
        int surfaceHeight = 128)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (level.MeshChunks.Count == 0)
        {
            return Array.Empty<ProceduralChunkContent>();
        }

        var result = new ProceduralChunkContent[level.MeshChunks.Count];
        for (int i = 0; i < level.MeshChunks.Count; i++)
        {
            result[i] = Build(level.MeshChunks[i], seed, surfaceWidth, surfaceHeight);
        }

        return result;
    }

    private static (float Roughness, float Metallic) SelectMaterialParams(LevelChunkTag tag)
    {
        return tag.NodeType switch
        {
            LevelNodeType.Room => (0.55f + (tag.Variant * 0.05f), 0.08f),
            LevelNodeType.Corridor => (0.62f + (tag.Variant * 0.04f), 0.12f),
            LevelNodeType.Junction => (0.58f + (tag.Variant * 0.05f), 0.10f),
            LevelNodeType.DeadEnd => (0.68f + (tag.Variant * 0.04f), 0.05f),
            LevelNodeType.Shaft => (0.72f + (tag.Variant * 0.03f), 0.16f),
            _ => throw new InvalidDataException($"Unsupported node type '{tag.NodeType}'.")
        };
    }
}
