using System.Numerics;

namespace Engine.Procedural;

public sealed class MeshBuilder
{
    private readonly List<ProcVertex> _vertices = [];
    private readonly List<int> _indices = [];
    private readonly List<ProcSubmesh> _submeshes = [];
    private readonly List<ProcMeshLod> _lods = [];
    private int _currentSubmeshStart;
    private string _currentMaterialTag = "Default";
    private bool _submeshOpen;

    public int VertexCount => _vertices.Count;

    public int IndexCount => _indices.Count;

    public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        return AddVertex(position, normal, uv, Vector4.One);
    }

    public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 color)
    {
        return AddVertex(position, normal, uv, color, new Vector4(1f, 0f, 0f, 1f));
    }

    public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 color, Vector4 tangent)
    {
        ValidateFinite(position, nameof(position));
        ValidateFinite(normal, nameof(normal));
        ValidateFinite(uv, nameof(uv));
        ValidateFinite(color, nameof(color));
        ValidateFinite(tangent, nameof(tangent));

        Vector3 safeNormal = normal.LengthSquared() <= 0f ? Vector3.UnitY : Vector3.Normalize(normal);
        Vector3 tangentDirection = new(tangent.X, tangent.Y, tangent.Z);
        if (tangentDirection.LengthSquared() <= 1e-8f)
        {
            tangentDirection = ComputeFallbackTangent(safeNormal);
        }
        else
        {
            tangentDirection = Vector3.Normalize(tangentDirection);
        }

        float tangentSign = tangent.W < 0f ? -1f : 1f;
        int index = _vertices.Count;
        _vertices.Add(new ProcVertex(position, safeNormal, uv, color, new Vector4(tangentDirection, tangentSign)));
        return index;
    }

    public void AddTriangle(int a, int b, int c)
    {
        ValidateVertexIndex(a);
        ValidateVertexIndex(b);
        ValidateVertexIndex(c);

        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    public void BeginSubmesh(string materialTag)
    {
        if (_submeshOpen)
        {
            EndSubmesh();
        }

        if (string.IsNullOrWhiteSpace(materialTag))
        {
            throw new ArgumentException("Material tag cannot be empty.", nameof(materialTag));
        }

        _currentSubmeshStart = _indices.Count;
        _currentMaterialTag = materialTag.Trim();
        _submeshOpen = true;
    }

    public void EndSubmesh()
    {
        if (!_submeshOpen)
        {
            return;
        }

        int indexCount = _indices.Count - _currentSubmeshStart;
        if (indexCount > 0)
        {
            _submeshes.Add(new ProcSubmesh(_currentSubmeshStart, indexCount, _currentMaterialTag));
        }

        _submeshOpen = false;
    }

    public void GenerateUv(UvProjection projection, float scale = 1f)
    {
        if (scale <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "UV scale must be greater than zero.");
        }

        if (_vertices.Count == 0)
        {
            throw new InvalidDataException("Cannot generate UV coordinates without vertices.");
        }

        for (int i = 0; i < _vertices.Count; i++)
        {
            ProcVertex vertex = _vertices[i];
            Vector2 uv = projection switch
            {
                UvProjection.Planar => new Vector2(vertex.Position.X, vertex.Position.Z),
                UvProjection.Box => BoxProjection(vertex.Position, vertex.Normal),
                UvProjection.Cylindrical => CylindricalProjection(vertex.Position),
                _ => throw new InvalidDataException($"Unsupported UV projection value: {projection}.")
            };

            _vertices[i] = vertex with { Uv = uv / scale };
        }
    }

    public void GenerateLod(float screenCoverage = 0.5f)
    {
        if (screenCoverage <= 0f || screenCoverage >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(screenCoverage), "Screen coverage must be within (0,1)." );
        }

        if (_indices.Count < 6)
        {
            throw new InvalidDataException("Need at least two triangles to generate a simplified LOD.");
        }

        int triangleCount = _indices.Count / 3;
        int keepTriangleCount = Math.Clamp(
            checked((int)MathF.Ceiling(triangleCount * screenCoverage)),
            1,
            triangleCount - 1);
        var candidates = new List<TriangleCandidate>(triangleCount);
        for (int triangle = 0; triangle < triangleCount; triangle++)
        {
            int start = triangle * 3;
            int ia = _indices[start];
            int ib = _indices[start + 1];
            int ic = _indices[start + 2];
            Vector3 a = _vertices[ia].Position;
            Vector3 b = _vertices[ib].Position;
            Vector3 c = _vertices[ic].Position;
            float area = Vector3.Cross(b - a, c - a).LengthSquared();
            candidates.Add(new TriangleCandidate(start, area));
        }

        int[] selectedStarts = candidates
            .OrderByDescending(static x => x.Area)
            .ThenBy(static x => x.IndexStart)
            .Take(keepTriangleCount)
            .Select(static x => x.IndexStart)
            .OrderBy(static x => x)
            .ToArray();
        var lodIndices = new List<int>(selectedStarts.Length * 3);
        foreach (int start in selectedStarts)
        {
            lodIndices.Add(_indices[start]);
            lodIndices.Add(_indices[start + 1]);
            lodIndices.Add(_indices[start + 2]);
        }

        _lods.Add(new ProcMeshLod(screenCoverage, lodIndices.ToArray()));
    }

    public void GenerateLodChain(params float[] screenCoverages)
    {
        ArgumentNullException.ThrowIfNull(screenCoverages);
        if (screenCoverages.Length == 0)
        {
            throw new ArgumentException("LOD coverage chain cannot be empty.", nameof(screenCoverages));
        }

        float previousCoverage = 1f;
        foreach (float coverage in screenCoverages)
        {
            if (!float.IsFinite(coverage))
            {
                throw new ArgumentOutOfRangeException(nameof(screenCoverages), "LOD coverage values must be finite.");
            }

            if (coverage >= previousCoverage)
            {
                throw new InvalidDataException("LOD coverage chain must be strictly descending.");
            }

            GenerateLod(coverage);
            previousCoverage = coverage;
        }
    }

    public ProcMeshData Build()
    {
        EndSubmesh();
        RecalculateTangents();

        ProcBounds bounds = ProcBounds.FromPoints(_vertices);
        var submeshes = _submeshes.Count == 0
            ? new[] { new ProcSubmesh(0, _indices.Count, "Default") }
            : _submeshes.ToArray();

        ProcMeshData mesh = new(
            Vertices: _vertices.ToArray(),
            Indices: _indices.ToArray(),
            Submeshes: submeshes,
            Bounds: bounds,
            Lods: _lods.ToArray());
        return mesh.Validate();
    }

    private static Vector2 BoxProjection(Vector3 position, Vector3 normal)
    {
        Vector3 absNormal = new(MathF.Abs(normal.X), MathF.Abs(normal.Y), MathF.Abs(normal.Z));
        if (absNormal.X >= absNormal.Y && absNormal.X >= absNormal.Z)
        {
            return new Vector2(position.Y, position.Z);
        }

        if (absNormal.Y >= absNormal.X && absNormal.Y >= absNormal.Z)
        {
            return new Vector2(position.X, position.Z);
        }

        return new Vector2(position.X, position.Y);
    }

    private static Vector2 CylindricalProjection(Vector3 position)
    {
        float angle = MathF.Atan2(position.Z, position.X);
        float u = (angle / (MathF.PI * 2f)) + 0.5f;
        return new Vector2(u, position.Y);
    }

    private void RecalculateTangents()
    {
        if (_vertices.Count == 0 || _indices.Count == 0)
        {
            return;
        }

        var tangentSums = new Vector3[_vertices.Count];
        var bitangentSums = new Vector3[_vertices.Count];

        for (int i = 0; i < _indices.Count; i += 3)
        {
            int ia = _indices[i];
            int ib = _indices[i + 1];
            int ic = _indices[i + 2];

            ProcVertex a = _vertices[ia];
            ProcVertex b = _vertices[ib];
            ProcVertex c = _vertices[ic];

            Vector3 edge1 = b.Position - a.Position;
            Vector3 edge2 = c.Position - a.Position;
            Vector2 deltaUv1 = b.Uv - a.Uv;
            Vector2 deltaUv2 = c.Uv - a.Uv;

            float determinant = deltaUv1.X * deltaUv2.Y - deltaUv1.Y * deltaUv2.X;
            if (MathF.Abs(determinant) <= 1e-8f)
            {
                continue;
            }

            float inverse = 1f / determinant;
            Vector3 tangent = (edge1 * deltaUv2.Y - edge2 * deltaUv1.Y) * inverse;
            Vector3 bitangent = (edge2 * deltaUv1.X - edge1 * deltaUv2.X) * inverse;

            if (!IsFinite(tangent) || !IsFinite(bitangent))
            {
                continue;
            }

            tangentSums[ia] += tangent;
            tangentSums[ib] += tangent;
            tangentSums[ic] += tangent;
            bitangentSums[ia] += bitangent;
            bitangentSums[ib] += bitangent;
            bitangentSums[ic] += bitangent;
        }

        for (int i = 0; i < _vertices.Count; i++)
        {
            ProcVertex vertex = _vertices[i];
            Vector3 normal = vertex.Normal.LengthSquared() <= 1e-8f ? Vector3.UnitY : Vector3.Normalize(vertex.Normal);
            Vector3 tangent = tangentSums[i];

            if (tangent.LengthSquared() <= 1e-8f || !IsFinite(tangent))
            {
                tangent = ComputeFallbackTangent(normal);
            }
            else
            {
                tangent = Vector3.Normalize(tangent - normal * Vector3.Dot(normal, tangent));
                if (tangent.LengthSquared() <= 1e-8f || !IsFinite(tangent))
                {
                    tangent = ComputeFallbackTangent(normal);
                }
            }

            Vector3 bitangent = bitangentSums[i];
            float handedness = 1f;
            if (bitangent.LengthSquared() > 1e-8f && IsFinite(bitangent))
            {
                handedness = Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) < 0f ? -1f : 1f;
            }

            _vertices[i] = vertex with
            {
                Normal = normal,
                Tangent = new Vector4(tangent, handedness)
            };
        }
    }

    private static Vector3 ComputeFallbackTangent(Vector3 normal)
    {
        Vector3 axis = MathF.Abs(normal.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 tangent = Vector3.Cross(axis, normal);
        if (tangent.LengthSquared() <= 1e-8f)
        {
            tangent = Vector3.UnitX;
        }

        return Vector3.Normalize(tangent);
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static void ValidateFinite(Vector2 value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
        {
            throw new ArgumentOutOfRangeException(paramName, "Vector2 components must be finite.");
        }
    }

    private static void ValidateFinite(Vector3 value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(paramName, "Vector3 components must be finite.");
        }
    }

    private static void ValidateFinite(Vector4 value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z) || !float.IsFinite(value.W))
        {
            throw new ArgumentOutOfRangeException(paramName, "Vector4 components must be finite.");
        }
    }

    private void ValidateVertexIndex(int index)
    {
        if (index < 0 || index >= _vertices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Vertex index {index} is outside range [0, {_vertices.Count - 1}].");
        }
    }

    private readonly record struct TriangleCandidate(int IndexStart, float Area);
}
