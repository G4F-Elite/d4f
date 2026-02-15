using Assetc;
using Engine.AssetPipeline;
using Engine.Scenes;

namespace Assetc.Tests;

public sealed class AssetcAppValidationTests
{
    [Fact]
    public void Run_ShouldFailBuild_WhenManifestDoesNotExist()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        AssetcApp app = new(output, error);

        int code = app.Run(["build", "--manifest", "missing.json", "--output", "content.pak"]);

        Assert.Equal(1, code);
        Assert.Contains("Manifest file was not found", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFailBuild_WhenReferencedAssetMissing()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "assets": [
                    {
                      "path": "textures/missing.png",
                      "kind": "texture"
                    }
                  ]
                }
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            AssetcApp app = new(output, error);

            int code = app.Run(["build", "--manifest", manifestPath, "--output", Path.Combine(tempRoot, "content.pak")]);

            Assert.Equal(1, code);
            Assert.Contains("Asset file was not found", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldBuildAndListPak_WhenManifestAndAssetsValid()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "textures"));
            File.WriteAllText(Path.Combine(tempRoot, "textures", "hero.png"), "png-data");

            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            string pakPath = Path.Combine(tempRoot, "content.pak");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "assets": [
                    {
                      "path": "textures/hero.png",
                      "kind": "texture"
                    }
                  ]
                }
                """);

            using var buildOutput = new StringWriter();
            using var buildError = new StringWriter();
            AssetcApp buildApp = new(buildOutput, buildError);

            int buildCode = buildApp.Run(["build", "--manifest", manifestPath, "--output", pakPath]);

            Assert.Equal(0, buildCode);
            Assert.True(File.Exists(pakPath));

            using var listOutput = new StringWriter();
            using var listError = new StringWriter();
            AssetcApp listApp = new(listOutput, listError);

            int listCode = listApp.Run(["list", "--pak", pakPath]);

            Assert.Equal(0, listCode);
            Assert.Contains("texture\ttextures/hero.png", listOutput.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Run_ShouldCompileSceneAsset_WhenManifestContainsSceneKind()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "scenes"));
            string scenePath = Path.Combine(tempRoot, "scenes", "level.scene.json");
            File.WriteAllText(
                scenePath,
                """
                {
                  "entities": [
                    { "stableId": 1, "name": "Player" }
                  ],
                  "components": [
                    {
                      "entityStableId": 1,
                      "typeId": "Tag",
                      "payloadBase64": "TWFpbg=="
                    }
                  ]
                }
                """);

            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            string pakPath = Path.Combine(tempRoot, "content.pak");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "assets": [
                    {
                      "path": "scenes/level.scene.json",
                      "kind": "scene"
                    }
                  ]
                }
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            AssetcApp app = new(output, error);

            int buildCode = app.Run(["build", "--manifest", manifestPath, "--output", pakPath]);

            Assert.Equal(0, buildCode);
            PakArchive pak = AssetPipelineService.ReadPak(pakPath);
            PakEntry entry = Assert.Single(pak.Entries);
            Assert.Equal("scene", entry.Kind);

            string compiledFullPath = Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(pakPath)!,
                    "compiled",
                    entry.CompiledPath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(compiledFullPath));

            using FileStream compiledStream = File.OpenRead(compiledFullPath);
            SceneAsset scene = SceneBinaryCodec.ReadScene(compiledStream);
            Assert.Single(scene.Entities);
            Assert.Single(scene.Components);
            Assert.Equal(1u, scene.Entities[0].StableId);
            Assert.Equal("Tag", scene.Components[0].TypeId);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"assetc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
