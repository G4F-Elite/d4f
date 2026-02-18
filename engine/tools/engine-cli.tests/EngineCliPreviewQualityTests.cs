using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Engine.Cli;
using Engine.Procedural;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewQualityTests
{
    [Fact]
    public void Run_Preview_ShouldProduceDifferentMeshThumbnails_ForDifferentMeshTags()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareMeshComparisonManifest(tempRoot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);
            string previewRoot = Path.Combine(tempRoot, "artifacts", "preview");
            string roomPreview = Path.Combine(previewRoot, "meshes", "mesh_room.png");
            string shaftPreview = Path.Combine(previewRoot, "meshes", "mesh_shaft.png");
            Assert.True(File.Exists(roomPreview));
            Assert.True(File.Exists(shaftPreview));

            byte[] roomBytes = File.ReadAllBytes(roomPreview);
            byte[] shaftBytes = File.ReadAllBytes(shaftPreview);
            Assert.NotEqual(roomBytes, shaftBytes);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_Preview_ShouldProduceChromaticAndDetailedImages()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDefaultManifest(tempRoot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);
            string previewRoot = Path.Combine(tempRoot, "artifacts", "preview");
            byte[] meshRgba = DecodePngRgba(Path.Combine(previewRoot, "meshes", "mesh_cube.png"), out _, out _);
            byte[] textureRgba = DecodePngRgba(Path.Combine(previewRoot, "textures", "textures_noise.png"), out _, out _);
            byte[] materialRgba = DecodePngRgba(Path.Combine(previewRoot, "materials", "materials_wall.png"), out _, out _);

            Assert.True(HasChromaticPixels(meshRgba));
            Assert.True(HasChromaticPixels(textureRgba));
            Assert.True(HasChromaticPixels(materialRgba));
            Assert.True(HasLuminanceVariance(meshRgba));
            Assert.True(HasLuminanceVariance(materialRgba));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_Preview_ShouldProduceBalancedMeshHighlightsAndShadows()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDefaultManifest(tempRoot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);
            byte[] meshRgba = DecodePngRgba(
                Path.Combine(tempRoot, "artifacts", "preview", "meshes", "mesh_cube.png"),
                out _,
                out _);

            (double brightFraction, _) = ComputeBrightnessFractions(meshRgba);
            Assert.InRange(brightFraction, 0.01, 0.45);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_Preview_TexturePreviewShouldApplyDisplayTransform()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PrepareDefaultManifest(tempRoot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview"
            ]);

            Assert.Equal(0, code);
            byte[] previewRgba = DecodePngRgba(
                Path.Combine(tempRoot, "artifacts", "preview", "textures", "textures_noise.png"),
                out _,
                out _);

            const string assetPath = "textures/noise.tex";
            uint seed = ComputePreviewSeed("texture", assetPath);
            LevelMeshChunk chunk = BuildChunkFromPathForTest(assetPath, seed);
            ProceduralTextureSurface surface = ProceduralChunkSurfaceCatalog.BuildChunkSurface(chunk, seed, 96, 96);

            Assert.NotEqual(surface.AlbedoRgba8, previewRgba);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void PrepareDefaultManifest(string rootPath)
    {
        string assetsDirectory = Path.Combine(rootPath, "assets");
        Directory.CreateDirectory(assetsDirectory);

        WriteAssetFile(assetsDirectory, "mesh/cube.mesh", "mesh-data");
        WriteAssetFile(assetsDirectory, "textures/noise.tex", "texture-data");
        WriteAssetFile(assetsDirectory, "materials/wall.mat", "material-data");
        WriteAssetFile(assetsDirectory, "audio/ambience.wav", "audio-data");

        File.WriteAllText(
            Path.Combine(assetsDirectory, "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                { "path": "mesh/cube.mesh", "kind": "mesh" },
                { "path": "textures/noise.tex", "kind": "texture" },
                { "path": "materials/wall.mat", "kind": "material" },
                { "path": "audio/ambience.wav", "kind": "audio" }
              ]
            }
            """);
    }

    private static void PrepareMeshComparisonManifest(string rootPath)
    {
        string assetsDirectory = Path.Combine(rootPath, "assets");
        Directory.CreateDirectory(assetsDirectory);

        WriteAssetFile(assetsDirectory, "mesh/room.mesh", "mesh-room-data");
        WriteAssetFile(assetsDirectory, "mesh/shaft.mesh", "mesh-shaft-data");

        File.WriteAllText(
            Path.Combine(assetsDirectory, "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                { "path": "mesh/room.mesh", "kind": "mesh" },
                { "path": "mesh/shaft.mesh", "kind": "mesh" }
              ]
            }
            """);
    }

    private static void WriteAssetFile(string assetsDirectory, string relativePath, string content)
    {
        string fullPath = Path.Combine(assetsDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
    }

    private static bool HasChromaticPixels(byte[] rgba)
    {
        for (int i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i] != rgba[i + 1] || rgba[i + 1] != rgba[i + 2])
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLuminanceVariance(byte[] rgba)
    {
        double sum = 0.0;
        double sumSquares = 0.0;
        int count = 0;
        for (int i = 0; i < rgba.Length; i += 4)
        {
            double luma = (0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2]) / 255.0;
            sum += luma;
            sumSquares += luma * luma;
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        double mean = sum / count;
        double variance = Math.Max(0.0, (sumSquares / count) - (mean * mean));
        return variance > 0.0005;
    }

    private static (double BrightFraction, double DarkFraction) ComputeBrightnessFractions(byte[] rgba)
    {
        int pixelCount = rgba.Length / 4;
        if (pixelCount == 0)
        {
            return (0.0, 0.0);
        }

        int bright = 0;
        int dark = 0;
        for (int i = 0; i < rgba.Length; i += 4)
        {
            double luma = (0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2]) / 255.0;
            if (luma >= 0.72)
            {
                bright++;
            }

            if (luma <= 0.18)
            {
                dark++;
            }
        }

        return (bright / (double)pixelCount, dark / (double)pixelCount);
    }

    private static uint ComputePreviewSeed(string kind, string assetPath)
    {
        string value = $"{kind}|{assetPath}";
        uint hash = 2166136261u;
        foreach (char ch in value)
        {
            hash ^= ch;
            hash *= 16777619u;
        }

        return hash;
    }

    private static LevelMeshChunk BuildChunkFromPathForTest(string assetPath, uint seed)
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

    private static uint Hash(uint seed, uint x, uint y)
    {
        uint value = seed ^ (x * 374761393u) ^ (y * 668265263u);
        value ^= value >> 13;
        value *= 1274126177u;
        value ^= value >> 16;
        return value;
    }

    private static byte[] DecodePngRgba(string path, out int width, out int height)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 33);
        Assert.Equal((byte)0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);

        width = 0;
        height = 0;
        int index = 8;
        using var idat = new MemoryStream();
        while (index + 8 <= bytes.Length)
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(index, 4));
            index += 4;
            string chunkType = Encoding.ASCII.GetString(bytes, index, 4);
            index += 4;

            Assert.True(index + length + 4 <= bytes.Length);
            ReadOnlySpan<byte> payload = bytes.AsSpan(index, checked((int)length));
            index += checked((int)length);
            index += 4; // CRC

            if (chunkType == "IHDR")
            {
                width = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(0, 4));
                height = (int)BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
            }
            else if (chunkType == "IDAT")
            {
                idat.Write(payload);
            }
            else if (chunkType == "IEND")
            {
                break;
            }
        }

        Assert.True(width > 0);
        Assert.True(height > 0);

        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionMode.Decompress, leaveOpen: true))
        {
            zlib.CopyTo(inflated);
        }

        byte[] scanlines = inflated.ToArray();
        int stride = checked(width * 4);
        Assert.Equal((stride + 1) * height, scanlines.Length);

        var rgba = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        {
            int scanlineOffset = y * (stride + 1);
            Assert.Equal(0, scanlines[scanlineOffset]); // filter type: none
            Buffer.BlockCopy(scanlines, scanlineOffset + 1, rgba, y * stride, stride);
        }

        return rgba;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-preview-quality-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
