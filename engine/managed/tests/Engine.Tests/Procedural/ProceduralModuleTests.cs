using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralModuleTests
{
    [Fact]
    public void MeshBuilder_ShouldBuildMeshWithBoundsAndSubmesh()
    {
        MeshBuilder builder = new();
        builder.BeginSubmesh("Default");
        int a = builder.AddVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int b = builder.AddVertex(new Vector3(1f, 0f, 0f), Vector3.UnitY, Vector2.UnitX);
        int c = builder.AddVertex(new Vector3(0f, 0f, 1f), Vector3.UnitY, Vector2.UnitY);
        builder.AddTriangle(a, b, c);
        builder.EndSubmesh();

        ProcMeshData mesh = builder.Build();

        Assert.Equal(3, mesh.Vertices.Count);
        Assert.Equal(3, mesh.Indices.Count);
        Assert.Single(mesh.Submeshes);
        Assert.Equal(Vector3.Zero, mesh.Bounds.Min);
        Assert.Equal(new Vector3(1f, 0f, 1f), mesh.Bounds.Max);
    }

    [Fact]
    public void MeshBuilder_ShouldGenerateUvAndLod()
    {
        MeshBuilder builder = new();
        int a = builder.AddVertex(new Vector3(0f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int b = builder.AddVertex(new Vector3(1f, 0f, 0f), Vector3.UnitY, Vector2.Zero);
        int c = builder.AddVertex(new Vector3(0f, 0f, 1f), Vector3.UnitY, Vector2.Zero);
        int d = builder.AddVertex(new Vector3(1f, 0f, 1f), Vector3.UnitY, Vector2.Zero);
        builder.AddTriangle(a, b, c);
        builder.AddTriangle(b, d, c);

        builder.GenerateUv(UvProjection.Cylindrical, scale: 1f);
        builder.GenerateLod(screenCoverage: 0.5f);

        ProcMeshData mesh = builder.Build();

        Assert.Single(mesh.Lods);
        Assert.True(mesh.Lods[0].Indices.Count < mesh.Indices.Count);
        Assert.Contains(mesh.Vertices, static v => v.Uv != Vector2.Zero);
    }

    [Fact]
    public void TextureBuilder_ShouldGenerateDeterministicPerlinAndNormalMap()
    {
        ProceduralTextureRecipe recipe = new(
            Kind: ProceduralTextureKind.Perlin,
            Width: 32,
            Height: 32,
            Seed: 123u,
            FbmOctaves: 3,
            Frequency: 5f);

        float[] first = TextureBuilder.GenerateHeight(recipe);
        float[] second = TextureBuilder.GenerateHeight(recipe);
        byte[] normal = TextureBuilder.HeightToNormalMap(first, 32, 32, strength: 2f);

        Assert.Equal(first, second);
        Assert.Equal(32 * 32 * 4, normal.Length);
    }

    [Fact]
    public void TextureBuilder_ShouldGenerateDifferentData_WhenSeedChanges()
    {
        ProceduralTextureRecipe firstRecipe = new(ProceduralTextureKind.Worley, 16, 16, Seed: 1u);
        ProceduralTextureRecipe secondRecipe = new(ProceduralTextureKind.Worley, 16, 16, Seed: 2u);

        float[] first = TextureBuilder.GenerateHeight(firstRecipe);
        float[] second = TextureBuilder.GenerateHeight(secondRecipe);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void MaterialTemplates_ShouldCreateExpectedTemplateIds()
    {
        ProceduralMaterial lit = MaterialTemplates.CreateLitPbr("tex/albedo", "tex/normal", roughness: 0.5f, metallic: 0.1f);
        ProceduralMaterial unlit = MaterialTemplates.CreateUnlit(new Vector4(1f, 0f, 0f, 1f));
        ProceduralMaterial decal = MaterialTemplates.CreateDecal("tex/mask", opacity: 0.8f);
        ProceduralMaterial ui = MaterialTemplates.CreateUi(new Vector4(0f, 1f, 0f, 1f));

        Assert.Equal(MaterialTemplateId.DffLitPbr, lit.Template);
        Assert.Equal(MaterialTemplateId.DffUnlit, unlit.Template);
        Assert.Equal(MaterialTemplateId.DffDecal, decal.Template);
        Assert.Equal(MaterialTemplateId.DffUi, ui.Template);
    }

    [Fact]
    public void LevelGenerator_ShouldBeDeterministicAndProduceSpawnPoints()
    {
        LevelGenOptions options = new(Seed: 42u, TargetNodes: 20, Density: 0.7f, Danger: 0.4f);

        LevelGenResult first = LevelGenerator.Generate(options);
        LevelGenResult second = LevelGenerator.Generate(options);

        Assert.Equal(first.Graph.Nodes.Count, second.Graph.Nodes.Count);
        Assert.Equal(first.MeshChunks, second.MeshChunks);
        Assert.Equal(first.SpawnPoints, second.SpawnPoints);
        Assert.True(first.SpawnPoints.Count > 0);
    }

    [Fact]
    public void LevelGenerator_ShouldVaryTopology_WhenSeedChanges()
    {
        LevelGenResult first = LevelGenerator.Generate(new LevelGenOptions(Seed: 1u, TargetNodes: 16, Density: 0.6f, Danger: 0.5f));
        LevelGenResult second = LevelGenerator.Generate(new LevelGenOptions(Seed: 2u, TargetNodes: 16, Density: 0.6f, Danger: 0.5f));

        Assert.NotEqual(
            first.Graph.Nodes.Select(static n => (n.Type, n.Position)).ToArray(),
            second.Graph.Nodes.Select(static n => (n.Type, n.Position)).ToArray());
    }

    [Fact]
    public void ProceduralAnimation_ShouldEvaluateCurvesAndLookAt()
    {
        Assert.Equal(0f, ProceduralAnimation.EvaluateTween(0f, TweenCurve.Linear));
        Assert.Equal(1f, ProceduralAnimation.EvaluateTween(1f, TweenCurve.EaseInOut));
        Assert.True(ProceduralAnimation.EvaluateTween(0.5f, TweenCurve.EaseIn) < 0.5f);
        Assert.True(ProceduralAnimation.EvaluateTween(0.5f, TweenCurve.EaseOut) > 0.5f);

        Quaternion look = ProceduralAnimation.LookAt(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY);
        Vector3 aim = ProceduralAnimation.AimDirection(Vector3.Zero, new Vector3(1f, 0f, 1f));

        Assert.True(MathF.Abs(look.Length() - 1f) < 0.001f);
        Assert.True(MathF.Abs(aim.Length() - 1f) < 0.001f);
    }
}
