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
        float baseFrequency = recipe.DomainWarpFrequency;
        int octaves = recipe.DomainWarpOctaves;
        uint seed = recipe.Seed;

        float warpX = 0f;
        float warpY = 0f;
        float amplitude = 1f;
        float frequency = baseFrequency;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            uint octaveSeed = seed ^ ((uint)octave * 0x9E3779B9u);
            float sampleX = ValueNoise(
                (u + 13.1f) * frequency,
                (v + 5.7f) * frequency,
                octaveSeed ^ 0xA341316Cu) * 2f - 1f;
            float sampleY = ValueNoise(
                (u - 7.3f) * frequency,
                (v + 11.9f) * frequency,
                octaveSeed ^ 0x85EBCA6Bu) * 2f - 1f;

            warpX += sampleX * amplitude;
            warpY += sampleY * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        float normalization = MathF.Max(amplitudeSum, float.Epsilon);
        warpX = (warpX / normalization) * strength;
        warpY = (warpY / normalization) * strength;

        return (Frac(u + warpX), Frac(v + warpY));
    }
}
