namespace Engine.Content;

public enum MeshBlobIndexFormat : uint
{
    UInt16 = 1u,
    UInt32 = 2u
}

public sealed record MeshBlobStream(
    string Semantic,
    int ComponentCount,
    int ComponentSizeBytes,
    int StrideBytes,
    byte[] Data)
{
    public MeshBlobStream Validate(int vertexCount)
    {
        if (string.IsNullOrWhiteSpace(Semantic))
        {
            throw new ArgumentException("Mesh stream semantic cannot be empty.", nameof(Semantic));
        }

        if (ComponentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ComponentCount), "Mesh stream component count must be greater than zero.");
        }

        if (ComponentSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ComponentSizeBytes), "Mesh stream component size must be greater than zero.");
        }

        if (StrideBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StrideBytes), "Mesh stream stride must be greater than zero.");
        }

        int minStride = checked(ComponentCount * ComponentSizeBytes);
        if (StrideBytes < minStride)
        {
            throw new InvalidDataException(
                $"Mesh stream stride {StrideBytes} cannot be less than component payload size {minStride}.");
        }

        ArgumentNullException.ThrowIfNull(Data);
        if (Data.Length == 0)
        {
            throw new InvalidDataException("Mesh stream payload cannot be empty.");
        }

        if (vertexCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vertexCount), "Vertex count must be greater than zero for geometry streams.");
        }

        int expectedLength = checked(vertexCount * StrideBytes);
        if (Data.Length != expectedLength)
        {
            throw new InvalidDataException(
                $"Mesh stream payload size {Data.Length} does not match expected {expectedLength} for vertex count {vertexCount}.");
        }

        return this;
    }
}

public sealed record MeshBlobSubmesh(
    int IndexStart,
    int IndexCount,
    string MaterialTag)
{
    public MeshBlobSubmesh Validate(int totalIndexCount)
    {
        if (IndexStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IndexStart), "Submesh index start cannot be negative.");
        }

        if (IndexCount <= 0 || IndexCount % 3 != 0)
        {
            throw new InvalidDataException("Submesh index count must be greater than zero and divisible by 3.");
        }

        if (string.IsNullOrWhiteSpace(MaterialTag))
        {
            throw new ArgumentException("Submesh material tag cannot be empty.", nameof(MaterialTag));
        }

        if (IndexStart + IndexCount > totalIndexCount)
        {
            throw new InvalidDataException(
                $"Submesh range [{IndexStart}, {IndexStart + IndexCount}) exceeds index buffer size {totalIndexCount}.");
        }

        return this;
    }
}

public sealed record MeshBlobLod(
    float ScreenCoverage,
    MeshBlobIndexFormat IndexFormat,
    byte[] IndexData)
{
    public MeshBlobLod Validate()
    {
        if (ScreenCoverage <= 0f || ScreenCoverage > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(ScreenCoverage), "LOD screen coverage must be within (0,1].");
        }

        if (!Enum.IsDefined(IndexFormat))
        {
            throw new InvalidDataException($"Unsupported LOD index format '{IndexFormat}'.");
        }

        ArgumentNullException.ThrowIfNull(IndexData);
        if (IndexData.Length == 0)
        {
            throw new InvalidDataException("LOD index payload cannot be empty.");
        }

        int indexSizeBytes = GetIndexSizeBytes(IndexFormat);
        if ((IndexData.Length % indexSizeBytes) != 0)
        {
            throw new InvalidDataException(
                $"LOD index payload size {IndexData.Length} is not aligned to index format {IndexFormat}.");
        }

        int indexCount = IndexData.Length / indexSizeBytes;
        if (indexCount % 3 != 0)
        {
            throw new InvalidDataException("LOD index count must be divisible by 3.");
        }

        return this;
    }

    internal static int GetIndexSizeBytes(MeshBlobIndexFormat format)
    {
        return format switch
        {
            MeshBlobIndexFormat.UInt16 => 2,
            MeshBlobIndexFormat.UInt32 => 4,
            _ => throw new InvalidDataException($"Unsupported mesh index format '{format}'.")
        };
    }
}

public readonly record struct MeshBlobBounds(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ)
{
    public MeshBlobBounds Validate()
    {
        if (!IsFinite(MinX) || !IsFinite(MinY) || !IsFinite(MinZ) ||
            !IsFinite(MaxX) || !IsFinite(MaxY) || !IsFinite(MaxZ))
        {
            throw new InvalidDataException("Mesh bounds contain non-finite values.");
        }

        if (MaxX < MinX || MaxY < MinY || MaxZ < MinZ)
        {
            throw new InvalidDataException("Mesh bounds maximum components cannot be less than minimum components.");
        }

        return this;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed record MeshBlobData(
    int VertexCount,
    IReadOnlyList<MeshBlobStream> VertexStreams,
    MeshBlobIndexFormat IndexFormat,
    byte[] IndexData,
    IReadOnlyList<MeshBlobSubmesh> Submeshes,
    MeshBlobBounds Bounds,
    IReadOnlyList<MeshBlobLod> Lods,
    uint SourceKind = 0u,
    byte[]? SourcePayload = null)
{
    public MeshBlobData Validate()
    {
        ArgumentNullException.ThrowIfNull(VertexStreams);
        ArgumentNullException.ThrowIfNull(IndexData);
        ArgumentNullException.ThrowIfNull(Submeshes);
        ArgumentNullException.ThrowIfNull(Lods);

        bool hasSourcePayload = SourcePayload is { Length: > 0 };
        bool hasGeometry = VertexCount > 0 ||
                           VertexStreams.Count > 0 ||
                           IndexData.Length > 0 ||
                           Submeshes.Count > 0;

        if (!hasGeometry && !hasSourcePayload)
        {
            throw new InvalidDataException("Mesh blob must contain geometry data or source payload.");
        }

        _ = Bounds.Validate();

        if (!hasGeometry)
        {
            if (VertexCount != 0 ||
                VertexStreams.Count != 0 ||
                IndexData.Length != 0 ||
                Submeshes.Count != 0 ||
                Lods.Count != 0)
            {
                throw new InvalidDataException("Source-only mesh blobs cannot include geometry tables.");
            }

            return this;
        }

        if (VertexCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(VertexCount), "Mesh vertex count must be greater than zero.");
        }

        if (!Enum.IsDefined(IndexFormat))
        {
            throw new InvalidDataException($"Unsupported mesh index format '{IndexFormat}'.");
        }

        if (VertexStreams.Count == 0)
        {
            throw new InvalidDataException("Mesh vertex stream table cannot be empty.");
        }

        int indexElementSize = MeshBlobLod.GetIndexSizeBytes(IndexFormat);
        if (IndexData.Length == 0 || (IndexData.Length % indexElementSize) != 0)
        {
            throw new InvalidDataException(
                $"Mesh index payload size {IndexData.Length} is invalid for index format {IndexFormat}.");
        }

        int totalIndexCount = IndexData.Length / indexElementSize;
        if (totalIndexCount % 3 != 0)
        {
            throw new InvalidDataException("Mesh index count must be divisible by 3.");
        }

        foreach (MeshBlobStream stream in VertexStreams)
        {
            _ = stream.Validate(VertexCount);
        }

        if (Submeshes.Count == 0)
        {
            throw new InvalidDataException("Mesh submesh table cannot be empty when geometry is present.");
        }

        foreach (MeshBlobSubmesh submesh in Submeshes)
        {
            _ = submesh.Validate(totalIndexCount);
        }

        foreach (MeshBlobLod lod in Lods)
        {
            _ = lod.Validate();
        }

        return this;
    }
}
