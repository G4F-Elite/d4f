using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralMaterialValidationTests
{
    [Fact]
    public void Validate_ShouldFail_WhenScalarValueIsNotFinite()
    {
        var material = new ProceduralMaterial(
            MaterialTemplateId.DffLitPbr,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["roughness"] = float.NaN
            },
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["baseColor"] = Vector4.One
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["albedo"] = "tex/a",
                ["normal"] = "tex/n"
            });

        Assert.Throws<InvalidDataException>(() => material.Validate());
    }

    [Fact]
    public void Validate_ShouldFail_WhenVectorContainsNonFiniteComponent()
    {
        var material = new ProceduralMaterial(
            MaterialTemplateId.DffUnlit,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal),
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["color"] = new Vector4(1f, float.PositiveInfinity, 0f, 1f)
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Throws<InvalidDataException>(() => material.Validate());
    }

    [Fact]
    public void Validate_ShouldFail_WhenTextureSlotOrRefIsEmpty()
    {
        var invalidSlot = new ProceduralMaterial(
            MaterialTemplateId.DffLitPbr,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["roughness"] = 0.5f
            },
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["baseColor"] = Vector4.One
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [" "] = "tex/albedo"
            });

        var invalidRef = new ProceduralMaterial(
            MaterialTemplateId.DffLitPbr,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["roughness"] = 0.5f
            },
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["baseColor"] = Vector4.One
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["albedo"] = ""
            });

        Assert.Throws<InvalidDataException>(() => invalidSlot.Validate());
        Assert.Throws<InvalidDataException>(() => invalidRef.Validate());
    }
}
