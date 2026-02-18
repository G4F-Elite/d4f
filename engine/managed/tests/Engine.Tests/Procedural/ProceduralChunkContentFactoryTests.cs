using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralChunkContentFactoryTests
{
    [Fact]
    public void Build_ShouldProduceMeshAndMaterialBundle()
    {
        LevelMeshChunk chunk = new(NodeId: 12, MeshTag: "chunk/room/v1");

        ProceduralChunkContent content = ProceduralChunkContentFactory.Build(
            chunk,
            seed: 100u,
            surfaceWidth: 48,
            surfaceHeight: 48);

        Assert.Equal(12, content.NodeId);
        Assert.Equal(LevelNodeType.Room, content.NodeType);
        Assert.Equal(1, content.Variant);
        Assert.True(content.Mesh.Vertices.Count > 0);
        Assert.True(content.Mesh.Indices.Count > 0);
        Assert.Equal(MaterialTemplateId.DffLitPbr, content.MaterialBundle.Material.Template);
        Assert.Equal(5, content.MaterialBundle.Textures.Count);
        Assert.StartsWith("proc/chunk/room/v1/n12", content.MaterialBundle.Textures[0].Key, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShouldBeDeterministic()
    {
        LevelMeshChunk chunk = new(NodeId: 8, MeshTag: "chunk/junction/v3");

        ProceduralChunkContent first = ProceduralChunkContentFactory.Build(chunk, seed: 555u, surfaceWidth: 32, surfaceHeight: 32);
        ProceduralChunkContent second = ProceduralChunkContentFactory.Build(chunk, seed: 555u, surfaceWidth: 32, surfaceHeight: 32);

        Assert.Equal(first.Mesh.Bounds, second.Mesh.Bounds);
        Assert.Equal(first.Mesh.Vertices, second.Mesh.Vertices);
        Assert.Equal(first.Mesh.Indices, second.Mesh.Indices);
        Assert.Equal(first.MaterialBundle.Material.Template, second.MaterialBundle.Material.Template);
        Assert.Equal(first.MaterialBundle.Material.Scalars, second.MaterialBundle.Material.Scalars);
        Assert.Equal(first.MaterialBundle.Material.Vectors, second.MaterialBundle.Material.Vectors);
        Assert.Equal(first.MaterialBundle.Material.TextureRefs, second.MaterialBundle.Material.TextureRefs);
        Assert.Equal(first.MaterialBundle.Textures.Count, second.MaterialBundle.Textures.Count);
        for (int i = 0; i < first.MaterialBundle.Textures.Count; i++)
        {
            Assert.Equal(first.MaterialBundle.Textures[i].Rgba8, second.MaterialBundle.Textures[i].Rgba8);
        }
    }

    [Fact]
    public void Build_ShouldVaryBySeed()
    {
        LevelMeshChunk chunk = new(NodeId: 9, MeshTag: "chunk/deadend/v0");

        ProceduralChunkContent first = ProceduralChunkContentFactory.Build(chunk, seed: 1u, surfaceWidth: 32, surfaceHeight: 32);
        ProceduralChunkContent second = ProceduralChunkContentFactory.Build(chunk, seed: 2u, surfaceWidth: 32, surfaceHeight: 32);

        Assert.NotEqual(first.Mesh.Bounds, second.Mesh.Bounds);
        Assert.NotEqual(first.MaterialBundle.Textures[0].Rgba8, second.MaterialBundle.Textures[0].Rgba8);
    }

    [Fact]
    public void BuildAll_ShouldProduceChunkForEachMeshChunk()
    {
        LevelGenResult level = LevelGenerator.Generate(new LevelGenOptions(
            Seed: 77u,
            TargetNodes: 20,
            Density: 0.6f,
            Danger: 0.5f,
            Complexity: 0.7f));

        IReadOnlyList<ProceduralChunkContent> chunks = ProceduralChunkContentFactory.BuildAll(
            level,
            seed: 77u,
            surfaceWidth: 24,
            surfaceHeight: 24);

        Assert.Equal(level.MeshChunks.Count, chunks.Count);
        Assert.All(chunks, static chunk =>
        {
            Assert.True(chunk.Mesh.Vertices.Count > 0);
            Assert.Equal(5, chunk.MaterialBundle.Textures.Count);
        });
    }

    [Fact]
    public void Build_ShouldValidateInput()
    {
        Assert.Throws<ArgumentNullException>(() => ProceduralChunkContentFactory.Build(null!, seed: 1u));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralChunkContentFactory.Build(new LevelMeshChunk(1, "chunk/room/v0"), seed: 1u, surfaceWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralChunkContentFactory.Build(new LevelMeshChunk(1, "chunk/room/v0"), seed: 1u, surfaceHeight: 0));
        Assert.Throws<InvalidDataException>(() => ProceduralChunkContentFactory.Build(new LevelMeshChunk(1, "chunk/unknown/v0"), seed: 1u));
    }
}
