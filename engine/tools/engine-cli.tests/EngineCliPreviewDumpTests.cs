using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewDumpTests
{
    [Fact]
    public void Parse_ShouldCreatePreviewDumpCommand_WithDefaultManifest()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["preview", "dump", "--project", "game"]);

        PreviewDumpCommand command = Assert.IsType<PreviewDumpCommand>(result.Command);
        Assert.Equal("game", command.ProjectDirectory);
        Assert.Equal(Path.Combine("game", "artifacts", "preview", "manifest.json"), command.ManifestPath);
    }

    [Fact]
    public void Parse_ShouldFailPreviewDump_WhenProjectMissing()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["preview", "dump"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--project' is required for 'preview dump'.", result.Error);
    }

    [Fact]
    public void Run_PreviewDump_ShouldPrintManifestEntries()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string previewDirectory = Path.Combine(tempRoot, "artifacts", "preview");
            Directory.CreateDirectory(previewDirectory);
            File.WriteAllText(
                Path.Combine(previewDirectory, "manifest.json"),
                """
                {
                  "generatedAtUtc": "2026-02-16T12:34:56.0000000Z",
                  "artifacts": [
                    {
                      "kind": "mesh-preview",
                      "relativePath": "meshes/cube.png",
                      "description": "Mesh preview image."
                    },
                    {
                      "kind": "audio-waveform-preview",
                      "relativePath": "audio/ambience.waveform.png",
                      "description": "Waveform preview image."
                    }
                  ]
                }
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["preview", "dump", "--project", tempRoot]);

            Assert.Equal(0, code);
            string text = output.ToString();
            Assert.Contains("Preview artifact manifest:", text, StringComparison.Ordinal);
            Assert.Contains("mesh-preview\tmeshes/cube.png\tMesh preview image.", text, StringComparison.Ordinal);
            Assert.Contains("audio-waveform-preview\taudio/ambience.waveform.png\tWaveform preview image.", text, StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_PreviewDump_ShouldPrintGalleryEntries_WhenGalleryArtifactPresent()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string previewDirectory = Path.Combine(tempRoot, "artifacts", "preview");
            string galleryDirectory = Path.Combine(previewDirectory, "gallery");
            Directory.CreateDirectory(galleryDirectory);
            File.WriteAllText(
                Path.Combine(previewDirectory, "manifest.json"),
                """
                {
                  "generatedAtUtc": "2026-02-16T12:34:56.0000000Z",
                  "artifacts": [
                    {
                      "kind": "preview-gallery",
                      "relativePath": "gallery/gallery.json",
                      "description": "Preview gallery metadata."
                    }
                  ]
                }
                """);

            File.WriteAllText(
                Path.Combine(galleryDirectory, "gallery.json"),
                """
                [
                  {
                    "path": "Gameplay/Rock",
                    "kind": "texture-preview",
                    "category": "environment",
                    "tags": ["procedural", "rock"],
                    "previewPath": "textures/rock.png"
                  }
                ]
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["preview", "dump", "--project", tempRoot]);

            Assert.Equal(0, code);
            string text = output.ToString();
            Assert.Contains("Preview gallery entries:", text, StringComparison.Ordinal);
            Assert.Contains(
                "gallery\tGameplay/Rock\ttexture-preview\tenvironment\tprocedural,rock\ttextures/rock.png",
                text,
                StringComparison.Ordinal);
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_PreviewDump_ShouldFail_WhenManifestMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            EngineCliApp app = new(output, error);

            int code = app.Run(["preview", "dump", "--project", tempRoot]);

            Assert.Equal(1, code);
            Assert.Contains("Artifact manifest was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-preview-dump-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
