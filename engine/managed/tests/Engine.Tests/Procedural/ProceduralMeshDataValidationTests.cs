using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralMeshDataValidationTests
{
    [Fact]
    public void Validate_ShouldRejectLodIndicesOutsideVertexRange()
    {
        ProcMeshData mesh = BuildMeshWithLods(
            new ProcMeshLod(0.75f, [0, 1, 99]));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => mesh.Validate());
        Assert.Contains("LOD index", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldRejectNonDescendingLodCoverage()
    {
        ProcMeshData mesh = BuildMeshWithLods(
            new ProcMeshLod(0.60f, [0, 1, 2]),
            new ProcMeshLod(0.60f, [0, 1, 2]));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => mesh.Validate());
        Assert.Contains("descending screen coverage", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldRejectLodsThatIncreaseIndexCount()
    {
        ProcMeshData mesh = BuildMeshWithLods(
            new ProcMeshLod(0.80f, [0, 1, 2]),
            new ProcMeshLod(0.40f, [0, 1, 2, 1, 3, 2]));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => mesh.Validate());
        Assert.Contains("must not increase", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShouldAcceptValidDescendingLodChain()
    {
        ProcMeshData mesh = BuildMeshWithLods(
            new ProcMeshLod(0.80f, [0, 1, 2, 1, 3, 2]),
            new ProcMeshLod(0.45f, [0, 1, 2]),
            new ProcMeshLod(0.20f, [0, 1, 2]));

        ProcMeshData validated = mesh.Validate();
        Assert.Equal(mesh, validated);
    }

    private static ProcMeshData BuildMeshWithLods(params ProcMeshLod[] lods)
    {
        IReadOnlyList<ProcVertex> vertices =
        [
            new ProcVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero, Vector4.One),
            new ProcVertex(new Vector3(1f, 0f, 0f), Vector3.UnitY, Vector2.UnitX, Vector4.One),
            new ProcVertex(new Vector3(0f, 0f, 1f), Vector3.UnitY, Vector2.UnitY, Vector4.One),
            new ProcVertex(new Vector3(1f, 0f, 1f), Vector3.UnitY, Vector2.One, Vector4.One)
        ];
        IReadOnlyList<int> indices = [0, 1, 2, 1, 3, 2];
        IReadOnlyList<ProcSubmesh> submeshes = [new ProcSubmesh(0, indices.Count, "default")];
        ProcBounds bounds = ProcBounds.FromPoints(vertices);
        return new ProcMeshData(vertices, indices, submeshes, bounds, lods);
    }
}
