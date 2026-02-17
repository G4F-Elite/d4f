using System.Text;
using Engine.Core.Handles;
using Engine.Rendering;

namespace Engine.Procedural;

public sealed record ProceduralChunkUploadResult(
    RenderMeshInstance Instance,
    MeshHandle Mesh,
    MaterialHandle Material,
    TextureHandle AlbedoTexture,
    IReadOnlyDictionary<string, TextureHandle> TexturesByKey)
{
    public ProceduralChunkUploadResult Validate()
    {
        if (!Mesh.IsValid)
        {
            throw new InvalidDataException("Mesh handle is invalid.");
        }

        if (!Material.IsValid)
        {
            throw new InvalidDataException("Material handle is invalid.");
        }

        if (!AlbedoTexture.IsValid)
        {
            throw new InvalidDataException("Albedo texture handle is invalid.");
        }

        ArgumentNullException.ThrowIfNull(TexturesByKey);
        if (TexturesByKey.Count == 0)
        {
            throw new InvalidDataException("Texture handle map cannot be empty.");
        }

        foreach (KeyValuePair<string, TextureHandle> pair in TexturesByKey)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new InvalidDataException("Texture handle map key cannot be empty.");
            }

            if (!pair.Value.IsValid)
            {
                throw new InvalidDataException($"Texture handle for key '{pair.Key}' is invalid.");
            }
        }

        if (!TexturesByKey.Values.Contains(AlbedoTexture))
        {
            throw new InvalidDataException("Albedo texture handle is not present in texture map.");
        }

        return this;
    }

    public void Destroy(IRenderingFacade rendering)
    {
        ArgumentNullException.ThrowIfNull(rendering);

        var uniqueHandles = new HashSet<ulong>
        {
            Mesh.Value,
            Material.Value
        };

        foreach (TextureHandle texture in TexturesByKey.Values)
        {
            uniqueHandles.Add(texture.Value);
        }

        foreach (ulong handle in uniqueHandles.OrderBy(static x => x))
        {
            rendering.DestroyResource(handle);
        }
    }
}

public readonly record struct ProceduralChunkUploadOptions(
    bool UseCpuMeshPath = false,
    bool UseCpuTexturePath = false);

public static class ProceduralChunkRenderUploader
{
    public static ProceduralChunkUploadResult Upload(
        IRenderingFacade rendering,
        ProceduralChunkContent content,
        in ProceduralChunkUploadOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(rendering);
        ArgumentNullException.ThrowIfNull(content);
        content = content.Validate();

        MeshHandle meshHandle = options.UseCpuMeshPath
            ? CreateMeshFromCpu(rendering, content.Mesh)
            : rendering.CreateMeshFromBlob(EncodeMeshBlob(content.Mesh));

        var textureHandles = new Dictionary<string, TextureHandle>(
            content.MaterialBundle.Textures.Count,
            StringComparer.Ordinal);
        foreach (ProceduralTextureExport textureExport in content.MaterialBundle.Textures)
        {
            TextureHandle textureHandle = options.UseCpuTexturePath
                ? CreateTextureFromCpu(rendering, textureExport)
                : rendering.CreateTextureFromBlob(EncodeTextureBlob(textureExport));
            textureHandles.Add(textureExport.Key, textureHandle);
        }

        if (!content.MaterialBundle.Material.TextureRefs.TryGetValue("albedo", out string? albedoKey) ||
            !textureHandles.TryGetValue(albedoKey, out TextureHandle albedoTexture))
        {
            throw new InvalidDataException("Material bundle does not provide an uploaded albedo texture handle.");
        }

        MaterialHandle materialHandle = rendering.CreateMaterialFromBlob(
            EncodeMaterialBlob(content.MaterialBundle.Material, textureHandles));

        var instance = new RenderMeshInstance(meshHandle, materialHandle, albedoTexture);
        return new ProceduralChunkUploadResult(
            instance,
            meshHandle,
            materialHandle,
            albedoTexture,
            textureHandles).Validate();
    }

    private static MeshHandle CreateMeshFromCpu(IRenderingFacade rendering, ProcMeshData mesh)
    {
        mesh = mesh.Validate();
        var positions = new float[checked(mesh.Vertices.Count * 3)];
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            int offset = checked(i * 3);
            positions[offset] = mesh.Vertices[i].Position.X;
            positions[offset + 1] = mesh.Vertices[i].Position.Y;
            positions[offset + 2] = mesh.Vertices[i].Position.Z;
        }

