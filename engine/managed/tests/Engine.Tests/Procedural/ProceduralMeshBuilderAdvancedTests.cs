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

    [Fact]
    public void GenerateLodChain_ShouldProduceDescendingLods()
    {
        MeshBuilder builder = CreateGridBuilder(cellCount: 5);

        builder.GenerateLodChain(0.75f, 0.50f, 0.25f);
        ProcMeshData mesh = builder.Build();

        Assert.Equal(3, mesh.Lods.Count);
        Assert.Equal(0.75f, mesh.Lods[0].ScreenCoverage);
        Assert.Equal(0.50f, mesh.Lods[1].ScreenCoverage);
        Assert.Equal(0.25f, mesh.Lods[2].ScreenCoverage);
        Assert.True(mesh.Lods[0].Indices.Count > mesh.Lods[1].Indices.Count);
        Assert.True(mesh.Lods[1].Indices.Count > mesh.Lods[2].Indices.Count);
    }

    [Fact]
    public void GenerateLodChain_ShouldRejectEmptyOrNonDescendingCoverage()
    {
        MeshBuilder builder = CreateGridBuilder(cellCount: 3);

        Assert.Throws<ArgumentException>(() => builder.GenerateLodChain());
        Assert.Throws<InvalidDataException>(() => builder.GenerateLodChain(0.5f, 0.5f));
        Assert.Throws<InvalidDataException>(() => builder.GenerateLodChain(0.4f, 0.6f));
    }

    [Fact]
    public void GenerateLodChain_ShouldRejectNonFiniteCoverage()
    {
        MeshBuilder builder = CreateGridBuilder(cellCount: 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.GenerateLodChain(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.GenerateLodChain(float.PositiveInfinity));
    }

    private static MeshBuilder CreateGridBuilder(int cellCount)
    {
        if (cellCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellCount));
        }

        MeshBuilder builder = new();
        for (int y = 0; y <= cellCount; y++)
        {
            for (int x = 0; x <= cellCount; x++)
            {
                builder.AddVertex(
                    new Vector3(x, 0f, y),
                    Vector3.UnitY,
                    new Vector2(x / (float)cellCount, y / (float)cellCount));
            }
        }

        int stride = cellCount + 1;
        for (int y = 0; y < cellCount; y++)
        {
            for (int x = 0; x < cellCount; x++)
            {
                int i0 = y * stride + x;
                int i1 = i0 + 1;
                int i2 = i0 + stride;
                int i3 = i2 + 1;
                builder.AddTriangle(i0, i1, i2);
                builder.AddTriangle(i1, i3, i2);
            }
        }

        return builder;
    }
}
