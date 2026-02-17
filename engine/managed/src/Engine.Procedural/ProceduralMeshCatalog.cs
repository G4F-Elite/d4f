using System.Numerics;

namespace Engine.Procedural;

public static class ProceduralMeshCatalog
{
    public static ProcMeshData BuildChunkMesh(LevelMeshChunk chunk, uint seed)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ParsedMeshTag parsedTag = ParseMeshTag(chunk.MeshTag);

        var builder = new MeshBuilder();
        builder.BeginSubmesh($"chunk/{parsedTag.TypeTag}");
        switch (parsedTag.NodeType)
        {
            case LevelNodeType.Room:
                BuildRoomMesh(builder, chunk.NodeId, parsedTag.Variant, seed);
                break;
            case LevelNodeType.Corridor:
                BuildCorridorMesh(builder, chunk.NodeId, parsedTag.Variant, seed);
                break;
            case LevelNodeType.Junction:
                BuildJunctionMesh(builder, chunk.NodeId, parsedTag.Variant, seed);
                break;
            case LevelNodeType.DeadEnd:
                BuildDeadEndMesh(builder, chunk.NodeId, parsedTag.Variant, seed);
                break;
            case LevelNodeType.Shaft:
                BuildShaftMesh(builder, chunk.NodeId, parsedTag.Variant, seed);
                break;
            default:
                throw new InvalidDataException($"Unsupported node type '{parsedTag.NodeType}' in mesh tag '{chunk.MeshTag}'.");
        }

        builder.EndSubmesh();

