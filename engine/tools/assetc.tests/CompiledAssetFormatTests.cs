using Assetc;
using Engine.AssetPipeline;

namespace Assetc.Tests;

public sealed class CompiledAssetFormatTests
{
    [Fact]
    public void Build_CompilesTextureAndMeshIntoVersionedBinaryFormats()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "textures"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "meshes"));

            string texturePath = Path.Combine(tempRoot, "textures", "pixel.png");
            string meshPath = Path.Combine(tempRoot, "meshes", "cube.gltf");
            File.WriteAllBytes(texturePath, Convert.FromBase64String(OneByOnePngBase64));
            File.WriteAllText(meshPath, """
            {
              "asset": { "version": "2.0" },
              "scenes": [],
              "nodes": [],
              "meshes": []
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
                      "path": "textures/pixel.png",
                      "kind": "texture"
                    },
                    {
                      "path": "meshes/cube.gltf",
                      "kind": "mesh"
                    }
                  ]
                }
                """);

            using var output = new StringWriter();
            using var error = new StringWriter();
            AssetcApp app = new(output, error);

            int code = app.Run(["build", "--manifest", manifestPath, "--output", pakPath]);

            Assert.Equal(0, code);

            PakArchive pak = AssetPipelineService.ReadPak(pakPath);
            Assert.Equal(2, pak.Entries.Count);

            PakEntry textureEntry = pak.Entries.Single(x => x.Kind == "texture");
            string textureCompiledPath = ResolveCompiledPath(pakPath, textureEntry.CompiledPath);
            Assert.True(File.Exists(textureCompiledPath));
            VerifyTextureBinary(textureCompiledPath, expectedWidth: 1u, expectedHeight: 1u);

            PakEntry meshEntry = pak.Entries.Single(x => x.Kind == "mesh");
            string meshCompiledPath = ResolveCompiledPath(pakPath, meshEntry.CompiledPath);
            Assert.True(File.Exists(meshCompiledPath));
            VerifyMeshBinary(meshCompiledPath, expectedSourceKind: 1u);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static void VerifyTextureBinary(string filePath, uint expectedWidth, uint expectedHeight)
    {
        using FileStream stream = File.OpenRead(filePath);
        using BinaryReader reader = new(stream);

        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        uint width = reader.ReadUInt32();
        uint height = reader.ReadUInt32();
        ulong payloadLength = reader.ReadUInt64();

        Assert.Equal(CompiledAssetFormat.TextureMagic, magic);
        Assert.Equal(CompiledAssetFormat.TextureVersion, version);
        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
        Assert.True(payloadLength > 0u);
    }

    private static void VerifyMeshBinary(string filePath, uint expectedSourceKind)
    {
        using FileStream stream = File.OpenRead(filePath);
        using BinaryReader reader = new(stream);

        uint magic = reader.ReadUInt32();
        uint version = reader.ReadUInt32();
        uint sourceKind = reader.ReadUInt32();
        ulong payloadLength = reader.ReadUInt64();

        Assert.Equal(CompiledAssetFormat.MeshMagic, magic);
        Assert.Equal(CompiledAssetFormat.MeshVersion, version);
        Assert.Equal(expectedSourceKind, sourceKind);
        Assert.True(payloadLength > 0u);
    }

    private static string ResolveCompiledPath(string pakPath, string compiledRelativePath)
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(pakPath)!,
                "compiled",
                compiledRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"assetc-formats-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private const string OneByOnePngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6nG2QAAAAASUVORK5CYII=";
}
