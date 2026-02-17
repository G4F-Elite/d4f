using System.Text;

namespace Engine.Content;

public static class MeshBlobCodec
{
    public const uint Magic = 0x424D4644; // DFMB
    public const uint Version = 1u;

    public static byte[] Write(MeshBlobData mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        mesh = mesh.Validate();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(mesh.VertexCount);
            writer.Write(mesh.VertexStreams.Count);
            writer.Write((uint)mesh.IndexFormat);
            writer.Write(mesh.IndexData.Length);
            writer.Write(mesh.Submeshes.Count);
            writer.Write(mesh.Bounds.MinX);
            writer.Write(mesh.Bounds.MinY);
            writer.Write(mesh.Bounds.MinZ);
            writer.Write(mesh.Bounds.MaxX);
            writer.Write(mesh.Bounds.MaxY);
            writer.Write(mesh.Bounds.MaxZ);
            writer.Write(mesh.Lods.Count);
            writer.Write(mesh.SourceKind);
            writer.Write(mesh.SourcePayload?.Length ?? 0);

            foreach (MeshBlobStream streamEntry in mesh.VertexStreams)
            {
                writer.Write(streamEntry.Semantic);
                writer.Write(streamEntry.ComponentCount);
                writer.Write(streamEntry.ComponentSizeBytes);
                writer.Write(streamEntry.StrideBytes);
                writer.Write(streamEntry.Data.Length);
                writer.Write(streamEntry.Data);
            }

            writer.Write(mesh.IndexData);

            foreach (MeshBlobSubmesh submesh in mesh.Submeshes)
            {
                writer.Write(submesh.IndexStart);
                writer.Write(submesh.IndexCount);
                writer.Write(submesh.MaterialTag);
            }

            foreach (MeshBlobLod lod in mesh.Lods)
            {
                writer.Write(lod.ScreenCoverage);
                writer.Write((uint)lod.IndexFormat);
                writer.Write(lod.IndexData.Length);
                writer.Write(lod.IndexData);
            }

            if (mesh.SourcePayload is { Length: > 0 } sourcePayload)
            {
                writer.Write(sourcePayload);
            }
        }

        return stream.ToArray();
    }

    public static MeshBlobData Read(ReadOnlySpan<byte> blob)
    {
        if (blob.IsEmpty)
        {
            throw new InvalidDataException("Mesh blob payload cannot be empty.");
        }

        using var stream = new MemoryStream(blob.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid mesh blob magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported mesh blob version {version}. Expected {Version}.");
        }

        int vertexCount = reader.ReadInt32();
        int streamCount = reader.ReadInt32();
        MeshBlobIndexFormat indexFormat = checked((MeshBlobIndexFormat)reader.ReadUInt32());
        int indexPayloadLength = reader.ReadInt32();
        int submeshCount = reader.ReadInt32();
        var bounds = new MeshBlobBounds(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());
        int lodCount = reader.ReadInt32();
        uint sourceKind = reader.ReadUInt32();
        int sourcePayloadLength = reader.ReadInt32();

        if (streamCount < 0 || submeshCount < 0 || lodCount < 0)
        {
            throw new InvalidDataException("Mesh blob table counts cannot be negative.");
        }

        if (indexPayloadLength < 0 || sourcePayloadLength < 0)
        {
            throw new InvalidDataException("Mesh blob payload lengths cannot be negative.");
        }

        var streams = new MeshBlobStream[streamCount];
        for (int i = 0; i < streamCount; i++)
        {
            string semantic = reader.ReadString();
            int componentCount = reader.ReadInt32();
            int componentSizeBytes = reader.ReadInt32();
            int strideBytes = reader.ReadInt32();
            int payloadLength = reader.ReadInt32();
            if (payloadLength < 0)
            {
                throw new InvalidDataException("Mesh stream payload length cannot be negative.");
            }

            byte[] payload = reader.ReadBytes(payloadLength);
            if (payload.Length != payloadLength)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading mesh stream payload.");
            }

            streams[i] = new MeshBlobStream(semantic, componentCount, componentSizeBytes, strideBytes, payload);
        }

        byte[] indexData = reader.ReadBytes(indexPayloadLength);
        if (indexData.Length != indexPayloadLength)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading mesh index payload.");
        }

        var submeshes = new MeshBlobSubmesh[submeshCount];
        for (int i = 0; i < submeshCount; i++)
        {
            int indexStart = reader.ReadInt32();
            int indexCount = reader.ReadInt32();
            string materialTag = reader.ReadString();
            submeshes[i] = new MeshBlobSubmesh(indexStart, indexCount, materialTag);
        }

        var lods = new MeshBlobLod[lodCount];
        for (int i = 0; i < lodCount; i++)
        {
            float screenCoverage = reader.ReadSingle();
            MeshBlobIndexFormat lodIndexFormat = checked((MeshBlobIndexFormat)reader.ReadUInt32());
            int lodPayloadLength = reader.ReadInt32();
            if (lodPayloadLength < 0)
            {
                throw new InvalidDataException("Mesh LOD payload length cannot be negative.");
            }

            byte[] lodPayload = reader.ReadBytes(lodPayloadLength);
            if (lodPayload.Length != lodPayloadLength)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading mesh LOD payload.");
            }

            lods[i] = new MeshBlobLod(screenCoverage, lodIndexFormat, lodPayload);
        }

        byte[]? sourcePayload = null;
        if (sourcePayloadLength > 0)
        {
            sourcePayload = reader.ReadBytes(sourcePayloadLength);
            if (sourcePayload.Length != sourcePayloadLength)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading mesh source payload.");
            }
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Mesh blob contains trailing bytes.");
        }

        return new MeshBlobData(
            vertexCount,
            streams,
            indexFormat,
            indexData,
            submeshes,
            bounds,
            lods,
            sourceKind,
            sourcePayload).Validate();
    }
}
