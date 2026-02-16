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
        Vector3 safeNormal = normal.LengthSquared() <= 0f ? Vector3.UnitY : Vector3.Normalize(normal);
        int index = _vertices.Count;
        _vertices.Add(new ProcVertex(position, safeNormal, uv, Vector4.One));
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
        int step = Math.Max(2, triangleCount / 2);
        var lodIndices = new List<int>(_indices.Count / 2);
        for (int triangle = 0; triangle < triangleCount; triangle += step)
        {
            int start = triangle * 3;
            lodIndices.Add(_indices[start]);
            lodIndices.Add(_indices[start + 1]);
            lodIndices.Add(_indices[start + 2]);
        }

        if (lodIndices.Count == 0)
        {
            lodIndices.AddRange(_indices.Take(3));
        }

        _lods.Add(new ProcMeshLod(screenCoverage, lodIndices.ToArray()));
    }

    public ProcMeshData Build()
    {
        EndSubmesh();

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

    private void ValidateVertexIndex(int index)
    {
        if (index < 0 || index >= _vertices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Vertex index {index} is outside range [0, {_vertices.Count - 1}].");
        }
    }
}
