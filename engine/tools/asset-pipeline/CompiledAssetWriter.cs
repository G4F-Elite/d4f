using System.Buffers.Binary;

namespace Engine.AssetPipeline;

internal static class CompiledAssetWriter
{
    public static void WriteTexture(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        (uint width, uint height) = TryReadPngDimensions(bytes, out uint w, out uint h)
            ? (w, h)
            : (0u, 0u);

        using FileStream output = File.Create(outputPath);
        using BinaryWriter writer = new(output);
        writer.Write(CompiledAssetFormat.TextureMagic);
        writer.Write(CompiledAssetFormat.TextureVersion);
        writer.Write(width);
        writer.Write(height);
        writer.Write((ulong)bytes.LongLength);
        writer.Write(bytes);
    }

    public static void WriteMesh(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        uint sourceKind = GetMeshSourceKind(sourcePath);

        using FileStream output = File.Create(outputPath);
        using BinaryWriter writer = new(output);
        writer.Write(CompiledAssetFormat.MeshMagic);
        writer.Write(CompiledAssetFormat.MeshVersion);
        writer.Write(sourceKind);
        writer.Write((ulong)bytes.LongLength);
        writer.Write(bytes);
    }

    public static void WriteRaw(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);

        using FileStream output = File.Create(outputPath);
        using BinaryWriter writer = new(output);
        writer.Write(CompiledAssetFormat.RawMagic);
        writer.Write(CompiledAssetFormat.RawVersion);
        writer.Write((ulong)bytes.LongLength);
        writer.Write(bytes);
    }

    public static void WrapAsSceneBinary(string sceneBinaryPath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sceneBinaryPath);

        using FileStream output = File.Create(outputPath);
        using BinaryWriter writer = new(output);
        writer.Write(CompiledAssetFormat.SceneMagic);
        writer.Write(CompiledAssetFormat.SceneVersion);
        writer.Write((ulong)bytes.LongLength);
        writer.Write(bytes);
    }

    public static void WrapAsPrefabBinary(string prefabBinaryPath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(prefabBinaryPath);

        using FileStream output = File.Create(outputPath);
        using BinaryWriter writer = new(output);
        writer.Write(CompiledAssetFormat.PrefabMagic);
        writer.Write(CompiledAssetFormat.PrefabVersion);
        writer.Write((ulong)bytes.LongLength);
        writer.Write(bytes);
    }

    private static uint GetMeshSourceKind(string sourcePath)
    {
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        return extension switch
        {
            ".gltf" => 1u,
            ".glb" => 2u,
            _ => 0u
        };
    }

    private static bool TryReadPngDimensions(ReadOnlySpan<byte> bytes, out uint width, out uint height)
    {
        width = 0u;
        height = 0u;

        if (bytes.Length < 24)
        {
            return false;
        }

        ReadOnlySpan<byte> signature = stackalloc byte[]
        {
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A
        };

        if (!bytes.Slice(0, 8).SequenceEqual(signature))
        {
            return false;
        }

        ReadOnlySpan<byte> ihdr = stackalloc byte[] { 0x49, 0x48, 0x44, 0x52 };
        if (!bytes.Slice(12, 4).SequenceEqual(ihdr))
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        return true;
    }
}
