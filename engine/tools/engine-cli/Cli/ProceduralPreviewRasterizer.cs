using System.Numerics;
using Engine.Procedural;
using Engine.Testing;

namespace Engine.Cli;

internal static class ProceduralPreviewRasterizer
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
            surfaceWidth: 64,
            surfaceHeight: 64);
        TexturePayload albedo = ResolveTextureBySuffix(content.MaterialBundle, ".albedo");
        return RasterizeMesh(content.Mesh, albedo.Rgba8, albedo.Width, albedo.Height, width, height, seed);
    }

    private static GoldenImageBuffer BuildTexturePreview(LevelMeshChunk chunk, uint seed, int width, int height)
    {
        ProceduralTextureSurface surface = ProceduralChunkSurfaceCatalog.BuildChunkSurface(
            chunk,
            seed,
            width,
            height);
        return new GoldenImageBuffer(width, height, surface.AlbedoRgba8.ToArray());
    }

    private static GoldenImageBuffer BuildMaterialPreview(LevelMeshChunk chunk, uint seed, int width, int height)
    {
        ProceduralChunkContent content = ProceduralChunkContentFactory.Build(
            chunk,
            seed,
            surfaceWidth: Math.Max(width, 64),
            surfaceHeight: Math.Max(height, 64));
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

                Vector3 perturbedNormal = Vector3.Normalize(new Vector3(
                    sphereNormal.X + normalSample.X * 0.28f,
                    sphereNormal.Y + normalSample.Y * 0.28f,
                    MathF.Max(0.2f, sphereNormal.Z + normalSample.Z * 0.18f)));

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
        byte[] albedo,
        int albedoWidth,
        int albedoHeight,
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
            Vector3 normal = Vector3.Normalize(Vector3.TransformNormal(src.Normal, rotation));
            transformed[i] = new MeshRasterVertex(sx, sy, world.Z, normal, src.Uv);
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
                    Vector3 normal = Vector3.Normalize(
                        (v0.Normal * b0) +
                        (v1.Normal * b1) +
                        (v2.Normal * b2));
                    Vector2 uv = (v0.Uv * b0) + (v1.Uv * b1) + (v2.Uv * b2);
                    Vector3 albedoSample = SampleRgb(albedo, albedoWidth, albedoHeight, uv.X, uv.Y);

                    float ndotl = MathF.Max(0f, Vector3.Dot(normal, lightDir));
                    float rim = MathF.Pow(1f - MathF.Max(0f, Vector3.Dot(normal, viewDir)), 2f) * 0.22f;
                    Vector3 color = albedoSample * (0.16f + ndotl * 0.84f) + Vector3.One * rim * 0.12f;
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

    private static Vector3 SampleRgb(TexturePayload texture, float u, float v)
    {
        return SampleRgb(texture.Rgba8, texture.Width, texture.Height, u, v);
    }

    private static Vector3 SampleRgb(byte[] rgba, int width, int height, float u, float v)
    {
        (int x, int y) = WrapUvToPixel(width, height, u, v);
        int offset = ((y * width) + x) * 4;
        return new Vector3(
            rgba[offset] / 255f,
            rgba[offset + 1] / 255f,
            rgba[offset + 2] / 255f);
    }

    private static Vector3 SampleNormal(TexturePayload texture, float u, float v)
    {
        Vector3 encoded = SampleRgb(texture, u, v);
        Vector3 normal = new(
            encoded.X * 2f - 1f,
            encoded.Y * 2f - 1f,
            encoded.Z * 2f - 1f);
        return Vector3.Normalize(normal);
    }

    private static float SampleGray(TexturePayload texture, float u, float v)
    {
        (int x, int y) = WrapUvToPixel(texture.Width, texture.Height, u, v);
        int offset = ((y * texture.Width) + x) * 4;
        return texture.Rgba8[offset] / 255f;
    }

    private static (int X, int Y) WrapUvToPixel(int width, int height, float u, float v)
    {
        float wrappedU = u - MathF.Floor(u);
        float wrappedV = v - MathF.Floor(v);
        int x = Math.Clamp((int)(wrappedU * Math.Max(width - 1, 0)), 0, Math.Max(0, width - 1));
        int y = Math.Clamp((int)((1f - wrappedV) * Math.Max(height - 1, 0)), 0, Math.Max(0, height - 1));
        return (x, y);
    }

    private static void WritePixel(byte[] rgba, int offset, Vector3 color)
    {
        rgba[offset] = ToByte(color.X * 255f);
        rgba[offset + 1] = ToByte(color.Y * 255f);
        rgba[offset + 2] = ToByte(color.Z * 255f);
        rgba[offset + 3] = 255;
    }

    private static float Edge(float ax, float ay, float bx, float by, float px, float py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private static uint Hash(uint seed, uint x, uint y)
    {
        uint value = seed ^ (x * 374761393u) ^ (y * 668265263u);
        value ^= value >> 13;
        value *= 1274126177u;
        value ^= value >> 16;
        return value;
    }

    private static float Sample01(uint seed, uint x, uint y)
    {
        return (Hash(seed, x, y) & 0x00FFFFFFu) / 16777215f;
    }

    private static byte ToByte(float value)
    {
        int rounded = (int)MathF.Round(value);
        if (rounded <= 0)
        {
            return 0;
        }

        return rounded >= 255 ? (byte)255 : (byte)rounded;
    }

    private readonly record struct TexturePayload(int Width, int Height, byte[] Rgba8);

    private readonly record struct MeshRasterVertex(float X, float Y, float Z, Vector3 Normal, Vector2 Uv);
}
