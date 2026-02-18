using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewArtifactsDeterminismTests
{
    [Fact]
    public void Run_ShouldGenerateDeterministicPreviews_ForMeshTextureAndMaterial()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            PreparePreviewManifest(tempRoot);

            int firstCode = RunPreview(tempRoot, "artifacts/preview-a");
            int secondCode = RunPreview(tempRoot, "artifacts/preview-b");

            Assert.Equal(0, firstCode);
            Assert.Equal(0, secondCode);

            string meshAPath = Path.Combine(tempRoot, "artifacts", "preview-a", "meshes", "mesh_chunk_room.png");
            string textureAPath = Path.Combine(tempRoot, "artifacts", "preview-a", "textures", "texture_chunk_room.png");
            string materialAPath = Path.Combine(tempRoot, "artifacts", "preview-a", "materials", "material_chunk_room.png");

            string meshBPath = Path.Combine(tempRoot, "artifacts", "preview-b", "meshes", "mesh_chunk_room.png");
            string textureBPath = Path.Combine(tempRoot, "artifacts", "preview-b", "textures", "texture_chunk_room.png");
            string materialBPath = Path.Combine(tempRoot, "artifacts", "preview-b", "materials", "material_chunk_room.png");

            Assert.True(File.Exists(meshAPath));
            Assert.True(File.Exists(textureAPath));
            Assert.True(File.Exists(materialAPath));
            Assert.True(File.Exists(meshBPath));
            Assert.True(File.Exists(textureBPath));
            Assert.True(File.Exists(materialBPath));

            byte[] meshA = File.ReadAllBytes(meshAPath);
            byte[] textureA = File.ReadAllBytes(textureAPath);
            byte[] materialA = File.ReadAllBytes(materialAPath);

            byte[] meshB = File.ReadAllBytes(meshBPath);
            byte[] textureB = File.ReadAllBytes(textureBPath);
            byte[] materialB = File.ReadAllBytes(materialBPath);

            Assert.Equal(meshA, meshB);
            Assert.Equal(textureA, textureB);
            Assert.Equal(materialA, materialB);

            Assert.NotEqual(meshA, textureA);
            Assert.NotEqual(meshA, materialA);
            Assert.NotEqual(textureA, materialA);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static int RunPreview(string projectRoot, string outputPath)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var app = new EngineCliApp(output, error);
        return app.Run(
        [
            "preview",
            "--project", projectRoot,
            "--manifest", "assets/manifest.json",
            "--out", outputPath
        ]);
    }

    private static void PreparePreviewManifest(string rootPath)
    {
        string assetsDirectory = Path.Combine(rootPath, "assets");
        Directory.CreateDirectory(Path.Combine(assetsDirectory, "mesh"));
        Directory.CreateDirectory(Path.Combine(assetsDirectory, "texture"));
        Directory.CreateDirectory(Path.Combine(assetsDirectory, "material"));

        File.WriteAllBytes(Path.Combine(assetsDirectory, "mesh", "chunk_room.mesh"), [1, 7, 11, 13]);
        File.WriteAllBytes(Path.Combine(assetsDirectory, "texture", "chunk_room.tex"), [3, 5, 8, 13, 21]);
        File.WriteAllBytes(Path.Combine(assetsDirectory, "material", "chunk_room.mat"), [2, 4, 6, 8, 10, 12]);

        File.WriteAllText(
            Path.Combine(assetsDirectory, "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                {
                  "path": "mesh/chunk_room.mesh",
                  "kind": "mesh"
                },
                {
                  "path": "texture/chunk_room.tex",
                  "kind": "texture"
                },
                {
                  "path": "material/chunk_room.mat",
                  "kind": "material"
                }
              ]
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"engine-cli-preview-determinism-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
