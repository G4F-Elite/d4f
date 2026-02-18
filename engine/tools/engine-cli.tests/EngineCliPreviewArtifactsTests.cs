using System.Text.Json;
using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewArtifactsTests
{
    [Fact]
    public void Run_Preview_ShouldGenerateDeterministicThumbnailsAndWaveformArtifacts()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PreparePreviewManifest(tempRoot);
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
            string meshPreview = Path.Combine(previewRoot, "meshes", "mesh_cube.png");
            string texturePreview = Path.Combine(previewRoot, "textures", "textures_noise.png");
            string materialPreview = Path.Combine(previewRoot, "materials", "materials_wall.png");
            string audioPreview = Path.Combine(previewRoot, "audio", "audio_ambience.png");
            string waveformImage = Path.Combine(previewRoot, "audio", "audio_ambience.waveform.png");
            string waveformMetadata = Path.Combine(previewRoot, "audio", "audio_ambience.waveform.json");
            string compressedWaveform = Path.Combine(previewRoot, "audio", "audio_ambience.waveform.pcm16.gz");
            string galleryPath = Path.Combine(previewRoot, "gallery", "gallery.json");
            string manifestPath = Path.Combine(previewRoot, "manifest.json");

            Assert.True(File.Exists(meshPreview));
            Assert.True(File.Exists(texturePreview));
            Assert.True(File.Exists(materialPreview));
            Assert.True(File.Exists(audioPreview));
            Assert.True(File.Exists(waveformImage));
            Assert.True(File.Exists(waveformMetadata));
            Assert.True(File.Exists(compressedWaveform));
            Assert.True(File.Exists(galleryPath));
            Assert.True(File.Exists(manifestPath));
            Assert.True(new FileInfo(compressedWaveform).Length > 0);

            Assert.Equal(((uint)96, (uint)96), ReadPngDimensions(meshPreview));
            Assert.Equal(((uint)256, (uint)64), ReadPngDimensions(waveformImage));

            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement artifacts = manifest.RootElement.GetProperty("artifacts");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "audio-waveform-preview");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "audio-waveform");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "audio-compressed-preview");
            Assert.Contains(artifacts.EnumerateArray(), static entry =>
                entry.GetProperty("kind").GetString() == "preview-gallery");

            using JsonDocument gallery = JsonDocument.Parse(File.ReadAllText(galleryPath));
            JsonElement galleryEntries = gallery.RootElement;
            Assert.Equal(JsonValueKind.Array, galleryEntries.ValueKind);
            JsonElement meshEntry = galleryEntries.EnumerateArray().Single(static x =>
                x.GetProperty("path").GetString() == "mesh/cube.mesh");
            Assert.Equal("geometry", meshEntry.GetProperty("category").GetString());
            Assert.Contains(meshEntry.GetProperty("tags").EnumerateArray(), static x =>
                x.GetString() == "hero");
            JsonElement audioEntry = galleryEntries.EnumerateArray().Single(static x =>
                x.GetProperty("path").GetString() == "audio/ambience.wav");
            Assert.Equal("audio", audioEntry.GetProperty("category").GetString());
            Assert.Equal("audio/audio_ambience.png", audioEntry.GetProperty("previewPath").GetString());

            using JsonDocument waveform = JsonDocument.Parse(File.ReadAllText(waveformMetadata));
            Assert.Equal("audio/ambience.wav", waveform.RootElement.GetProperty("source").GetString());
            Assert.Equal("audio", waveform.RootElement.GetProperty("kind").GetString());
            Assert.Equal("audio/audio_ambience.waveform.pcm16.gz", waveform.RootElement.GetProperty("compressedPreviewPath").GetString());
            Assert.True(waveform.RootElement.GetProperty("samples").GetArrayLength() >= 128);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_Preview_ShouldProduceStableImageBytesAcrossRuns()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PreparePreviewManifest(tempRoot);
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int first = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview-a"
            ]);
            int second = app.Run(
            [
                "preview",
                "--project", tempRoot,
                "--manifest", "assets/manifest.json",
                "--out", "artifacts/preview-b"
            ]);

            Assert.Equal(0, first);
            Assert.Equal(0, second);

            byte[] firstMesh = File.ReadAllBytes(Path.Combine(tempRoot, "artifacts", "preview-a", "meshes", "mesh_cube.png"));
            byte[] secondMesh = File.ReadAllBytes(Path.Combine(tempRoot, "artifacts", "preview-b", "meshes", "mesh_cube.png"));
            byte[] firstWave = File.ReadAllBytes(Path.Combine(tempRoot, "artifacts", "preview-a", "audio", "audio_ambience.waveform.png"));
            byte[] secondWave = File.ReadAllBytes(Path.Combine(tempRoot, "artifacts", "preview-b", "audio", "audio_ambience.waveform.png"));
            byte[] firstCompressed = File.ReadAllBytes(Path.Combine(tempRoot, "artifacts", "preview-a", "audio", "audio_ambience.waveform.pcm16.gz"));
            byte[] secondCompressed = File.ReadAllBytes(Path.Combine(tempRoot, "artifacts", "preview-b", "audio", "audio_ambience.waveform.pcm16.gz"));

            Assert.Equal(firstMesh, secondMesh);
            Assert.Equal(firstWave, secondWave);
            Assert.Equal(firstCompressed, secondCompressed);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void PreparePreviewManifest(string rootPath)
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
                { "path": "mesh/cube.mesh", "kind": "mesh", "category": "geometry", "tags": ["hero", "hard-surface", "hero"] },
                { "path": "textures/noise.tex", "kind": "texture" },
                { "path": "materials/wall.mat", "kind": "material" },
                { "path": "audio/ambience.wav", "kind": "audio", "tags": ["ambient", "loop"] }
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

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-preview-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static (uint Width, uint Height) ReadPngDimensions(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        Assert.True(bytes.Length >= 24);
        Assert.Equal((byte)0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
        Assert.Equal((byte)'I', bytes[12]);
        Assert.Equal((byte)'H', bytes[13]);
        Assert.Equal((byte)'D', bytes[14]);
        Assert.Equal((byte)'R', bytes[15]);

        uint width = ((uint)bytes[16] << 24) | ((uint)bytes[17] << 16) | ((uint)bytes[18] << 8) | bytes[19];
        uint height = ((uint)bytes[20] << 24) | ((uint)bytes[21] << 16) | ((uint)bytes[22] << 8) | bytes[23];
        return (width, height);
    }
}
