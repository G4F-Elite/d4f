using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralMeshBuilderAdvancedTests
{
    [Fact]
    public void GenerateLod_ShouldKeepLargerTriangles_WhenSimplifying()
    {
        MeshBuilder builder = new();

        int a = builder.AddVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int b = builder.AddVertex(new Vector3(10f, 0f, 0f), Vector3.UnitY, Vector2.UnitX);
        int c = builder.AddVertex(new Vector3(0f, 0f, 10f), Vector3.UnitY, Vector2.UnitY);
        int d = builder.AddVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int e = builder.AddVertex(new Vector3(1f, 0f, 0f), Vector3.UnitY, Vector2.UnitX);
        int f = builder.AddVertex(new Vector3(0f, 0f, 1f), Vector3.UnitY, Vector2.UnitY);

        builder.AddTriangle(a, b, c); // Large area.
        builder.AddTriangle(d, e, f); // Small area.

        builder.GenerateLod(screenCoverage: 0.5f);
        ProcMeshData mesh = builder.Build();

        ProcMeshLod lod = Assert.Single(mesh.Lods);
        Assert.Equal(3, lod.Indices.Count);
        Assert.Equal([a, b, c], lod.Indices.ToArray());
    }
}
