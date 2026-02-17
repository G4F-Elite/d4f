using System.Buffers.Binary;
using Engine.Content;

namespace Engine.AssetPipeline;

internal static class CompiledAssetWriter
{
    public static void WriteTexture(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        bool isPng = TryReadPngDimensions(bytes, out uint pngWidth, out uint pngHeight);
        TextureBlobFormat format = isPng ? TextureBlobFormat.SourcePng : TextureBlobFormat.SourceBinary;
        uint width = isPng ? pngWidth : 1u;
        uint height = isPng ? pngHeight : 1u;
        var blobData = new TextureBlobData(
            format,
            TextureBlobColorSpace.Srgb,
            checked((int)width),
            checked((int)height),
            [new TextureBlobMip(checked((int)width), checked((int)height), 0, bytes)]);
        File.WriteAllBytes(outputPath, TextureBlobCodec.Write(blobData));
    }

    public static void WriteMesh(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        uint sourceKind = GetMeshSourceKind(sourcePath);
        var meshData = new MeshBlobData(
            VertexCount: 0,
            VertexStreams: Array.Empty<MeshBlobStream>(),
            IndexFormat: MeshBlobIndexFormat.UInt32,
            IndexData: Array.Empty<byte>(),
            Submeshes: Array.Empty<MeshBlobSubmesh>(),
            Bounds: new MeshBlobBounds(0f, 0f, 0f, 0f, 0f, 0f),
            Lods: Array.Empty<MeshBlobLod>(),
            SourceKind: sourceKind,
            SourcePayload: bytes);
        File.WriteAllBytes(outputPath, MeshBlobCodec.Write(meshData));
    }

    public static void WriteMaterial(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        var materialData = new MaterialBlobData(
            TemplateId: "raw/material:v1",
            ParameterBlock: bytes,
            TextureReferences: Array.Empty<MaterialTextureReference>());
        File.WriteAllBytes(outputPath, MaterialBlobCodec.Write(materialData));
    }

    public static void WriteSound(string sourcePath, string outputPath)
    {
        byte[] bytes = File.ReadAllBytes(sourcePath);
        (int sampleRate, int channels) = TryReadWaveMeta(bytes, out int sr, out int ch)
            ? (sr, ch)
            : (48000, 1);
        var soundData = new SoundBlobData(
            sampleRate,
            channels,
            SoundBlobEncoding.SourceEncoded,
            bytes);
        File.WriteAllBytes(outputPath, SoundBlobCodec.Write(soundData));
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

    private static bool TryReadWaveMeta(ReadOnlySpan<byte> bytes, out int sampleRate, out int channels)
    {
        sampleRate = 0;
        channels = 0;
        if (bytes.Length < 44)
        {
            return false;
        }

        ReadOnlySpan<byte> riff = "RIFF"u8;
        ReadOnlySpan<byte> wave = "WAVE"u8;
        ReadOnlySpan<byte> fmt = "fmt "u8;
        if (!bytes.Slice(0, 4).SequenceEqual(riff) ||
            !bytes.Slice(8, 4).SequenceEqual(wave))
        {
            return false;
        }

        int offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            ReadOnlySpan<byte> chunkId = bytes.Slice(offset, 4);
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4));
            int dataOffset = offset + 8;
            if (chunkSize < 0 || dataOffset + chunkSize > bytes.Length)
            {
                return false;
            }

            if (chunkId.SequenceEqual(fmt) && chunkSize >= 16)
            {
                channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(dataOffset + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(dataOffset + 4, 4));
                return channels > 0 && sampleRate > 0;
            }

            offset = dataOffset + chunkSize;
            if ((chunkSize & 1) == 1)
            {
                offset++;
            }
        }

        return false;
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
