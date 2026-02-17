using System.Text;
using Engine.Content;
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
        uint[] indices = ToUnsignedIndices(mesh.Indices);
        MeshBlobSubmesh[] submeshes = mesh.Submeshes
            .Select(static x => new MeshBlobSubmesh(x.IndexStart, x.IndexCount, x.MaterialTag))
            .ToArray();
        MeshBlobLod[] lods = mesh.Lods
            .Select(static x => new MeshBlobLod(
                x.ScreenCoverage,
                MeshBlobIndexFormat.UInt32,
                EncodeUInt32Array(ToUnsignedIndices(x.Indices))))
            .ToArray();

        var meshData = new MeshBlobData(
            VertexCount: mesh.Vertices.Count,
            VertexStreams: BuildVertexStreams(mesh.Vertices),
            IndexFormat: MeshBlobIndexFormat.UInt32,
            IndexData: EncodeUInt32Array(indices),
            Submeshes: submeshes,
            Bounds: new MeshBlobBounds(
                mesh.Bounds.Min.X,
                mesh.Bounds.Min.Y,
                mesh.Bounds.Min.Z,
                mesh.Bounds.Max.X,
                mesh.Bounds.Max.Y,
                mesh.Bounds.Max.Z),
            Lods: lods);
        return MeshBlobCodec.Write(meshData);
    }

    private static byte[] EncodeTextureBlob(ProceduralTextureExport texture)
    {
        texture = texture.Validate();
        TextureBlobColorSpace colorSpace = IsLinearTexture(texture.Key)
            ? TextureBlobColorSpace.Linear
            : TextureBlobColorSpace.Srgb;

        TextureBlobMip[] mips = texture.MipChain
            .Select(static x =>
            {
                TextureMipLevel mip = x.Validate();
                int rowPitch = checked(mip.Width * 4);
                return new TextureBlobMip(mip.Width, mip.Height, rowPitch, mip.Rgba8);
            })
            .ToArray();

        var blobData = new TextureBlobData(
            TextureBlobFormat.Rgba8Unorm,
            colorSpace,
            texture.Width,
            texture.Height,
            mips);
        return TextureBlobCodec.Write(blobData);
    }

    private static byte[] EncodeMaterialBlob(
        ProceduralMaterial material,
        IReadOnlyDictionary<string, TextureHandle> textureHandles)
    {
        material = material.Validate();
        ArgumentNullException.ThrowIfNull(textureHandles);
        MaterialTextureReference[] textureReferences = material.TextureRefs
            .OrderBy(static x => x.Key, StringComparer.Ordinal)
            .Select(x =>
            {
                if (!textureHandles.TryGetValue(x.Value, out TextureHandle textureHandle))
                {
                    throw new InvalidDataException(
                        $"Material texture reference '{x.Key}' points to missing texture key '{x.Value}'.");
                }

                return new MaterialTextureReference(x.Key, x.Value, textureHandle.Value);
            })
            .ToArray();

        var blobData = new MaterialBlobData(
            TemplateId: material.Template.ToString(),
            ParameterBlock: EncodeMaterialParameterBlock(material),
            TextureReferences: textureReferences);
        return MaterialBlobCodec.Write(blobData);
    }

    private static MeshBlobStream[] BuildVertexStreams(IReadOnlyList<ProcVertex> vertices)
    {
        var positions = new float[checked(vertices.Count * 3)];
        var normals = new float[checked(vertices.Count * 3)];
        var uvs = new float[checked(vertices.Count * 2)];
        var colors = new float[checked(vertices.Count * 4)];
        var tangents = new float[checked(vertices.Count * 4)];

        for (int i = 0; i < vertices.Count; i++)
        {
            ProcVertex vertex = vertices[i];

            int positionOffset = checked(i * 3);
            positions[positionOffset] = vertex.Position.X;
            positions[positionOffset + 1] = vertex.Position.Y;
            positions[positionOffset + 2] = vertex.Position.Z;

            normals[positionOffset] = vertex.Normal.X;
            normals[positionOffset + 1] = vertex.Normal.Y;
            normals[positionOffset + 2] = vertex.Normal.Z;

            int uvOffset = checked(i * 2);
            uvs[uvOffset] = vertex.Uv.X;
            uvs[uvOffset + 1] = vertex.Uv.Y;

            int vector4Offset = checked(i * 4);
            colors[vector4Offset] = vertex.Color.X;
            colors[vector4Offset + 1] = vertex.Color.Y;
            colors[vector4Offset + 2] = vertex.Color.Z;
            colors[vector4Offset + 3] = vertex.Color.W;

            tangents[vector4Offset] = vertex.Tangent.X;
            tangents[vector4Offset + 1] = vertex.Tangent.Y;
            tangents[vector4Offset + 2] = vertex.Tangent.Z;
            tangents[vector4Offset + 3] = vertex.Tangent.W;
        }

        return
        [
            new MeshBlobStream("POSITION", 3, sizeof(float), sizeof(float) * 3, EncodeFloatArray(positions)),
            new MeshBlobStream("NORMAL", 3, sizeof(float), sizeof(float) * 3, EncodeFloatArray(normals)),
            new MeshBlobStream("TEXCOORD0", 2, sizeof(float), sizeof(float) * 2, EncodeFloatArray(uvs)),
            new MeshBlobStream("COLOR0", 4, sizeof(float), sizeof(float) * 4, EncodeFloatArray(colors)),
            new MeshBlobStream("TANGENT", 4, sizeof(float), sizeof(float) * 4, EncodeFloatArray(tangents))
        ];
    }

    private static uint[] ToUnsignedIndices(IReadOnlyList<int> indices)
    {
        var converted = new uint[indices.Count];
        for (int i = 0; i < indices.Count; i++)
        {
            int index = indices[i];
            if (index < 0)
            {
                throw new InvalidDataException($"Mesh index '{index}' cannot be negative.");
            }

            converted[i] = checked((uint)index);
        }

        return converted;
    }

    private static byte[] EncodeFloatArray(float[] values)
    {
        var payload = new byte[checked(values.Length * sizeof(float))];
        Buffer.BlockCopy(values, 0, payload, 0, payload.Length);
        return payload;
    }

    private static byte[] EncodeUInt32Array(uint[] values)
    {
        var payload = new byte[checked(values.Length * sizeof(uint))];
        Buffer.BlockCopy(values, 0, payload, 0, payload.Length);
        return payload;
    }

    private static bool IsLinearTexture(string textureKey)
    {
        if (textureKey.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
            textureKey.Contains("roughness", StringComparison.OrdinalIgnoreCase) ||
            textureKey.Contains("ao", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static byte[] EncodeMaterialParameterBlock(ProceduralMaterial material)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
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
                writer.Write(vector.Value.X);
                writer.Write(vector.Value.Y);
                writer.Write(vector.Value.Z);
                writer.Write(vector.Value.W);
            }
        }

        return stream.ToArray();
    }
}