        var indices = new uint[mesh.Indices.Count];
        for (int i = 0; i < mesh.Indices.Count; i++)
        {
            int index = mesh.Indices[i];
            if (index < 0)
            {
                throw new InvalidDataException($"Mesh index '{index}' cannot be negative for CPU upload.");
            }

            indices[i] = checked((uint)index);
        }

        return rendering.CreateMeshFromCpu(positions, indices);
    }

    private static TextureHandle CreateTextureFromCpu(
        IRenderingFacade rendering,
        ProceduralTextureExport textureExport)
    {
        textureExport = textureExport.Validate();
        return rendering.CreateTextureFromCpu(
            checked((uint)textureExport.Width),
            checked((uint)textureExport.Height),
            textureExport.Rgba8,
            strideBytes: checked((uint)(textureExport.Width * 4)));
    }

    private static byte[] EncodeMeshBlob(ProcMeshData mesh)
    {
        mesh = mesh.Validate();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteMagic(writer, "DFF_MESH_V1");
        writer.Write(mesh.Vertices.Count);
        writer.Write(mesh.Indices.Count);
        writer.Write(mesh.Submeshes.Count);
        writer.Write(mesh.Lods.Count);

        foreach (ProcVertex vertex in mesh.Vertices)
        {
            WriteVector3(writer, vertex.Position);
            WriteVector3(writer, vertex.Normal);
            WriteVector2(writer, vertex.Uv);
            WriteVector4(writer, vertex.Color);
            WriteVector4(writer, vertex.Tangent);
        }

        foreach (int index in mesh.Indices)
        {
            writer.Write(index);
        }

        WriteVector3(writer, mesh.Bounds.Min);
        WriteVector3(writer, mesh.Bounds.Max);

        foreach (ProcSubmesh submesh in mesh.Submeshes)
        {
            writer.Write(submesh.IndexStart);
            writer.Write(submesh.IndexCount);
            writer.Write(submesh.MaterialTag);
        }

        foreach (ProcMeshLod lod in mesh.Lods)
        {
            writer.Write(lod.ScreenCoverage);
            writer.Write(lod.Indices.Count);
            foreach (int index in lod.Indices)
            {
                writer.Write(index);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] EncodeTextureBlob(ProceduralTextureExport texture)
    {
        texture = texture.Validate();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteMagic(writer, "DFF_TEXTURE_V1");
        writer.Write(texture.Key);
        writer.Write(texture.Width);
        writer.Write(texture.Height);
        writer.Write(texture.MipChain.Count);

        foreach (TextureMipLevel mip in texture.MipChain)
        {
            TextureMipLevel validatedMip = mip.Validate();
            writer.Write(validatedMip.Width);
            writer.Write(validatedMip.Height);
            writer.Write(validatedMip.Rgba8.Length);
            writer.Write(validatedMip.Rgba8);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] EncodeMaterialBlob(
        ProceduralMaterial material,
        IReadOnlyDictionary<string, TextureHandle> textureHandles)
    {
        material = material.Validate();
        ArgumentNullException.ThrowIfNull(textureHandles);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteMagic(writer, "DFF_MATERIAL_V1");
        writer.Write((int)material.Template);

        KeyValuePair<string, float>[] scalars = material.Scalars
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .ToArray();
        writer.Write(scalars.Length);
        foreach (KeyValuePair<string, float> scalar in scalars)
        {
            writer.Write(scalar.Key);
            writer.Write(scalar.Value);
        }

        KeyValuePair<string, System.Numerics.Vector4>[] vectors = material.Vectors
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .ToArray();
        writer.Write(vectors.Length);
        foreach (KeyValuePair<string, System.Numerics.Vector4> vector in vectors)
        {
            writer.Write(vector.Key);
            WriteVector4(writer, vector.Value);
        }

        KeyValuePair<string, string>[] refs = material.TextureRefs
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .ToArray();
        writer.Write(refs.Length);
        foreach (KeyValuePair<string, string> textureRef in refs)
        {
            if (!textureHandles.TryGetValue(textureRef.Value, out TextureHandle textureHandle))
            {
                throw new InvalidDataException(
                    $"Material texture reference '{textureRef.Key}' points to missing texture key '{textureRef.Value}'.");
            }

            writer.Write(textureRef.Key);
            writer.Write(textureRef.Value);
            writer.Write(textureHandle.Value);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteMagic(BinaryWriter writer, string magic)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(magic);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteVector2(BinaryWriter writer, System.Numerics.Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    private static void WriteVector3(BinaryWriter writer, System.Numerics.Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteVector4(BinaryWriter writer, System.Numerics.Vector4 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }
}
