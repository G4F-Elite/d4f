using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class MaterialTemplatesLitPbrTextureRefsTests
{
    [Fact]
    public void CreateLitPbr_ShouldIncludeOnlyRequiredTextureRefs_WhenOptionalRefsAreNotProvided()
    {
        ProceduralMaterial material = MaterialTemplates.CreateLitPbr(
            "tex/albedo",
            "tex/normal",
            roughness: 0.4f,
            metallic: 0.2f);

        Assert.Equal(MaterialTemplateId.DffLitPbr, material.Template);
        Assert.Equal(2, material.TextureRefs.Count);
        Assert.Equal("tex/albedo", material.TextureRefs["albedo"]);
        Assert.Equal("tex/normal", material.TextureRefs["normal"]);
        Assert.False(material.TextureRefs.ContainsKey("roughness"));
        Assert.False(material.TextureRefs.ContainsKey("metallic"));
        Assert.False(material.TextureRefs.ContainsKey("ao"));
    }

    [Fact]
    public void CreateLitPbr_ShouldIncludeOptionalTextureRefs_WhenProvided()
    {
        ProceduralMaterial material = MaterialTemplates.CreateLitPbr(
            "tex/albedo",
            "tex/normal",
            roughness: 0.7f,
            metallic: 0.5f,
            roughnessTexture: "tex/roughness",
            metallicTexture: "tex/metallic",
            ambientOcclusionTexture: "tex/ao");

        Assert.Equal("tex/albedo", material.TextureRefs["albedo"]);
        Assert.Equal("tex/normal", material.TextureRefs["normal"]);
        Assert.Equal("tex/roughness", material.TextureRefs["roughness"]);
        Assert.Equal("tex/metallic", material.TextureRefs["metallic"]);
        Assert.Equal("tex/ao", material.TextureRefs["ao"]);
    }

    [Fact]
    public void CreateLitPbr_ShouldFail_WhenOptionalTextureRefIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => MaterialTemplates.CreateLitPbr(
            "tex/albedo",
            "tex/normal",
            roughness: 0.5f,
            metallic: 0.1f,
            roughnessTexture: " ",
            metallicTexture: "tex/metallic",
            ambientOcclusionTexture: "tex/ao"));
    }
}
