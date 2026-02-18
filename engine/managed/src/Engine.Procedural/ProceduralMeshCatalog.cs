using System.Numerics;

namespace Engine.Procedural;

public static class ProceduralMeshCatalog
{
    public static ProcMeshData BuildChunkMesh(LevelMeshChunk chunk, uint seed)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        LevelChunkTag parsedTag = LevelChunkTag.Parse(chunk.MeshTag);
        string baseMaterialTag = $"chunk/{parsedTag.TypeTag}";
        string accentMaterialTag = $"{baseMaterialTag}/accent";
        (Vector4 baseColor, Vector4 accentColor) = SelectSubmeshColors(parsedTag, seed, chunk.NodeId);

        var builder = new MeshBuilder();
        switch (parsedTag.NodeType)
        {
            case LevelNodeType.Room:
                BuildRoomMesh(builder, chunk.NodeId, parsedTag.Variant, seed, baseMaterialTag, accentMaterialTag, baseColor, accentColor);
                break;
            case LevelNodeType.Corridor:
                BuildCorridorMesh(builder, chunk.NodeId, parsedTag.Variant, seed, baseMaterialTag, accentMaterialTag, baseColor, accentColor);
                break;
            case LevelNodeType.Junction:
                BuildJunctionMesh(builder, chunk.NodeId, parsedTag.Variant, seed, baseMaterialTag, accentMaterialTag, baseColor, accentColor);
                break;
            case LevelNodeType.DeadEnd:
                BuildDeadEndMesh(builder, chunk.NodeId, parsedTag.Variant, seed, baseMaterialTag, accentMaterialTag, baseColor, accentColor);
                break;
            case LevelNodeType.Shaft:
                BuildShaftMesh(builder, chunk.NodeId, parsedTag.Variant, seed, baseMaterialTag, accentMaterialTag, baseColor, accentColor);
                break;
            default:
                throw new InvalidDataException($"Unsupported node type '{parsedTag.NodeType}' in mesh tag '{chunk.MeshTag}'.");
        }

