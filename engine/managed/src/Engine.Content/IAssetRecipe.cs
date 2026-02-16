using System.IO;

namespace Engine.Content;

public interface IAssetRecipe
{
    string GeneratorId { get; }

    int RecipeVersion { get; }

    ulong Seed { get; }

    void Write(BinaryWriter writer);
}

public interface IAssetGenerator<in TRecipe, out TOutput>
    where TRecipe : IAssetRecipe
{
    int GeneratorVersion { get; }

    TOutput Generate(TRecipe recipe);
}
