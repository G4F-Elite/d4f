using System.Numerics;

namespace Engine.Procedural;

public sealed record ProceduralTextureExport(
    string Key,
    int Width,
    int Height,
    byte[] Rgba8,
    IReadOnlyList<TextureMipLevel> MipChain)
{
    public ProceduralTextureExport Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            throw new ArgumentException("Texture export key cannot be empty.", nameof(Key));
        }

        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Texture export dimensions must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(Rgba8);
        ArgumentNullException.ThrowIfNull(MipChain);

        int expectedPayloadSize = checked(Width * Height * 4);
        if (Rgba8.Length != expectedPayloadSize)
        {
            throw new InvalidDataException(
                $"Texture export payload size {Rgba8.Length} does not match dimensions {Width}x{Height} ({expectedPayloadSize} bytes).");
        }

        if (MipChain.Count == 0)
        {
            throw new InvalidDataException("Texture export mip chain cannot be empty.");
        }

        TextureMipLevel baseMip = MipChain[0].Validate();
        if (baseMip.Width != Width || baseMip.Height != Height)
        {
            throw new InvalidDataException("Texture export base mip dimensions do not match texture dimensions.");
        }

        if (!baseMip.Rgba8.SequenceEqual(Rgba8))
        {
            throw new InvalidDataException("Texture export base mip payload does not match top-level texture payload.");
        }

        for (int i = 1; i < MipChain.Count; i++)
        {
            _ = MipChain[i].Validate();
        }

        return this;
    }
}

public sealed record ProceduralLitMaterialBundle(
    ProceduralMaterial Material,
    IReadOnlyList<ProceduralTextureExport> Textures)
{
    public ProceduralLitMaterialBundle Validate()
    {
        ArgumentNullException.ThrowIfNull(Material);
        ArgumentNullException.ThrowIfNull(Textures);
        _ = Material.Validate();

        if (Textures.Count == 0)
        {
            throw new InvalidDataException("Material bundle must contain at least one texture export.");
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProceduralTextureExport texture in Textures)
        {
            _ = texture.Validate();
            if (!seenKeys.Add(texture.Key))
            {
                throw new InvalidDataException($"Texture export key '{texture.Key}' is duplicated.");
            }
        }

        foreach (string key in Material.TextureRefs.Values)
        {
            if (!seenKeys.Contains(key))
            {
                throw new InvalidDataException($"Material references missing texture export '{key}'.");
            }
        }

        return this;
    }
}

public static class ProceduralMaterialFactory
{
    public static ProceduralLitMaterialBundle CreateLitPbrFromSurface(
        ProceduralTextureSurface surface,
        string textureKeyPrefix,
        float roughness = 0.5f,
        float metallic = 0.0f)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (string.IsNullOrWhiteSpace(textureKeyPrefix))
        {
            throw new ArgumentException("Texture key prefix cannot be empty.", nameof(textureKeyPrefix));
        }

        surface = surface.Validate();

        string albedoKey = $"{textureKeyPrefix}.albedo";
        string normalKey = $"{textureKeyPrefix}.normal";
        string roughnessKey = $"{textureKeyPrefix}.roughness";
        string aoKey = $"{textureKeyPrefix}.ao";

        ProceduralMaterial baseMaterial = MaterialTemplates.CreateLitPbr(albedoKey, normalKey, roughness, metallic);
        var textureRefs = new Dictionary<string, string>(baseMaterial.TextureRefs, StringComparer.Ordinal)
        {
            ["roughness"] = roughnessKey,
            ["ao"] = aoKey
        };
        var material = new ProceduralMaterial(
            Template: MaterialTemplateId.DffLitPbr,
            Scalars: new Dictionary<string, float>(baseMaterial.Scalars, StringComparer.Ordinal),
            Vectors: new Dictionary<string, Vector4>(baseMaterial.Vectors, StringComparer.Ordinal),
            TextureRefs: textureRefs).Validate();

        IReadOnlyList<TextureMipLevel> albedoMips = CloneMipChain(surface.MipChain);
        IReadOnlyList<TextureMipLevel> normalMips = CloneMipChain(
            TextureBuilder.GenerateMipChainRgba8(surface.NormalRgba8, surface.Width, surface.Height));
        IReadOnlyList<TextureMipLevel> roughnessMips = CloneMipChain(
            TextureBuilder.GenerateMipChainRgba8(surface.RoughnessRgba8, surface.Width, surface.Height));
        IReadOnlyList<TextureMipLevel> aoMips = CloneMipChain(
            TextureBuilder.GenerateMipChainRgba8(surface.AmbientOcclusionRgba8, surface.Width, surface.Height));

        IReadOnlyList<ProceduralTextureExport> textures =
        [
            CreateTextureExport(albedoKey, surface.Width, surface.Height, surface.AlbedoRgba8, albedoMips),
            CreateTextureExport(normalKey, surface.Width, surface.Height, surface.NormalRgba8, normalMips),
            CreateTextureExport(roughnessKey, surface.Width, surface.Height, surface.RoughnessRgba8, roughnessMips),
            CreateTextureExport(aoKey, surface.Width, surface.Height, surface.AmbientOcclusionRgba8, aoMips)
        ];

        return new ProceduralLitMaterialBundle(material, textures).Validate();
    }

    private static ProceduralTextureExport CreateTextureExport(
        string key,
        int width,
        int height,
        byte[] rgba8,
        IReadOnlyList<TextureMipLevel> mipChain)
    {
        return new ProceduralTextureExport(
            key,
            width,
            height,
            rgba8.ToArray(),
            mipChain).Validate();
    }

    private static IReadOnlyList<TextureMipLevel> CloneMipChain(IReadOnlyList<TextureMipLevel> sourceMipChain)
    {
        ArgumentNullException.ThrowIfNull(sourceMipChain);
        if (sourceMipChain.Count == 0)
        {
            return Array.Empty<TextureMipLevel>();
        }

        var cloned = new TextureMipLevel[sourceMipChain.Count];
        for (int i = 0; i < sourceMipChain.Count; i++)
        {
            TextureMipLevel mip = sourceMipChain[i].Validate();
            cloned[i] = new TextureMipLevel(mip.Width, mip.Height, mip.Rgba8.ToArray()).Validate();
        }

        return cloned;
    }
}
