using System.Numerics;
using Engine.Procedural;
using Engine.Testing;

namespace Engine.Cli;

internal static partial class ProceduralPreviewRasterizer
{
    public static GoldenImageBuffer BuildPreview(
        string kind,
        string assetPath,
        uint seed,
        int width,
        int height)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetPath);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Preview dimensions must be greater than zero.");
        }

        string normalizedKind = kind.Trim().ToLowerInvariant();
        LevelMeshChunk chunk = BuildChunkFromPath(assetPath, seed);

        return normalizedKind switch
        {
            "mesh" => BuildMeshPreview(chunk, seed, width, height),
            "texture" => BuildTexturePreview(chunk, seed, width, height),
            "material" => BuildMaterialPreview(chunk, seed, width, height),
            _ => BuildFallbackPreview(seed, width, height)
        };
    }

    private static GoldenImageBuffer BuildMeshPreview(LevelMeshChunk chunk, uint seed, int width, int height)
    {
        ProceduralChunkContent content = ProceduralChunkContentFactory.Build(
            chunk,
            seed,
            surfaceWidth: Math.Max(width * 2, 128),
            surfaceHeight: Math.Max(height * 2, 128));
        TexturePayload albedo = ResolveTextureBySuffix(content.MaterialBundle, ".albedo");
        TexturePayload normal = ResolveTextureBySuffix(content.MaterialBundle, ".normal");
        TexturePayload roughness = ResolveTextureBySuffix(content.MaterialBundle, ".roughness");
        TexturePayload ao = ResolveTextureBySuffix(content.MaterialBundle, ".ao");
        return RasterizeMesh(content.Mesh, albedo, normal, roughness, ao, width, height, seed);
    }

    private static GoldenImageBuffer BuildTexturePreview(LevelMeshChunk chunk, uint seed, int width, int height)
    {
        ProceduralTextureSurface surface = ProceduralChunkSurfaceCatalog.BuildChunkSurface(
            chunk,
            seed,
            width,
            height);
        byte[] displayRgba = ConvertLinearToDisplay(surface.AlbedoRgba8);
        return new GoldenImageBuffer(width, height, displayRgba);
    }

    private static GoldenImageBuffer BuildMaterialPreview(LevelMeshChunk chunk, uint seed, int width, int height)
    {
        ProceduralChunkContent content = ProceduralChunkContentFactory.Build(
            chunk,
            seed,
            surfaceWidth: Math.Max(width * 2, 128),
            surfaceHeight: Math.Max(height * 2, 128));
        TexturePayload albedo = ResolveTextureBySuffix(content.MaterialBundle, ".albedo");
        TexturePayload normal = ResolveTextureBySuffix(content.MaterialBundle, ".normal");
        TexturePayload roughness = ResolveTextureBySuffix(content.MaterialBundle, ".roughness");
        TexturePayload ao = ResolveTextureBySuffix(content.MaterialBundle, ".ao");

        var rgba = new byte[width * height * 4];
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.45f, 0.65f, 0.62f));
        Vector3 viewDir = Vector3.UnitZ;
        float radius = 0.9f;

        for (int y = 0; y < height; y++)
        {
            float ny = ((y + 0.5f) / height) * 2f - 1f;
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width) * 2f - 1f;
                float r2 = nx * nx + ny * ny;
                int offset = ((y * width) + x) * 4;
                if (r2 > radius * radius)
                {
                    WritePixel(
                        rgba,
                        offset,
                        new Vector3(0.06f + (x / (float)width) * 0.06f, 0.07f, 0.09f + (y / (float)height) * 0.06f));
                    continue;
                }

                float z = MathF.Sqrt(MathF.Max(0f, 1f - (r2 / (radius * radius))));
                var sphereNormal = Vector3.Normalize(new Vector3(nx, -ny, z));
                float u = nx * 0.5f + 0.5f;
                float v = ny * 0.5f + 0.5f;

                Vector3 albedoSample = SampleRgb(albedo, u, v);
                Vector3 normalSample = SampleNormal(normal, u, v);
                float roughnessSample = SampleGray(roughness, u, v);
                float aoSample = SampleGray(ao, u, v);
                Vector3 sphereTangent = ComputeSurfaceTangent(sphereNormal);
                Vector3 sphereBitangent = NormalizeOrFallback(
                    Vector3.Cross(sphereNormal, sphereTangent),
                    Vector3.UnitX);
                Vector3 perturbedNormal = ApplyNormalMap(
                    sphereNormal,
                    sphereTangent,
                    sphereBitangent,
                    normalSample,
                    strength: 0.68f);

                float ndotl = MathF.Max(0f, Vector3.Dot(perturbedNormal, lightDir));
                Vector3 halfway = Vector3.Normalize(lightDir + viewDir);
                float specPow = Lerp(64f, 8f, roughnessSample);
                float specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(perturbedNormal, halfway)), specPow);
                float specIntensity = Lerp(0.05f, 0.3f, 1f - roughnessSample);
                float fresnel = MathF.Pow(1f - MathF.Max(0f, Vector3.Dot(perturbedNormal, viewDir)), 5f) * 0.2f;

                Vector3 lit = albedoSample * (0.15f + ndotl * aoSample * 0.85f)
                    + Vector3.One * (specular * specIntensity + fresnel);
                WritePixel(rgba, offset, lit);
            }
        }

        return new GoldenImageBuffer(width, height, rgba);
    }

    private static GoldenImageBuffer BuildFallbackPreview(uint seed, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                uint hash = Hash(seed, (uint)x, (uint)y);
                float grain = ((hash >> 8) & 0xFFu) / 255f;
                Vector3 color = new(
                    0.14f + (x / (float)Math.Max(width - 1, 1)) * 0.5f + grain * 0.05f,
                    0.16f + (y / (float)Math.Max(height - 1, 1)) * 0.45f + grain * 0.04f,
                    0.2f + ((x + y) / (float)Math.Max(width + height - 2, 1)) * 0.35f);
                int offset = ((y * width) + x) * 4;
                WritePixel(rgba, offset, color);
            }
        }

        return new GoldenImageBuffer(width, height, rgba);
    }

    private static GoldenImageBuffer RasterizeMesh(
        ProcMeshData mesh,
        TexturePayload albedo,
        TexturePayload normalMap,
        TexturePayload roughnessMap,
        TexturePayload aoMap,
        int width,
        int height,
        uint seed)
    {
        var rgba = new byte[width * height * 4];
        var depth = new float[width * height];
        Array.Fill(depth, float.MaxValue);

        for (int y = 0; y < height; y++)
        {
            float t = y / (float)Math.Max(height - 1, 1);
            Vector3 bg = Vector3.Lerp(new Vector3(0.06f, 0.08f, 0.11f), new Vector3(0.12f, 0.14f, 0.18f), t);
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;
                WritePixel(rgba, offset, bg);
            }
        }

        Vector3 size = mesh.Bounds.Size;
        float maxExtent = MathF.Max(MathF.Max(size.X, size.Y), size.Z);
        float uniformScale = maxExtent <= float.Epsilon ? 1f : 1.8f / maxExtent;
        Vector3 center = (mesh.Bounds.Min + mesh.Bounds.Max) * 0.5f;
        float yaw = 0.65f + (Sample01(seed, 17u, 3u) * 0.35f);
        float pitch = -0.42f + (Sample01(seed, 23u, 7u) * 0.08f);
        Matrix4x4 rotation = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, 0f);
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.5f, 0.62f, 0.61f));
        Vector3 viewDir = Vector3.UnitZ;

        var transformed = new MeshRasterVertex[mesh.Vertices.Count];
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            ProcVertex src = mesh.Vertices[i];
            Vector3 model = (src.Position - center) * uniformScale;
            Vector3 world = Vector3.Transform(model, rotation);
            world.Z += 3.2f;

            float invZ = 1f / MathF.Max(world.Z, 0.1f);
            float projection = 1.6f * invZ;
            float sx = (world.X * projection * 0.5f + 0.5f) * (width - 1);
            float sy = (0.5f - world.Y * projection * 0.5f) * (height - 1);
            Vector3 worldNormal = Vector3.Normalize(Vector3.TransformNormal(src.Normal, rotation));
            Vector3 worldTangent = Vector3.TransformNormal(new Vector3(src.Tangent.X, src.Tangent.Y, src.Tangent.Z), rotation);
            worldTangent = OrthonormalizeTangent(worldNormal, worldTangent);
            float handedness = src.Tangent.W < 0f ? -1f : 1f;
            Vector3 worldBitangent = NormalizeOrFallback(
                Vector3.Cross(worldNormal, worldTangent) * handedness,
                Vector3.UnitY);
            Vector3 vertexColor = Vector3.Clamp(new Vector3(src.Color.X, src.Color.Y, src.Color.Z), Vector3.Zero, Vector3.One);
            transformed[i] = new MeshRasterVertex(sx, sy, world.Z, worldNormal, worldTangent, worldBitangent, src.Uv, vertexColor);
        }

        for (int tri = 0; tri < mesh.Indices.Count; tri += 3)
        {
            MeshRasterVertex v0 = transformed[mesh.Indices[tri]];
            MeshRasterVertex v1 = transformed[mesh.Indices[tri + 1]];
            MeshRasterVertex v2 = transformed[mesh.Indices[tri + 2]];

            float area = Edge(v0.X, v0.Y, v1.X, v1.Y, v2.X, v2.Y);
            if (MathF.Abs(area) < 1e-6f)
            {
                continue;
            }

            int minX = Math.Clamp((int)MathF.Floor(MathF.Min(v0.X, MathF.Min(v1.X, v2.X))), 0, width - 1);
            int maxX = Math.Clamp((int)MathF.Ceiling(MathF.Max(v0.X, MathF.Max(v1.X, v2.X))), 0, width - 1);
            int minY = Math.Clamp((int)MathF.Floor(MathF.Min(v0.Y, MathF.Min(v1.Y, v2.Y))), 0, height - 1);
            int maxY = Math.Clamp((int)MathF.Ceiling(MathF.Max(v0.Y, MathF.Max(v1.Y, v2.Y))), 0, height - 1);
            if (minX > maxX || minY > maxY)
            {
                continue;
            }

            bool isPositiveArea = area > 0f;
            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    float sampleX = px + 0.5f;
                    float sampleY = py + 0.5f;
                    float w0 = Edge(v1.X, v1.Y, v2.X, v2.Y, sampleX, sampleY);
                    float w1 = Edge(v2.X, v2.Y, v0.X, v0.Y, sampleX, sampleY);
                    float w2 = Edge(v0.X, v0.Y, v1.X, v1.Y, sampleX, sampleY);
                    if (isPositiveArea)
                    {
                        if (w0 < 0f || w1 < 0f || w2 < 0f)
                        {
                            continue;
                        }
                    }
                    else if (w0 > 0f || w1 > 0f || w2 > 0f)
                    {
                        continue;
                    }

                    float invArea = 1f / area;
                    float b0 = w0 * invArea;
                    float b1 = w1 * invArea;
                    float b2 = w2 * invArea;
                    float z = b0 * v0.Z + b1 * v1.Z + b2 * v2.Z;
                    int bufferIndex = py * width + px;
                    if (z >= depth[bufferIndex])
                    {
                        continue;
                    }

                    depth[bufferIndex] = z;
                    Vector3 geometricNormal = NormalizeOrFallback(
                        (v0.Normal * b0) +
                        (v1.Normal * b1) +
                        (v2.Normal * b2),
                        Vector3.UnitZ);
                    Vector3 tangent = OrthonormalizeTangent(
                        geometricNormal,
                        (v0.Tangent * b0) + (v1.Tangent * b1) + (v2.Tangent * b2));
                    Vector3 bitangentCandidate = NormalizeOrFallback(
                        (v0.Bitangent * b0) + (v1.Bitangent * b1) + (v2.Bitangent * b2),
                        Vector3.Cross(geometricNormal, tangent));
                    Vector3 geometricBitangent = NormalizeOrFallback(
                        Vector3.Cross(geometricNormal, tangent),
                        Vector3.UnitY);
                    if (Vector3.Dot(bitangentCandidate, geometricBitangent) < 0f)
                    {
                        geometricBitangent = -geometricBitangent;
                    }

                    Vector2 uv = (v0.Uv * b0) + (v1.Uv * b1) + (v2.Uv * b2);
                    Vector3 albedoSample = SampleRgb(albedo, uv.X, uv.Y);
                    Vector3 sampledNormal = SampleNormal(normalMap, uv.X, uv.Y);
                    float roughnessSample = SampleGray(roughnessMap, uv.X, uv.Y);
                    float aoSample = SampleGray(aoMap, uv.X, uv.Y);
                    Vector3 vertexTint = Vector3.Clamp(
                        (v0.Color * b0) +
                        (v1.Color * b1) +
                        (v2.Color * b2),
                        Vector3.Zero,
                        Vector3.One);

                    Vector3 perturbedNormal = ApplyNormalMap(
                        geometricNormal,
                        tangent,
                        geometricBitangent,
                        sampledNormal,
                        strength: 0.62f);
                    float ndotl = MathF.Max(0f, Vector3.Dot(perturbedNormal, lightDir));
                    Vector3 halfway = Vector3.Normalize(lightDir + viewDir);
                    float specPow = Lerp(72f, 10f, roughnessSample);
                    float specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(perturbedNormal, halfway)), specPow);
                    float specIntensity = Lerp(0.04f, 0.22f, 1f - roughnessSample);
                    float fresnel = MathF.Pow(1f - MathF.Max(0f, Vector3.Dot(perturbedNormal, viewDir)), 5f) * 0.18f;
                    float rim = MathF.Pow(1f - MathF.Max(0f, Vector3.Dot(perturbedNormal, viewDir)), 2f) * 0.16f;
                    Vector3 color = (albedoSample * vertexTint) * (0.14f + ndotl * aoSample * 0.86f)
                        + Vector3.One * (specular * specIntensity + fresnel + rim * 0.08f);
                    int offset = bufferIndex * 4;
                    WritePixel(rgba, offset, color);
                }
            }
        }

        return new GoldenImageBuffer(width, height, rgba);
    }

    private static TexturePayload ResolveTextureBySuffix(ProceduralLitMaterialBundle bundle, string keySuffix)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(keySuffix);

        for (int i = 0; i < bundle.Textures.Count; i++)
        {
            ProceduralTextureExport texture = bundle.Textures[i];
            if (texture.Key.EndsWith(keySuffix, StringComparison.Ordinal))
            {
                return new TexturePayload(texture.Width, texture.Height, texture.Rgba8);
            }
        }

        throw new InvalidDataException($"Could not resolve texture ending with '{keySuffix}'.");
    }

    private static LevelMeshChunk BuildChunkFromPath(string assetPath, uint seed)
    {
        string normalized = assetPath.Replace('\\', '/').ToLowerInvariant();
        LevelNodeType type = normalized.Contains("corridor", StringComparison.Ordinal)
            ? LevelNodeType.Corridor
            : normalized.Contains("junction", StringComparison.Ordinal)
                ? LevelNodeType.Junction
                : normalized.Contains("deadend", StringComparison.Ordinal)
                    ? LevelNodeType.DeadEnd
                    : normalized.Contains("shaft", StringComparison.Ordinal)
                        ? LevelNodeType.Shaft
                        : LevelNodeType.Room;
        int variant = (int)(Hash(seed, 17u, 29u) & 0x3u);
        int nodeId = (int)(Hash(seed, 41u, 53u) & 0x7FFFu);
        string typeTag = type.ToString().ToLowerInvariant();
        return new LevelMeshChunk(nodeId, $"chunk/{typeTag}/v{variant}");
    }

    private static void WritePixel(byte[] rgba, int offset, Vector3 color)
    {
        Vector3 displayColor = ToneMapToDisplay(color);
        rgba[offset] = ToByte(displayColor.X * 255f);
        rgba[offset + 1] = ToByte(displayColor.Y * 255f);
        rgba[offset + 2] = ToByte(displayColor.Z * 255f);
        rgba[offset + 3] = 255;
    }

    private static byte[] ConvertLinearToDisplay(byte[] linearRgba)
    {
        var display = new byte[linearRgba.Length];
        for (int i = 0; i < linearRgba.Length; i += 4)
        {
            var linear = new Vector3(
                linearRgba[i] / 255f,
                linearRgba[i + 1] / 255f,
                linearRgba[i + 2] / 255f);
            Vector3 mapped = ToneMapToDisplay(linear);
            display[i] = ToByte(mapped.X * 255f);
            display[i + 1] = ToByte(mapped.Y * 255f);
            display[i + 2] = ToByte(mapped.Z * 255f);
            display[i + 3] = linearRgba[i + 3];
        }

        return display;
    }

    private readonly record struct TexturePayload(int Width, int Height, byte[] Rgba8);
    private readonly record struct MeshRasterVertex(
        float X,
        float Y,
        float Z,
        Vector3 Normal,
        Vector3 Tangent,
        Vector3 Bitangent,
        Vector2 Uv,
        Vector3 Color);
}
