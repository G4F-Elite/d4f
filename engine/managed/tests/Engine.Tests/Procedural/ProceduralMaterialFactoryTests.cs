using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralMaterialFactoryTests
{
    [Fact]
    public void CreateLitPbrFromSurface_ShouldProduceDeterministicBundle()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 32,
            Height: 16,
            Seed: 555u,
            FbmOctaves: 4,
            Frequency: 6f);
        ProceduralTextureSurface firstSurface = TextureBuilder.GenerateSurfaceMaps(recipe);
        ProceduralTextureSurface secondSurface = TextureBuilder.GenerateSurfaceMaps(recipe);

        ProceduralLitMaterialBundle first = ProceduralMaterialFactory.CreateLitPbrFromSurface(
            firstSurface,
            textureKeyPrefix: "proc/rock",
            roughness: 0.45f,
            metallic: 0.15f);
        ProceduralLitMaterialBundle second = ProceduralMaterialFactory.CreateLitPbrFromSurface(
            secondSurface,
            textureKeyPrefix: "proc/rock",
            roughness: 0.45f,
            metallic: 0.15f);

        Assert.Equal(MaterialTemplateId.DffLitPbr, first.Material.Template);
        Assert.Equal(first.Material.Scalars["roughness"], second.Material.Scalars["roughness"]);
        Assert.Equal(first.Material.Scalars["metallic"], second.Material.Scalars["metallic"]);
        Assert.Equal(first.Material.TextureRefs["albedo"], second.Material.TextureRefs["albedo"]);
        Assert.Equal(first.Material.TextureRefs["normal"], second.Material.TextureRefs["normal"]);
        Assert.Equal(first.Material.TextureRefs["roughness"], second.Material.TextureRefs["roughness"]);
        Assert.Equal(first.Material.TextureRefs["metallic"], second.Material.TextureRefs["metallic"]);
        Assert.Equal(first.Material.TextureRefs["ao"], second.Material.TextureRefs["ao"]);

        Assert.Equal(first.Textures.Count, second.Textures.Count);
        for (int i = 0; i < first.Textures.Count; i++)
        {
            ProceduralTextureExport left = first.Textures[i];
            ProceduralTextureExport right = second.Textures[i];

            Assert.Equal(left.Key, right.Key);
            Assert.Equal(left.Width, right.Width);
            Assert.Equal(left.Height, right.Height);
            Assert.Equal(left.Rgba8, right.Rgba8);
            Assert.Equal(left.MipChain.Count, right.MipChain.Count);
            for (int mip = 0; mip < left.MipChain.Count; mip++)
            {
                Assert.Equal(left.MipChain[mip].Width, right.MipChain[mip].Width);
                Assert.Equal(left.MipChain[mip].Height, right.MipChain[mip].Height);
                Assert.Equal(left.MipChain[mip].Rgba8, right.MipChain[mip].Rgba8);
            }
        }
    }

    [Fact]
    public void CreateLitPbrFromSurface_ShouldExposeRefsForAllSurfaceMaps()
    {
        ProceduralTextureSurface surface = TextureBuilder.GenerateSurfaceMaps(
            new ProceduralTextureRecipe(
                Kind: ProceduralTextureKind.Worley,
                Width: 24,
                Height: 24,
                Seed: 77u,
                FbmOctaves: 3,
                Frequency: 4f));

        ProceduralLitMaterialBundle bundle = ProceduralMaterialFactory.CreateLitPbrFromSurface(
            surface,
            textureKeyPrefix: "proc/cave");

        Assert.Equal(5, bundle.Textures.Count);
        Assert.Equal("proc/cave.albedo", bundle.Material.TextureRefs["albedo"]);
        Assert.Equal("proc/cave.normal", bundle.Material.TextureRefs["normal"]);
        Assert.Equal("proc/cave.roughness", bundle.Material.TextureRefs["roughness"]);
        Assert.Equal("proc/cave.metallic", bundle.Material.TextureRefs["metallic"]);
        Assert.Equal("proc/cave.ao", bundle.Material.TextureRefs["ao"]);

        Assert.Equal(bundle.Material.TextureRefs["albedo"], bundle.Textures[0].Key);
        Assert.Equal(bundle.Material.TextureRefs["normal"], bundle.Textures[1].Key);
        Assert.Equal(bundle.Material.TextureRefs["roughness"], bundle.Textures[2].Key);
        Assert.Equal(bundle.Material.TextureRefs["metallic"], bundle.Textures[3].Key);
        Assert.Equal(bundle.Material.TextureRefs["ao"], bundle.Textures[4].Key);
    }

    [Fact]
    public void CreateLitPbrFromSurface_ShouldValidateInput()
    {
        ProceduralTextureSurface validSurface = TextureBuilder.GenerateSurfaceMaps(
            new ProceduralTextureRecipe(
                Kind: ProceduralTextureKind.Simplex,
                Width: 16,
                Height: 16,
                Seed: 21u));

        Assert.Throws<ArgumentNullException>(() =>
            ProceduralMaterialFactory.CreateLitPbrFromSurface(null!, "proc/stone"));
        Assert.Throws<ArgumentException>(() =>
            ProceduralMaterialFactory.CreateLitPbrFromSurface(validSurface, " "));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProceduralMaterialFactory.CreateLitPbrFromSurface(validSurface, "proc/stone", roughness: -0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProceduralMaterialFactory.CreateLitPbrFromSurface(validSurface, "proc/stone", metallic: 1.1f));

        var invalidSurface = new ProceduralTextureSurface(
            Width: 2,
            Height: 2,
            HeightMap: new float[4],
            AlbedoRgba8: new byte[16],
            NormalRgba8: new byte[16],
            RoughnessRgba8: new byte[16],
            MetallicRgba8: new byte[16],
            AmbientOcclusionRgba8: new byte[16],
            MipChain: Array.Empty<TextureMipLevel>());

        Assert.Throws<InvalidDataException>(() =>
            ProceduralMaterialFactory.CreateLitPbrFromSurface(invalidSurface, "proc/invalid"));
    }

    [Fact]
    public void CreateLitPbrFromSurface_ShouldBuildNormalizedNormalMipChain()
    {
        byte[] albedo =
        [
            128, 128, 128, 255,
            128, 128, 128, 255,
            128, 128, 128, 255,
            128, 128, 128, 255
        ];
        byte[] normals =
        [
            EncodeSigned(1f), EncodeSigned(0f), EncodeSigned(0f), 255,
            EncodeSigned(-1f), EncodeSigned(0f), EncodeSigned(0f), 255,
            EncodeSigned(0f), EncodeSigned(1f), EncodeSigned(0f), 255,
            EncodeSigned(0f), EncodeSigned(-1f), EncodeSigned(0f), 255
        ];
        byte[] roughness =
        [
            100, 100, 100, 255,
            100, 100, 100, 255,
            100, 100, 100, 255,
            100, 100, 100, 255
        ];
        byte[] ao =
        [
            255, 255, 255, 255,
            255, 255, 255, 255,
            255, 255, 255, 255,
            255, 255, 255, 255
        ];
        byte[] metallic =
        [
            20, 20, 20, 255,
            20, 20, 20, 255,
            20, 20, 20, 255,
            20, 20, 20, 255
        ];
        ProceduralTextureSurface surface = new(
            Width: 2,
            Height: 2,
            HeightMap: [0f, 0f, 0f, 0f],
            AlbedoRgba8: albedo,
            NormalRgba8: normals,
            RoughnessRgba8: roughness,
            MetallicRgba8: metallic,
            AmbientOcclusionRgba8: ao,
            MipChain: TextureBuilder.GenerateMipChainRgba8(albedo, 2, 2));

        ProceduralLitMaterialBundle bundle = ProceduralMaterialFactory.CreateLitPbrFromSurface(surface, "proc/test");
        ProceduralTextureExport normalExport = bundle.Textures.Single(static x => x.Key.EndsWith(".normal", StringComparison.Ordinal));
        TextureMipLevel topMip = normalExport.MipChain[^1];

        Assert.Equal((1, 1), (topMip.Width, topMip.Height));
        float nx = DecodeSigned(topMip.Rgba8[0]);
        float ny = DecodeSigned(topMip.Rgba8[1]);
        float nz = DecodeSigned(topMip.Rgba8[2]);
        Assert.InRange(nx, -0.05f, 0.05f);
        Assert.InRange(ny, -0.05f, 0.05f);
        Assert.InRange(nz, 0.95f, 1.0f);
        Assert.Equal(255, topMip.Rgba8[3]);
    }

    private static byte EncodeSigned(float value)
    {
        int encoded = (int)MathF.Round((Math.Clamp(value, -1f, 1f) * 0.5f + 0.5f) * 255f);
        return (byte)Math.Clamp(encoded, 0, 255);
    }

    private static float DecodeSigned(byte value)
    {
        return (value / 255f) * 2f - 1f;
    }
}
