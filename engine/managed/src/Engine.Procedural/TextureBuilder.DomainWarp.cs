namespace Engine.Procedural;

public static partial class TextureBuilder
{
    private static (float U, float V) ApplyDomainWarp(float u, float v, ProceduralTextureRecipe recipe)
    {
        if (recipe.DomainWarpStrength <= 0f)
        {
            return (u, v);
        }

        float strength = recipe.DomainWarpStrength;
        float frequency = recipe.DomainWarpFrequency;
        uint seed = recipe.Seed;

        float warpX = (ValueNoise((u + 13.1f) * frequency, (v + 5.7f) * frequency, seed ^ 0x9E3779B9u) * 2f - 1f) * strength;
        float warpY = (ValueNoise((u - 7.3f) * frequency, (v + 11.9f) * frequency, seed ^ 0x85EBCA6Bu) * 2f - 1f) * strength;

        return (Frac(u + warpX), Frac(v + warpY));
    }
}
