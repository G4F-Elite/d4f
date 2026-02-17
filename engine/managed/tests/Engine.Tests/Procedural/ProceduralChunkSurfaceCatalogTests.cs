using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralChunkSurfaceCatalogTests
{
    [Theory]
    [InlineData("chunk/room/v0")]
    [InlineData("chunk/corridor/v1")]
    [InlineData("chunk/junction/v2")]
    [InlineData("chunk/deadend/v3")]
    [InlineData("chunk/shaft/v0")]
    public void BuildChunkSurface_ShouldProduceValidSurface(string meshTag)
    {
        LevelMeshChunk chunk = new(NodeId: 5, MeshTag: meshTag);

        ProceduralTextureSurface surface = ProceduralChunkSurfaceCatalog.BuildChunkSurface(
            chunk,
            seed: 42u,
            width: 64,
            height: 48);

        Assert.Equal(64, surface.Width);
        Assert.Equal(48, surface.Height);
        Assert.Equal(64 * 48, surface.HeightMap.Length);
        Assert.Equal(64 * 48 * 4, surface.AlbedoRgba8.Length);
        Assert.Equal(64 * 48 * 4, surface.NormalRgba8.Length);
        Assert.Equal(64 * 48 * 4, surface.RoughnessRgba8.Length);
        Assert.Equal(64 * 48 * 4, surface.AmbientOcclusionRgba8.Length);
        Assert.True(surface.MipChain.Count >= 2);
        Assert.Equal(64, surface.MipChain[0].Width);
        Assert.Equal(48, surface.MipChain[0].Height);
    }

    [Fact]
    public void BuildChunkSurface_ShouldBeDeterministic()
    {
        LevelMeshChunk chunk = new(NodeId: 11, MeshTag: "chunk/room/v2");

        ProceduralTextureSurface first = ProceduralChunkSurfaceCatalog.BuildChunkSurface(chunk, seed: 77u, width: 48, height: 48);
        ProceduralTextureSurface second = ProceduralChunkSurfaceCatalog.BuildChunkSurface(chunk, seed: 77u, width: 48, height: 48);

        Assert.Equal(first.HeightMap, second.HeightMap);
        Assert.Equal(first.AlbedoRgba8, second.AlbedoRgba8);
        Assert.Equal(first.NormalRgba8, second.NormalRgba8);
        Assert.Equal(first.RoughnessRgba8, second.RoughnessRgba8);
        Assert.Equal(first.AmbientOcclusionRgba8, second.AmbientOcclusionRgba8);
        Assert.Equal(first.MipChain.Count, second.MipChain.Count);
    }

    [Fact]
    public void BuildChunkSurface_ShouldVaryBySeedAndVariant()
    {
        LevelMeshChunk baseChunk = new(NodeId: 2, MeshTag: "chunk/corridor/v0");

        ProceduralTextureSurface bySeedA = ProceduralChunkSurfaceCatalog.BuildChunkSurface(baseChunk, seed: 1u, width: 32, height: 32);
        ProceduralTextureSurface bySeedB = ProceduralChunkSurfaceCatalog.BuildChunkSurface(baseChunk, seed: 2u, width: 32, height: 32);
        ProceduralTextureSurface byVariant = ProceduralChunkSurfaceCatalog.BuildChunkSurface(
            baseChunk with { MeshTag = "chunk/corridor/v1" },
            seed: 1u,
            width: 32,
            height: 32);

        Assert.NotEqual(bySeedA.AlbedoRgba8, bySeedB.AlbedoRgba8);
        Assert.NotEqual(bySeedA.AlbedoRgba8, byVariant.AlbedoRgba8);
    }

    [Fact]
    public void BuildChunkSurface_ShouldGenerateColoredAlbedo()
    {
        LevelMeshChunk chunk = new(NodeId: 6, MeshTag: "chunk/shaft/v1");

        ProceduralTextureSurface surface = ProceduralChunkSurfaceCatalog.BuildChunkSurface(
            chunk,
            seed: 11u,
            width: 16,
            height: 16);

        bool hasNonGrayscaleSample = false;
        for (int i = 0; i < surface.AlbedoRgba8.Length; i += 4)
        {
            byte r = surface.AlbedoRgba8[i];
            byte g = surface.AlbedoRgba8[i + 1];
            byte b = surface.AlbedoRgba8[i + 2];
            if (r != g || g != b)
            {
                hasNonGrayscaleSample = true;
                break;
            }
        }

        Assert.True(hasNonGrayscaleSample);
    }

    [Fact]
    public void BuildChunkSurface_ShouldValidateInput()
    {
        Assert.Throws<ArgumentNullException>(() => ProceduralChunkSurfaceCatalog.BuildChunkSurface(null!, seed: 1u));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralChunkSurfaceCatalog.BuildChunkSurface(new LevelMeshChunk(1, "chunk/room/v0"), seed: 1u, width: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralChunkSurfaceCatalog.BuildChunkSurface(new LevelMeshChunk(1, "chunk/room/v0"), seed: 1u, height: 0));
        Assert.Throws<InvalidDataException>(() => ProceduralChunkSurfaceCatalog.BuildChunkSurface(new LevelMeshChunk(1, "chunk/unknown/v0"), seed: 1u));
    }
}
