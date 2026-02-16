using System.Numerics;

namespace Engine.Procedural;

public enum MaterialTemplateId
{
    DffLitPbr = 0,
    DffUnlit = 1,
    DffDecal = 2,
    DffUi = 3
}

public sealed record ProceduralMaterial(
    MaterialTemplateId Template,
    IReadOnlyDictionary<string, float> Scalars,
    IReadOnlyDictionary<string, Vector4> Vectors,
    IReadOnlyDictionary<string, string> TextureRefs)
{
    public ProceduralMaterial Validate()
    {
        if (!Enum.IsDefined(Template))
        {
            throw new InvalidDataException($"Unsupported material template: {Template}.");
        }

        ArgumentNullException.ThrowIfNull(Scalars);
        ArgumentNullException.ThrowIfNull(Vectors);
        ArgumentNullException.ThrowIfNull(TextureRefs);
        return this;
    }
}

public static class MaterialTemplates
{
    public static ProceduralMaterial CreateLitPbr(string albedoTexture, string normalTexture, float roughness, float metallic)
    {
        ValidateTextureKey(albedoTexture, nameof(albedoTexture));
        ValidateTextureKey(normalTexture, nameof(normalTexture));
        if (roughness < 0f || roughness > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(roughness), "Roughness must be within [0,1].");
        }

        if (metallic < 0f || metallic > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(metallic), "Metallic must be within [0,1].");
        }

        return new ProceduralMaterial(
            Template: MaterialTemplateId.DffLitPbr,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["roughness"] = roughness,
                ["metallic"] = metallic
            },
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["baseColor"] = Vector4.One
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["albedo"] = albedoTexture,
                ["normal"] = normalTexture
            }).Validate();
    }

    public static ProceduralMaterial CreateUnlit(Vector4 color)
    {
        return new ProceduralMaterial(
            Template: MaterialTemplateId.DffUnlit,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal),
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["color"] = color
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)).Validate();
    }

    public static ProceduralMaterial CreateDecal(string maskTexture, float opacity)
    {
        ValidateTextureKey(maskTexture, nameof(maskTexture));
        if (opacity < 0f || opacity > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be within [0,1].");
        }

        return new ProceduralMaterial(
            Template: MaterialTemplateId.DffDecal,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["opacity"] = opacity
            },
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal),
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mask"] = maskTexture
            }).Validate();
    }

    public static ProceduralMaterial CreateUi(Vector4 tint)
    {
        return new ProceduralMaterial(
            Template: MaterialTemplateId.DffUi,
            Scalars: new Dictionary<string, float>(StringComparer.Ordinal),
            Vectors: new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                ["tint"] = tint
            },
            TextureRefs: new Dictionary<string, string>(StringComparer.Ordinal)).Validate();
    }

    private static void ValidateTextureKey(string textureKey, string paramName)
    {
        if (string.IsNullOrWhiteSpace(textureKey))
        {
            throw new ArgumentException("Texture key cannot be empty.", paramName);
        }
    }
}
