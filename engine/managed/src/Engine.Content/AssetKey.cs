using System.Security.Cryptography;
using System.Text;

namespace Engine.Content;

public readonly record struct AssetKey
{
    public AssetKey(
        string generatorId,
        int generatorVersion,
        int recipeVersion,
        string recipeHash,
        string buildConfigHash)
    {
        if (string.IsNullOrWhiteSpace(generatorId))
        {
            throw new ArgumentException("Generator id cannot be empty.", nameof(generatorId));
        }

        if (generatorVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Generator version must be greater than zero.");
        }

        if (recipeVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeVersion), "Recipe version must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(recipeHash))
        {
            throw new ArgumentException("Recipe hash cannot be empty.", nameof(recipeHash));
        }

        if (string.IsNullOrWhiteSpace(buildConfigHash))
        {
            throw new ArgumentException("Build config hash cannot be empty.", nameof(buildConfigHash));
        }

        GeneratorId = generatorId;
        GeneratorVersion = generatorVersion;
        RecipeVersion = recipeVersion;
        RecipeHash = recipeHash;
        BuildConfigHash = buildConfigHash;
    }

    public string GeneratorId { get; }

    public int GeneratorVersion { get; }

    public int RecipeVersion { get; }

    public string RecipeHash { get; }

    public string BuildConfigHash { get; }

    public override string ToString()
    {
        return $"{GeneratorId}:{GeneratorVersion}:{RecipeVersion}:{BuildConfigHash}:{RecipeHash}";
    }
}

public static class AssetKeyBuilder
{
    public static AssetKey Create(IAssetRecipe recipe, int generatorVersion, string buildConfigHash)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        if (generatorVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Generator version must be greater than zero.");
        }

        string recipeHash = ComputeRecipeHash(recipe);
        return new AssetKey(
            recipe.GeneratorId,
            generatorVersion,
            recipe.RecipeVersion,
            recipeHash,
            buildConfigHash);
    }

    public static string ComputeRecipeHash(IAssetRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        using SHA256 sha256 = SHA256.Create();
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(recipe.GeneratorId);
            writer.Write(recipe.RecipeVersion);
            writer.Write(recipe.Seed);
            recipe.Write(writer);
            writer.Flush();
        }

        byte[] hash = sha256.ComputeHash(buffer.ToArray());
        return Convert.ToHexString(hash);
    }

    public static string ComputeBuildConfigHash(IReadOnlyDictionary<string, string> configValues)
    {
        ArgumentNullException.ThrowIfNull(configValues);

        using SHA256 sha256 = SHA256.Create();
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(configValues.Count);

            foreach (KeyValuePair<string, string> pair in configValues.OrderBy(static x => x.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new InvalidDataException("Build config key cannot be empty.");
                }

                if (pair.Value is null)
                {
                    throw new InvalidDataException($"Build config value cannot be null for key '{pair.Key}'.");
                }

                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }

            writer.Flush();
        }

        byte[] hash = sha256.ComputeHash(buffer.ToArray());
        return Convert.ToHexString(hash);
    }
}
