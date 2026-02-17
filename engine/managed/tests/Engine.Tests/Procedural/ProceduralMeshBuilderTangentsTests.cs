using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralMeshBuilderTangentsTests
{
    [Fact]
    public void Build_ComputesNormalizedTangentsForPlanarMesh()
    {
        MeshBuilder builder = new();
        int a = builder.AddVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, new Vector2(0f, 0f), new Vector4(0.2f, 0.3f, 0.4f, 1f));
        int b = builder.AddVertex(new Vector3(1f, 0f, 0f), Vector3.UnitY, new Vector2(1f, 0f), new Vector4(0.5f, 0.6f, 0.7f, 1f));
        int c = builder.AddVertex(new Vector3(0f, 0f, 1f), Vector3.UnitY, new Vector2(0f, 1f), new Vector4(0.1f, 0.2f, 0.3f, 1f));
        int d = builder.AddVertex(new Vector3(1f, 0f, 1f), Vector3.UnitY, new Vector2(1f, 1f), new Vector4(0.9f, 0.8f, 0.7f, 1f));
        builder.AddTriangle(a, b, c);
        builder.AddTriangle(b, d, c);

        ProcMeshData mesh = builder.Build();

        Assert.Equal(4, mesh.Vertices.Count);
        foreach (ProcVertex vertex in mesh.Vertices)
        {
            Vector3 tangent = new(vertex.Tangent.X, vertex.Tangent.Y, vertex.Tangent.Z);
            Assert.InRange(MathF.Abs(tangent.Length() - 1f), 0f, 0.001f);
            Assert.InRange(MathF.Abs(Vector3.Dot(vertex.Normal, tangent)), 0f, 0.001f);
            Assert.True(vertex.Tangent.W is 1f or -1f);
        }

        Assert.Equal(new Vector4(0.2f, 0.3f, 0.4f, 1f), mesh.Vertices[0].Color);
        Assert.True(MathF.Abs(mesh.Vertices[0].Tangent.X) > 0.99f);
    }

    [Fact]
    public void Build_UsesFallbackTangents_WhenUvIsDegenerate()
    {
        MeshBuilder builder = new();
        int a = builder.AddVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int b = builder.AddVertex(new Vector3(1f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int c = builder.AddVertex(new Vector3(0f, 0f, 1f), Vector3.UnitY, Vector2.Zero);
        builder.AddTriangle(a, b, c);

        ProcMeshData mesh = builder.Build();

        foreach (ProcVertex vertex in mesh.Vertices)
        {
            Vector3 tangent = new(vertex.Tangent.X, vertex.Tangent.Y, vertex.Tangent.Z);
            Assert.True(float.IsFinite(tangent.X));
            Assert.True(float.IsFinite(tangent.Y));
            Assert.True(float.IsFinite(tangent.Z));
            Assert.InRange(MathF.Abs(tangent.Length() - 1f), 0f, 0.001f);
            Assert.InRange(MathF.Abs(Vector3.Dot(vertex.Normal, tangent)), 0f, 0.001f);
        }
    }

    [Fact]
    public void AddVertex_RejectsNonFiniteInputs()
    {
        MeshBuilder builder = new();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddVertex(new Vector3(float.NaN, 0f, 0f), Vector3.UnitY, Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddVertex(Vector3.Zero, new Vector3(0f, float.PositiveInfinity, 0f), Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddVertex(Vector3.Zero, Vector3.UnitY, new Vector2(float.NegativeInfinity, 0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddVertex(Vector3.Zero, Vector3.UnitY, Vector2.Zero, new Vector4(1f, float.NaN, 1f, 1f)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddVertex(Vector3.Zero, Vector3.UnitY, Vector2.Zero, Vector4.One, new Vector4(1f, 0f, 0f, float.NaN)));
    }
}
