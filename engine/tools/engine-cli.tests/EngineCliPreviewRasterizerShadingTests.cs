using System.Numerics;
using System.Reflection;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewRasterizerShadingTests
{
    [Fact]
    public void EvaluatePbrLighting_ShouldChangeResponse_WhenMetallicChanges()
    {
        MethodInfo method = GetRequiredMethod("EvaluatePbrLighting");
        Vector3 baseColor = new(0.8f, 0.55f, 0.2f);
        Vector3 normal = Vector3.Normalize(new Vector3(0.2f, 0.1f, 0.97f));
        Vector3 light = Vector3.Normalize(new Vector3(0.5f, 0.62f, 0.61f));
        Vector3 view = Vector3.UnitZ;

        Vector3 dielectric = (Vector3)(method.Invoke(
            null,
            [baseColor, normal, light, view, 0.25f, 0f, 1f]) ?? throw new InvalidOperationException("Method returned null."));
        Vector3 metallic = (Vector3)(method.Invoke(
            null,
            [baseColor, normal, light, view, 0.25f, 1f, 1f]) ?? throw new InvalidOperationException("Method returned null."));

        float diff = Vector3.Distance(dielectric, metallic);
        Assert.True(diff > 0.03f, $"Expected metallic workflow to alter shading response. diff={diff:F6}");
    }

    [Fact]
    public void EvaluatePbrLighting_ShouldProduceSharperHighlight_ForLowerRoughness()
    {
        MethodInfo method = GetRequiredMethod("EvaluatePbrLighting");
        Vector3 baseColor = new(0.92f, 0.86f, 0.74f);
        Vector3 normal = Vector3.Normalize(new Vector3(0.12f, 0.08f, 0.99f));
        Vector3 light = Vector3.Normalize(new Vector3(0.4f, 0.2f, 0.9f));
        Vector3 view = Vector3.UnitZ;

        Vector3 glossy = (Vector3)(method.Invoke(
            null,
            [baseColor, normal, light, view, 0.1f, 0.85f, 1f]) ?? throw new InvalidOperationException("Method returned null."));
        Vector3 rough = (Vector3)(method.Invoke(
            null,
            [baseColor, normal, light, view, 0.9f, 0.85f, 1f]) ?? throw new InvalidOperationException("Method returned null."));

        Assert.True(Luminance(glossy) > Luminance(rough), "Low-roughness surface should produce stronger specular response.");
    }

    [Fact]
    public void EvaluatePbrLighting_ShouldReturnFiniteColor_ForFiniteInputs()
    {
        MethodInfo method = GetRequiredMethod("EvaluatePbrLighting");
        Vector3 color = (Vector3)(method.Invoke(
            null,
            [new Vector3(1.2f, -0.3f, 2.5f), Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, -1f, 2f, -0.2f])
            ?? throw new InvalidOperationException("Method returned null."));

        Assert.True(float.IsFinite(color.X));
        Assert.True(float.IsFinite(color.Y));
        Assert.True(float.IsFinite(color.Z));
    }

    private static MethodInfo GetRequiredMethod(string methodName)
    {
        Type rasterizerType = typeof(EngineCliApp).Assembly.GetType("Engine.Cli.ProceduralPreviewRasterizer")
            ?? throw new InvalidOperationException("Could not resolve ProceduralPreviewRasterizer type.");
        return rasterizerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not resolve method '{methodName}'.");
    }

    private static float Luminance(Vector3 color)
    {
        return (0.2126f * color.X) + (0.7152f * color.Y) + (0.0722f * color.Z);
    }
}
