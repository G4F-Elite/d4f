using System.Numerics;

namespace Engine.Procedural;

public enum UvProjection
{
    Planar = 0,
    Box = 1,
    Cylindrical = 2
}

public readonly record struct ProcVertex(
    Vector3 Position,
    Vector3 Normal,
    Vector2 Uv,
    Vector4 Color,
    Vector4 Tangent)
{
    public ProcVertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 color)
        : this(position, normal, uv, color, new Vector4(1f, 0f, 0f, 1f))
    {
    }
}

public readonly record struct ProcBounds(Vector3 Min, Vector3 Max)
{
    public Vector3 Size => Max - Min;

    public static ProcBounds FromPoints(IReadOnlyList<ProcVertex> vertices)
    {
        if (vertices.Count == 0)
        {
            throw new InvalidDataException("Cannot compute bounds for an empty vertex list.");
        }

        Vector3 min = vertices[0].Position;
        Vector3 max = vertices[0].Position;
        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 p = vertices[i].Position;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new ProcBounds(min, max);
    }
}

public sealed record ProcSubmesh(int IndexStart, int IndexCount, string MaterialTag)
{
    public ProcSubmesh Validate()
    {
        if (IndexStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IndexStart), "Index start cannot be negative.");
        }

        if (IndexCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IndexCount), "Index count cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(MaterialTag))
        {
            throw new ArgumentException("Material tag cannot be empty.", nameof(MaterialTag));
        }

        return this;
    }
}

public sealed record ProcMeshLod(float ScreenCoverage, IReadOnlyList<int> Indices)
{
    public ProcMeshLod Validate()
    {
        if (ScreenCoverage <= 0f || ScreenCoverage > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(ScreenCoverage), "Screen coverage must be within (0, 1].");
        }

        ArgumentNullException.ThrowIfNull(Indices);
        if (Indices.Count == 0)
        {
            throw new InvalidDataException("LOD indices cannot be empty.");
        }

        if (Indices.Count % 3 != 0)
        {
            throw new InvalidDataException("LOD index count must be divisible by 3.");
        }

        return this;
    }
}

public sealed record ProcMeshData(
    IReadOnlyList<ProcVertex> Vertices,
    IReadOnlyList<int> Indices,
    IReadOnlyList<ProcSubmesh> Submeshes,
    ProcBounds Bounds,
    IReadOnlyList<ProcMeshLod> Lods)
{
    public ProcMeshData Validate()
    {
        ArgumentNullException.ThrowIfNull(Vertices);
        ArgumentNullException.ThrowIfNull(Indices);
        ArgumentNullException.ThrowIfNull(Submeshes);
        ArgumentNullException.ThrowIfNull(Lods);

        if (Vertices.Count == 0)
        {
            throw new InvalidDataException("Mesh must contain at least one vertex.");
        }

        if (Indices.Count == 0 || Indices.Count % 3 != 0)
        {
            throw new InvalidDataException("Mesh indices must be non-empty and divisible by 3.");
        }

        foreach (ProcVertex vertex in Vertices)
        {
            ValidateVertex(vertex);
        }

        foreach (int index in Indices)
        {
            if (index < 0 || index >= Vertices.Count)
            {
                throw new InvalidDataException($"Mesh index {index} is outside vertex range [0, {Vertices.Count - 1}].");
            }
        }

        foreach (ProcSubmesh submesh in Submeshes)
        {
            _ = submesh.Validate();
            if (submesh.IndexStart + submesh.IndexCount > Indices.Count)
            {
                throw new InvalidDataException(
                    $"Submesh '{submesh.MaterialTag}' range [{submesh.IndexStart}, {submesh.IndexStart + submesh.IndexCount}) exceeds index count {Indices.Count}.");
            }
        }

        foreach (ProcMeshLod lod in Lods)
        {
            _ = lod.Validate();
        }

        return this;
    }

    private static void ValidateVertex(ProcVertex vertex)
    {
        if (!IsFinite(vertex.Position.X) || !IsFinite(vertex.Position.Y) || !IsFinite(vertex.Position.Z))
        {
            throw new InvalidDataException("Mesh vertex position contains a non-finite component.");
        }

        if (!IsFinite(vertex.Normal.X) || !IsFinite(vertex.Normal.Y) || !IsFinite(vertex.Normal.Z))
        {
            throw new InvalidDataException("Mesh vertex normal contains a non-finite component.");
        }

        if (!IsFinite(vertex.Uv.X) || !IsFinite(vertex.Uv.Y))
        {
            throw new InvalidDataException("Mesh vertex UV contains a non-finite component.");
        }

        if (!IsFinite(vertex.Color.X) || !IsFinite(vertex.Color.Y) || !IsFinite(vertex.Color.Z) || !IsFinite(vertex.Color.W))
        {
            throw new InvalidDataException("Mesh vertex color contains a non-finite component.");
        }

        if (!IsFinite(vertex.Tangent.X) || !IsFinite(vertex.Tangent.Y) || !IsFinite(vertex.Tangent.Z) || !IsFinite(vertex.Tangent.W))
        {
            throw new InvalidDataException("Mesh vertex tangent contains a non-finite component.");
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