        float uvScale = 1.2f + SampleRange(seed, chunk.NodeId, parsedTag.Variant, salt: 31u, min: 0.0f, max: 0.8f);
        builder.GenerateUv(UvProjection.Box, uvScale);
        builder.GenerateLod(screenCoverage: 0.55f);
        return builder.Build();
    }

    private static void BuildRoomMesh(MeshBuilder builder, int nodeId, int variant, uint seed)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 1u, min: 5.5f, max: 8.5f);
        float height = SampleRange(seed, nodeId, variant, salt: 2u, min: 2.8f, max: 4.2f);
        float depth = SampleRange(seed, nodeId, variant, salt: 3u, min: 5.5f, max: 8.5f);
        AppendBox(builder, Vector3.Zero, new Vector3(width, height, depth));

        if ((variant & 1) != 0)
        {
            float pillarHeight = height * 0.85f;
            AppendBox(builder, new Vector3(0f, -(height - pillarHeight) * 0.5f, 0f), new Vector3(0.8f, pillarHeight, 0.8f));
        }
    }

    private static void BuildCorridorMesh(MeshBuilder builder, int nodeId, int variant, uint seed)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 4u, min: 2.0f, max: 3.2f);
        float height = SampleRange(seed, nodeId, variant, salt: 5u, min: 2.2f, max: 3.0f);
        float depth = SampleRange(seed, nodeId, variant, salt: 6u, min: 7.0f, max: 12.5f);
        AppendBox(builder, Vector3.Zero, new Vector3(width, height, depth));

        if (variant >= 2)
        {
            float nicheOffset = width * 0.65f;
            float nicheDepth = depth * 0.2f;
            AppendBox(builder, new Vector3(nicheOffset, 0f, -depth * 0.2f), new Vector3(width * 0.35f, height * 0.7f, nicheDepth));
        }
    }

    private static void BuildJunctionMesh(MeshBuilder builder, int nodeId, int variant, uint seed)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 7u, min: 2.4f, max: 3.4f);
        float height = SampleRange(seed, nodeId, variant, salt: 8u, min: 2.4f, max: 3.2f);
        float armLength = SampleRange(seed, nodeId, variant, salt: 9u, min: 5.0f, max: 8.0f);

        AppendBox(builder, Vector3.Zero, new Vector3(width, height, armLength));
        AppendBox(builder, Vector3.Zero, new Vector3(armLength, height, width));

        if ((variant & 1) == 0)
        {
            AppendBox(builder, new Vector3(0f, -height * 0.25f, 0f), new Vector3(width * 0.5f, height * 0.5f, width * 0.5f));
        }
    }

    private static void BuildDeadEndMesh(MeshBuilder builder, int nodeId, int variant, uint seed)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 10u, min: 2.2f, max: 3.0f);
        float height = SampleRange(seed, nodeId, variant, salt: 11u, min: 2.3f, max: 3.1f);
        float depth = SampleRange(seed, nodeId, variant, salt: 12u, min: 6.0f, max: 9.0f);
        AppendBox(builder, Vector3.Zero, new Vector3(width, height, depth));

        float capThickness = SampleRange(seed, nodeId, variant, salt: 13u, min: 0.3f, max: 0.8f);
        AppendBox(
            builder,
            new Vector3(0f, 0f, depth * 0.5f + capThickness * 0.5f),
            new Vector3(width * 0.98f, height * 0.98f, capThickness));
    }

    private static void BuildShaftMesh(MeshBuilder builder, int nodeId, int variant, uint seed)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 14u, min: 1.8f, max: 2.8f);
        float height = SampleRange(seed, nodeId, variant, salt: 15u, min: 7.0f, max: 12.0f);
        float depth = SampleRange(seed, nodeId, variant, salt: 16u, min: 1.8f, max: 2.8f);
        AppendBox(builder, Vector3.Zero, new Vector3(width, height, depth));

        if (variant >= 1)
        {
            float platformY = height * 0.2f;
            AppendBox(builder, new Vector3(width * 0.45f, platformY, 0f), new Vector3(width * 0.5f, 0.35f, depth * 0.9f));
        }
    }

    private static void AppendBox(MeshBuilder builder, Vector3 center, Vector3 size)
    {
        if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
        {
            throw new InvalidDataException("Box size components must be greater than zero.");
        }

        Vector3 half = size * 0.5f;
        Vector3 min = center - half;
        Vector3 max = center + half;

        // Front (+Z)
        AddFace(
            builder,
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            Vector3.UnitZ);
        // Back (-Z)
        AddFace(
            builder,
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            -Vector3.UnitZ);
        // Left (-X)
        AddFace(
            builder,
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, min.Z),
            -Vector3.UnitX);
        // Right (+X)
        AddFace(
            builder,
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, max.Z),
            Vector3.UnitX);
        // Top (+Y)
        AddFace(
            builder,
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            Vector3.UnitY);
        // Bottom (-Y)
        AddFace(
            builder,
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, min.Y, max.Z),
            -Vector3.UnitY);
    }

    private static void AddFace(
        MeshBuilder builder,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal)
    {
        int i0 = builder.AddVertex(a, normal, new Vector2(0f, 0f));
        int i1 = builder.AddVertex(b, normal, new Vector2(1f, 0f));
        int i2 = builder.AddVertex(c, normal, new Vector2(1f, 1f));
        int i3 = builder.AddVertex(d, normal, new Vector2(0f, 1f));
        builder.AddTriangle(i0, i1, i2);
        builder.AddTriangle(i0, i2, i3);
    }

    private static ParsedMeshTag ParseMeshTag(string meshTag)
    {
        if (string.IsNullOrWhiteSpace(meshTag))
        {
            throw new ArgumentException("Mesh tag cannot be empty.", nameof(meshTag));
        }

        string[] parts = meshTag.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "chunk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Mesh tag '{meshTag}' must have format 'chunk/<type>/v<variant>'.");
        }

        if (!parts[2].StartsWith("v", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2].AsSpan(1), out int variant) ||
            variant < 0 ||
            variant > 3)
        {
            throw new InvalidDataException($"Mesh tag '{meshTag}' has invalid variant segment '{parts[2]}'.");
        }

        LevelNodeType nodeType = parts[1].ToLowerInvariant() switch
        {
            "room" => LevelNodeType.Room,
            "corridor" => LevelNodeType.Corridor,
            "junction" => LevelNodeType.Junction,
            "deadend" => LevelNodeType.DeadEnd,
            "shaft" => LevelNodeType.Shaft,
            _ => throw new InvalidDataException($"Mesh tag '{meshTag}' has unsupported type segment '{parts[1]}'.")
        };

        return new ParsedMeshTag(nodeType, parts[1].ToLowerInvariant(), variant);
    }

    private static float SampleRange(
        uint seed,
        int nodeId,
        int variant,
        uint salt,
        float min,
        float max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum sample range cannot exceed maximum.");
        }

        uint value = seed ^ ((uint)nodeId * 747796405u) ^ ((uint)variant * 2891336453u) ^ salt;
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        value *= 3266489917u;
        value ^= value >> 16;

        float normalized = (value & 0x00FFFFFFu) / 16777215.0f;
        return min + (max - min) * normalized;
    }

    private readonly record struct ParsedMeshTag(LevelNodeType NodeType, string TypeTag, int Variant);
}
