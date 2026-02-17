using Engine.Content;

namespace Engine.Tests.Content;

public sealed class MeshBlobCodecTests
{
    [Fact]
    public void WriteRead_ShouldRoundTripGeometryMeshBlob()
    {
        MeshBlobData source = CreateTriangleMesh();

        byte[] bytes = MeshBlobCodec.Write(source);
        MeshBlobData decoded = MeshBlobCodec.Read(bytes);

        Assert.Equal(MeshBlobCodec.Magic, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(MeshBlobCodec.Version, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(3, decoded.VertexCount);
        Assert.Equal(2, decoded.VertexStreams.Count);
        Assert.Equal(MeshBlobIndexFormat.UInt32, decoded.IndexFormat);
        Assert.Equal(12, decoded.IndexData.Length);
        Assert.Single(decoded.Submeshes);
        Assert.Single(decoded.Lods);
        Assert.Null(decoded.SourcePayload);
    }

    [Fact]
    public void WriteRead_ShouldRoundTripSourceOnlyMeshBlob()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        var source = new MeshBlobData(
            VertexCount: 0,
            VertexStreams: Array.Empty<MeshBlobStream>(),
            IndexFormat: MeshBlobIndexFormat.UInt32,
            IndexData: Array.Empty<byte>(),
            Submeshes: Array.Empty<MeshBlobSubmesh>(),
            Bounds: new MeshBlobBounds(0, 0, 0, 0, 0, 0),
            Lods: Array.Empty<MeshBlobLod>(),
            SourceKind: 2u,
            SourcePayload: payload);

        byte[] bytes = MeshBlobCodec.Write(source);
        MeshBlobData decoded = MeshBlobCodec.Read(bytes);

        Assert.Equal(0, decoded.VertexCount);
        Assert.Equal(2u, decoded.SourceKind);
        Assert.Equal(payload, decoded.SourcePayload);
    }

    [Fact]
    public void Write_ShouldFail_WhenSubmeshRangeExceedsIndexCount()
    {
        var invalid = new MeshBlobData(
            VertexCount: 3,
            VertexStreams:
            [
                new MeshBlobStream("POSITION", 3, sizeof(float), sizeof(float) * 3, new byte[36])
            ],
            IndexFormat: MeshBlobIndexFormat.UInt32,
            IndexData: new byte[12],
            Submeshes:
            [
                new MeshBlobSubmesh(0, 6, "main")
            ],
            Bounds: new MeshBlobBounds(0, 0, 0, 1, 1, 1),
            Lods: Array.Empty<MeshBlobLod>());

        Assert.Throws<InvalidDataException>(() => MeshBlobCodec.Write(invalid));
    }

    private static MeshBlobData CreateTriangleMesh()
    {
        return new MeshBlobData(
            VertexCount: 3,
            VertexStreams:
            [
                new MeshBlobStream("POSITION", 3, sizeof(float), sizeof(float) * 3, new byte[36]),
                new MeshBlobStream("NORMAL", 3, sizeof(float), sizeof(float) * 3, new byte[36])
            ],
            IndexFormat: MeshBlobIndexFormat.UInt32,
            IndexData: new byte[12],
            Submeshes:
            [
                new MeshBlobSubmesh(0, 3, "main")
            ],
            Bounds: new MeshBlobBounds(-1, -1, -1, 1, 1, 1),
            Lods:
            [
                new MeshBlobLod(0.5f, MeshBlobIndexFormat.UInt32, new byte[12])
            ]);
    }
}
