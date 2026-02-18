using Engine.AssetPipeline;

namespace Assetc.Tests;

public sealed class AssetManifestMetadataTests
{
    [Fact]
    public void LoadManifest_ShouldParseCategoryAndTags()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "assets": [
                    {
                      "path": "mesh/cube.mesh",
                      "kind": "mesh",
                      "category": " geometry ",
                      "tags": ["hero", "hard-surface", "hero", "  trim  "]
                    }
                  ]
                }
                """);

            AssetManifest manifest = AssetPipelineService.LoadManifest(manifestPath);
            AssetManifestEntry asset = Assert.Single(manifest.Assets);

            Assert.Equal("geometry", asset.Category);
            Assert.Equal(["hero", "hard-surface", "trim"], asset.Tags);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadManifest_ShouldRejectInvalidTagsShape()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "assets": [
                    {
                      "path": "textures/noise.tex",
                      "kind": "texture",
                      "tags": "not-an-array"
                    }
                  ]
                }
                """);

            InvalidDataException exception =
                Assert.Throws<InvalidDataException>(() => AssetPipelineService.LoadManifest(manifestPath));
            Assert.Contains("assets[0].tags", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadManifest_ShouldRejectWhitespaceCategory()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "assets": [
                    {
                      "path": "materials/wall.mat",
                      "kind": "material",
                      "category": "   "
                    }
                  ]
                }
                """);

            InvalidDataException exception =
                Assert.Throws<InvalidDataException>(() => AssetPipelineService.LoadManifest(manifestPath));
            Assert.Contains("assets[0].category", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"asset-manifest-metadata-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
