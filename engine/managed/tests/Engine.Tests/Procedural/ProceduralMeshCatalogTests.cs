using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralMeshCatalogTests
{
    [Theory]
    [InlineData("chunk/room/v0")]
    [InlineData("chunk/corridor/v1")]
    [InlineData("chunk/junction/v2")]
    [InlineData("chunk/deadend/v3")]
    [InlineData("chunk/shaft/v0")]
    public void BuildChunkMesh_ShouldCreateValidMeshForKnownTags(string meshTag)
    {
        LevelMeshChunk chunk = new(NodeId: 7, MeshTag: meshTag);

        ProcMeshData mesh = ProceduralMeshCatalog.BuildChunkMesh(chunk, seed: 123u);

        Assert.True(mesh.Vertices.Count > 0);
        Assert.True(mesh.Indices.Count > 0);
        Assert.Equal(0, mesh.Indices.Count % 3);
        Assert.True(mesh.Bounds.Size.X > 0f);
        Assert.True(mesh.Bounds.Size.Y > 0f);
        Assert.True(mesh.Bounds.Size.Z > 0f);
        Assert.True(mesh.Lods.Count >= 1);
        Assert.Equal("chunk/" + meshTag.Split('/')[1], mesh.Submeshes[0].MaterialTag);
    }

    [Fact]
    public void BuildChunkMesh_ShouldBeDeterministicForSameChunkAndSeed()
    {
        LevelMeshChunk chunk = new(NodeId: 10, MeshTag: "chunk/junction/v1");

        ProcMeshData first = ProceduralMeshCatalog.BuildChunkMesh(chunk, seed: 9001u);
        ProcMeshData second = ProceduralMeshCatalog.BuildChunkMesh(chunk, seed: 9001u);

        Assert.Equal(first.Bounds, second.Bounds);
        Assert.Equal(first.Vertices.Count, second.Vertices.Count);
        Assert.Equal(first.Indices.Count, second.Indices.Count);

        for (int i = 0; i < first.Vertices.Count; i++)
        {
            Assert.Equal(first.Vertices[i], second.Vertices[i]);
        }

        for (int i = 0; i < first.Indices.Count; i++)
        {
            Assert.Equal(first.Indices[i], second.Indices[i]);
        }
    }

    [Fact]
    public void BuildChunkMesh_ShouldVaryWithSeedAndVariant()
    {
        LevelMeshChunk baseChunk = new(NodeId: 3, MeshTag: "chunk/room/v0");

        ProcMeshData bySeedA = ProceduralMeshCatalog.BuildChunkMesh(baseChunk, seed: 1u);
        ProcMeshData bySeedB = ProceduralMeshCatalog.BuildChunkMesh(baseChunk, seed: 2u);
        ProcMeshData byVariant = ProceduralMeshCatalog.BuildChunkMesh(baseChunk with { MeshTag = "chunk/room/v1" }, seed: 1u);

        Assert.NotEqual(bySeedA.Bounds, bySeedB.Bounds);
        Assert.NotEqual(bySeedA.Bounds, byVariant.Bounds);
    }

    [Theory]
    [InlineData("chunk/room/v1", true)]
    [InlineData("chunk/corridor/v2", true)]
    [InlineData("chunk/junction/v1", false)]
    [InlineData("chunk/deadend/v0", true)]
    [InlineData("chunk/shaft/v0", false)]
    [InlineData("chunk/shaft/v2", true)]
    public void BuildChunkMesh_ShouldUseAccentSubmeshForDecoratedVariants(string meshTag, bool expectAccentSubmesh)
    {
        LevelMeshChunk chunk = new(NodeId: 4, MeshTag: meshTag);

        ProcMeshData mesh = ProceduralMeshCatalog.BuildChunkMesh(chunk, seed: 77u);

        bool hasAccentSubmesh = mesh.Submeshes.Any(static x => x.MaterialTag.EndsWith("/accent", StringComparison.Ordinal));
        Assert.Equal(expectAccentSubmesh, hasAccentSubmesh);
    }

    [Theory]
    [InlineData("chunk/shaft/v1", true)]
    [InlineData("chunk/corridor/v2", true)]
    [InlineData("chunk/room/v0", false)]
    public void BuildChunkMesh_ShouldChooseExpectedUvProjection(string meshTag, bool expectsCylindricalProjection)
    {
        LevelMeshChunk chunk = new(NodeId: 8, MeshTag: meshTag);

        ProcMeshData mesh = ProceduralMeshCatalog.BuildChunkMesh(chunk, seed: 19u);
        float minU = mesh.Vertices.Min(static x => x.Uv.X);
        float maxU = mesh.Vertices.Max(static x => x.Uv.X);

        if (expectsCylindricalProjection)
        {
            Assert.True(minU >= -0.0001f);
            Assert.True(maxU <= 1.0f);
            return;
        }

        Assert.True(minU < -0.01f);
    }

    [Fact]
    public void BuildChunkMesh_ShouldRejectInvalidTag()
    {
        Assert.Throws<ArgumentNullException>(() => ProceduralMeshCatalog.BuildChunkMesh(null!, seed: 1u));
        Assert.Throws<ArgumentException>(() => ProceduralMeshCatalog.BuildChunkMesh(new LevelMeshChunk(1, " "), seed: 1u));
        Assert.Throws<InvalidDataException>(() => ProceduralMeshCatalog.BuildChunkMesh(new LevelMeshChunk(1, "room/v0"), seed: 1u));
        Assert.Throws<InvalidDataException>(() => ProceduralMeshCatalog.BuildChunkMesh(new LevelMeshChunk(1, "chunk/unknown/v0"), seed: 1u));
        Assert.Throws<InvalidDataException>(() => ProceduralMeshCatalog.BuildChunkMesh(new LevelMeshChunk(1, "chunk/room/v9"), seed: 1u));
    }
}
