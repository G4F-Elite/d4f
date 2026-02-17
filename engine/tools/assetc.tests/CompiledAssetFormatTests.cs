using Assetc;
using Engine.AssetPipeline;
using Engine.Content;

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
                  "version": 1,
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
            IReadOnlyList<PakEntry> compiledManifest = CompiledManifestCodec.Read(
                Path.Combine(tempRoot, AssetPipelineService.CompiledManifestFileName));
            Assert.Equal(2, compiledManifest.Count);
            Assert.All(pak.Entries, static entry => Assert.True(entry.OffsetBytes >= 0));
            Assert.All(pak.Entries, static entry => Assert.False(string.IsNullOrWhiteSpace(entry.AssetKey)));

            PakEntry textureEntry = pak.Entries.Single(x => x.Kind == "texture");
            string textureCompiledPath = ResolveCompiledPath(pakPath, textureEntry.CompiledPath);
            Assert.True(File.Exists(textureCompiledPath));
            VerifyTextureBinary(textureCompiledPath, expectedWidth: 1u, expectedHeight: 1u);
            Assert.Equal(File.ReadAllBytes(textureCompiledPath), ReadPakPayload(pakPath, textureEntry));

            PakEntry meshEntry = pak.Entries.Single(x => x.Kind == "mesh");
            string meshCompiledPath = ResolveCompiledPath(pakPath, meshEntry.CompiledPath);
            Assert.True(File.Exists(meshCompiledPath));
            VerifyMeshBinary(meshCompiledPath, expectedSourceKind: 1u);
            Assert.Equal(File.ReadAllBytes(meshCompiledPath), ReadPakPayload(pakPath, meshEntry));
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void Build_CompilesMaterialAndSoundIntoVersionedBinaryFormats()
    {
        string tempRoot = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(tempRoot, "materials"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "audio"));

            string materialPath = Path.Combine(tempRoot, "materials", "wall.mat");
            string soundPath = Path.Combine(tempRoot, "audio", "click.snd");
            File.WriteAllText(materialPath, "{ \"template\": \"lit\" }");
            File.WriteAllBytes(soundPath, [1, 2, 3, 4, 5, 6, 7, 8]);

            string manifestPath = Path.Combine(tempRoot, "manifest.json");
            string pakPath = Path.Combine(tempRoot, "content.pak");
            File.WriteAllText(
                manifestPath,
                """
                {
                  "version": 1,
                  "assets": [
                    {
                      "path": "materials/wall.mat",
                      "kind": "material"
                    },
                    {
                      "path": "audio/click.snd",
                      "kind": "sound"
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

            PakEntry materialEntry = pak.Entries.Single(x => x.Kind == "material");
            string materialCompiledPath = ResolveCompiledPath(pakPath, materialEntry.CompiledPath);
            MaterialBlobData material = MaterialBlobCodec.Read(File.ReadAllBytes(materialCompiledPath));
            Assert.Equal("raw/material:v1", material.TemplateId);
            Assert.NotEmpty(material.ParameterBlock);
            Assert.Empty(material.TextureReferences);

            PakEntry soundEntry = pak.Entries.Single(x => x.Kind == "sound");
            string soundCompiledPath = ResolveCompiledPath(pakPath, soundEntry.CompiledPath);
            SoundBlobData sound = SoundBlobCodec.Read(File.ReadAllBytes(soundCompiledPath));
            Assert.Equal(SoundBlobEncoding.SourceEncoded, sound.Encoding);
            Assert.Equal(48000, sound.SampleRate);
            Assert.Equal(1, sound.Channels);
            Assert.NotEmpty(sound.Data);
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    private static void VerifyTextureBinary(string filePath, uint expectedWidth, uint expectedHeight)
    {
        TextureBlobData texture = TextureBlobCodec.Read(File.ReadAllBytes(filePath));
        Assert.Equal(CompiledAssetFormat.TextureMagic, TextureBlobCodec.Magic);
        Assert.Equal(CompiledAssetFormat.TextureVersion, TextureBlobCodec.Version);
        Assert.Equal(TextureBlobFormat.SourcePng, texture.Format);
        Assert.Equal(TextureBlobColorSpace.Srgb, texture.ColorSpace);
        Assert.Equal(checked((int)expectedWidth), texture.Width);
        Assert.Equal(checked((int)expectedHeight), texture.Height);
        Assert.Single(texture.MipChain);
        Assert.NotEmpty(texture.MipChain[0].Data);
    }

    private static void VerifyMeshBinary(string filePath, uint expectedSourceKind)
    {
        MeshBlobData mesh = MeshBlobCodec.Read(File.ReadAllBytes(filePath));
        Assert.Equal(CompiledAssetFormat.MeshMagic, MeshBlobCodec.Magic);
        Assert.Equal(CompiledAssetFormat.MeshVersion, MeshBlobCodec.Version);
        Assert.Equal(0, mesh.VertexCount);
        Assert.Empty(mesh.VertexStreams);
        Assert.Empty(mesh.IndexData);
        Assert.Empty(mesh.Submeshes);
        Assert.Equal(expectedSourceKind, mesh.SourceKind);
        Assert.NotNull(mesh.SourcePayload);
        Assert.NotEmpty(mesh.SourcePayload!);
    }

    private static string ResolveCompiledPath(string pakPath, string compiledRelativePath)
    {
        return Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(pakPath)!,
                "compiled",
                compiledRelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static byte[] ReadPakPayload(string pakPath, PakEntry entry)
    {
        using FileStream stream = File.OpenRead(pakPath);
        stream.Seek(entry.OffsetBytes, SeekOrigin.Begin);
        byte[] bytes = new byte[checked((int)entry.SizeBytes)];
        int read = stream.Read(bytes, 0, bytes.Length);
        Assert.Equal(bytes.Length, read);
        return bytes;
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
