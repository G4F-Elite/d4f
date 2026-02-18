using System.Numerics;

namespace Engine.Cli;

internal static partial class ProceduralPreviewRasterizer
{
    private static Vector3 EvaluatePbrLighting(
        Vector3 baseColor,
        Vector3 normal,
        Vector3 lightDir,
        Vector3 viewDir,
        float roughness,
        float metallic,
        float ambientOcclusion)
    {
        Vector3 albedo = Vector3.Clamp(baseColor, Vector3.Zero, Vector3.One);
        Vector3 n = NormalizeOrFallback(normal, Vector3.UnitZ);
        Vector3 l = NormalizeOrFallback(lightDir, Vector3.Normalize(new Vector3(0.5f, 0.62f, 0.61f)));
        Vector3 v = NormalizeOrFallback(viewDir, Vector3.UnitZ);
        Vector3 h = NormalizeOrFallback(l + v, n);

        float ndotl = MathF.Max(0f, Vector3.Dot(n, l));
        float ndotv = MathF.Max(0.0001f, Vector3.Dot(n, v));
        float ndoth = MathF.Max(0f, Vector3.Dot(n, h));
        float vdoth = MathF.Max(0f, Vector3.Dot(v, h));
        float clampedRoughness = Math.Clamp(roughness, 0.04f, 1f);
        float clampedMetallic = Math.Clamp(metallic, 0f, 1f);
        float ao = Math.Clamp(ambientOcclusion, 0f, 1f);

        Vector3 f0 = Vector3.Lerp(new Vector3(0.04f), albedo, clampedMetallic);
        Vector3 fresnel = F_Schlick(vdoth, f0);
        float distribution = D_Ggx(ndoth, clampedRoughness);
        float geometry = G_SmithCorrelated(ndotl, ndotv, clampedRoughness);
        float specBoost = Lerp(1.15f, 2.35f, 1f - clampedRoughness);
        Vector3 specular = fresnel * (distribution * geometry / MathF.Max(4f * ndotl * ndotv, 1e-4f)) * specBoost;

        Vector3 kd = (Vector3.One - fresnel) * (1f - clampedMetallic);
        Vector3 diffuse = (kd * albedo) / MathF.PI;
        Vector3 direct = (diffuse + specular) * ndotl * ao * 2.25f;
        Vector3 ambient = albedo * (0.08f + (0.14f * ao)) * (1f - (0.45f * clampedMetallic));
        float highlightExponent = Lerp(180f, 24f, clampedRoughness);
        float highlight = MathF.Pow(ndoth, highlightExponent) * Lerp(0.38f, 0.11f, clampedRoughness);
        Vector3 highlightColor = Vector3.Lerp(Vector3.One * 0.65f, albedo, clampedMetallic) * highlight;
        float rim = Pow5(1f - MathF.Max(0f, Vector3.Dot(n, v))) * Lerp(0.015f, 0.08f, clampedMetallic);
        return ambient + direct + highlightColor + (fresnel * rim);
    }

    private static float D_Ggx(float ndoth, float roughness)
    {
        float alpha = roughness * roughness;
        float alpha2 = alpha * alpha;
        float ndoth2 = ndoth * ndoth;
        float denom = (ndoth2 * (alpha2 - 1f)) + 1f;
        return alpha2 / MathF.Max(MathF.PI * denom * denom, 1e-4f);
    }

    private static float G_SchlickGgx(float ndotx, float roughness)
    {
        float alpha = roughness * roughness;
        float k = ((alpha + 1f) * (alpha + 1f)) / 8f;
        return ndotx / ((ndotx * (1f - k)) + k);
    }

    private static float G_SmithCorrelated(float ndotl, float ndotv, float roughness)
    {
        float gl = G_SchlickGgx(ndotl, roughness);
        float gv = G_SchlickGgx(ndotv, roughness);
        return gl * gv;
    }

    private static Vector3 F_Schlick(float vdoth, Vector3 f0)
    {
        float oneMinus = 1f - vdoth;
        float factor = Pow5(oneMinus);
        return f0 + ((Vector3.One - f0) * factor);
    }

    private static float Pow5(float x)
    {
        float x2 = x * x;
        return x2 * x2 * x;
    }
}