        float uvScale = 1.2f + SampleRange(seed, chunk.NodeId, parsedTag.Variant, salt: 31u, min: 0.0f, max: 0.8f);
        builder.GenerateUv(SelectUvProjection(parsedTag), uvScale);
        builder.GenerateLodChain(0.55f, 0.30f);
        return builder.Build();
    }

    private static UvProjection SelectUvProjection(LevelChunkTag tag)
    {
        return tag.NodeType switch
        {
            LevelNodeType.Corridor => UvProjection.Cylindrical,
            LevelNodeType.Shaft => UvProjection.Cylindrical,
            LevelNodeType.Room => UvProjection.Box,
            LevelNodeType.Junction => UvProjection.Box,
            LevelNodeType.DeadEnd => UvProjection.Box,
            _ => throw new InvalidDataException($"Unsupported node type '{tag.NodeType}' for UV projection.")
        };
    }

    private static void BuildRoomMesh(
        MeshBuilder builder,
        int nodeId,
        int variant,
        uint seed,
        string baseMaterialTag,
        string accentMaterialTag,
        Vector4 baseColor,
        Vector4 accentColor)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 1u, min: 5.5f, max: 8.5f);
        float height = SampleRange(seed, nodeId, variant, salt: 2u, min: 2.8f, max: 4.2f);
        float depth = SampleRange(seed, nodeId, variant, salt: 3u, min: 5.5f, max: 8.5f);
        AppendBoxInSubmesh(builder, baseMaterialTag, Vector3.Zero, new Vector3(width, height, depth), baseColor);

        if ((variant & 1) != 0)
        {
            float pillarHeight = height * 0.85f;
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(0f, -(height - pillarHeight) * 0.5f, 0f),
                new Vector3(0.8f, pillarHeight, 0.8f),
                accentColor);
        }

        if (variant >= 2)
        {
            float trimHeight = MathF.Max(0.10f, height * 0.08f);
            float trimWidth = width * 0.94f;
            float trimDepth = depth * 0.94f;
            float topY = (height * 0.5f) - (trimHeight * 0.5f);
            float bottomY = (-height * 0.5f) + (trimHeight * 0.5f);
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(0f, topY, 0f),
                new Vector3(trimWidth, trimHeight, trimDepth),
                accentColor);
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(0f, bottomY, 0f),
                new Vector3(trimWidth, trimHeight, trimDepth),
                accentColor);
        }
    }

    private static void BuildCorridorMesh(
        MeshBuilder builder,
        int nodeId,
        int variant,
        uint seed,
        string baseMaterialTag,
        string accentMaterialTag,
        Vector4 baseColor,
        Vector4 accentColor)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 4u, min: 2.0f, max: 3.2f);
        float height = SampleRange(seed, nodeId, variant, salt: 5u, min: 2.2f, max: 3.0f);
        float depth = SampleRange(seed, nodeId, variant, salt: 6u, min: 7.0f, max: 12.5f);
        AppendBoxInSubmesh(builder, baseMaterialTag, Vector3.Zero, new Vector3(width, height, depth), baseColor);

        if (variant >= 2)
        {
            float nicheOffset = width * 0.65f;
            float nicheDepth = depth * 0.2f;
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(nicheOffset, 0f, -depth * 0.2f),
                new Vector3(width * 0.35f, height * 0.7f, nicheDepth),
                accentColor);
        }

        if (variant >= 3)
        {
            float railWidth = MathF.Max(0.12f, width * 0.14f);
            float railHeight = MathF.Max(0.16f, height * 0.10f);
            float railDepth = depth * 0.92f;
            float railY = (-height * 0.5f) + (railHeight * 0.5f);
            float railOffsetX = (width * 0.5f) - (railWidth * 0.5f);
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(railOffsetX, railY, 0f),
                new Vector3(railWidth, railHeight, railDepth),
                accentColor);
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(-railOffsetX, railY, 0f),
                new Vector3(railWidth, railHeight, railDepth),
                accentColor);
        }
    }

    private static void BuildJunctionMesh(
        MeshBuilder builder,
        int nodeId,
        int variant,
        uint seed,
        string baseMaterialTag,
        string accentMaterialTag,
        Vector4 baseColor,
        Vector4 accentColor)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 7u, min: 2.4f, max: 3.4f);
        float height = SampleRange(seed, nodeId, variant, salt: 8u, min: 2.4f, max: 3.2f);
        float armLength = SampleRange(seed, nodeId, variant, salt: 9u, min: 5.0f, max: 8.0f);

        builder.BeginSubmesh(baseMaterialTag);
        AppendBox(builder, Vector3.Zero, new Vector3(width, height, armLength), baseColor);
        AppendBox(builder, Vector3.Zero, new Vector3(armLength, height, width), baseColor);
        builder.EndSubmesh();

        if ((variant & 1) == 0)
        {
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(0f, -height * 0.25f, 0f),
                new Vector3(width * 0.5f, height * 0.5f, width * 0.5f),
                accentColor);
        }
    }

    private static void BuildDeadEndMesh(
        MeshBuilder builder,
        int nodeId,
        int variant,
        uint seed,
        string baseMaterialTag,
        string accentMaterialTag,
        Vector4 baseColor,
        Vector4 accentColor)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 10u, min: 2.2f, max: 3.0f);
        float height = SampleRange(seed, nodeId, variant, salt: 11u, min: 2.3f, max: 3.1f);
        float depth = SampleRange(seed, nodeId, variant, salt: 12u, min: 6.0f, max: 9.0f);
        AppendBoxInSubmesh(builder, baseMaterialTag, Vector3.Zero, new Vector3(width, height, depth), baseColor);

        float capThickness = SampleRange(seed, nodeId, variant, salt: 13u, min: 0.3f, max: 0.8f);
        AppendBoxInSubmesh(
            builder,
            accentMaterialTag,
            new Vector3(0f, 0f, depth * 0.5f + capThickness * 0.5f),
            new Vector3(width * 0.98f, height * 0.98f, capThickness),
            accentColor);
    }

    private static void BuildShaftMesh(
        MeshBuilder builder,
        int nodeId,
        int variant,
        uint seed,
        string baseMaterialTag,
        string accentMaterialTag,
        Vector4 baseColor,
        Vector4 accentColor)
    {
        float width = SampleRange(seed, nodeId, variant, salt: 14u, min: 1.8f, max: 2.8f);
        float height = SampleRange(seed, nodeId, variant, salt: 15u, min: 7.0f, max: 12.0f);
        float depth = SampleRange(seed, nodeId, variant, salt: 16u, min: 1.8f, max: 2.8f);
        AppendBoxInSubmesh(builder, baseMaterialTag, Vector3.Zero, new Vector3(width, height, depth), baseColor);

        if (variant >= 1)
        {
            float platformY = height * 0.2f;
            AppendBoxInSubmesh(
                builder,
                accentMaterialTag,
                new Vector3(width * 0.45f, platformY, 0f),
                new Vector3(width * 0.5f, 0.35f, depth * 0.9f),
                accentColor);
        }
    }

    private static void AppendBoxInSubmesh(
        MeshBuilder builder,
        string materialTag,
        Vector3 center,
        Vector3 size,
        Vector4 color)
    {
        builder.BeginSubmesh(materialTag);
        AppendBox(builder, center, size, color);
        builder.EndSubmesh();
    }

    private static void AppendBox(MeshBuilder builder, Vector3 center, Vector3 size, Vector4 color)
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
            Vector3.UnitZ,
            color);
        // Back (-Z)
        AddFace(
            builder,
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            -Vector3.UnitZ,
            color);
        // Left (-X)
        AddFace(
            builder,
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, min.Z),
            -Vector3.UnitX,
            color);
        // Right (+X)
        AddFace(
            builder,
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(max.X, max.Y, max.Z),
            Vector3.UnitX,
            color);
        // Top (+Y)
        AddFace(
            builder,
            new Vector3(min.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            Vector3.UnitY,
            color);
        // Bottom (-Y)
        AddFace(
            builder,
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(min.X, min.Y, max.Z),
            -Vector3.UnitY,
            color);
    }

    private static void AddFace(
        MeshBuilder builder,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal,
        Vector4 color)
    {
        int i0 = builder.AddVertex(a, normal, new Vector2(0f, 0f), color);
        int i1 = builder.AddVertex(b, normal, new Vector2(1f, 0f), color);
        int i2 = builder.AddVertex(c, normal, new Vector2(1f, 1f), color);
        int i3 = builder.AddVertex(d, normal, new Vector2(0f, 1f), color);
        builder.AddTriangle(i0, i1, i2);
        builder.AddTriangle(i0, i2, i3);
    }

    private static (Vector4 BaseColor, Vector4 AccentColor) SelectSubmeshColors(LevelChunkTag tag, uint seed, int nodeId)
    {
        Vector3 baseRgb = tag.NodeType switch
        {
            LevelNodeType.Room => new Vector3(0.92f, 0.89f, 0.84f),
            LevelNodeType.Corridor => new Vector3(0.80f, 0.87f, 0.90f),
            LevelNodeType.Junction => new Vector3(0.86f, 0.86f, 0.87f),
            LevelNodeType.DeadEnd => new Vector3(0.88f, 0.78f, 0.72f),
            LevelNodeType.Shaft => new Vector3(0.76f, 0.86f, 0.80f),
            _ => Vector3.One
        };

        float variation = SampleRange(seed, nodeId, tag.Variant, salt: 41u, min: 0.92f, max: 1.04f);
        Vector3 tintedBase = Vector3.Clamp(baseRgb * variation, Vector3.Zero, Vector3.One);
        float accentDarkening = SampleRange(seed, nodeId, tag.Variant, salt: 43u, min: 0.58f, max: 0.72f);
        Vector3 accentBoost = new Vector3(
            SampleRange(seed, nodeId, tag.Variant, salt: 45u, min: 0.01f, max: 0.04f),
            SampleRange(seed, nodeId, tag.Variant, salt: 47u, min: 0.01f, max: 0.04f),
            SampleRange(seed, nodeId, tag.Variant, salt: 49u, min: 0.01f, max: 0.04f));
        Vector3 tintedAccent = Vector3.Clamp((tintedBase * accentDarkening) + accentBoost, Vector3.Zero, Vector3.One);
        return (new Vector4(tintedBase, 1f), new Vector4(tintedAccent, 1f));
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
}
