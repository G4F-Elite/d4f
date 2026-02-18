using System.Numerics;
using System.Reflection;

namespace Engine.Cli.Tests;

public sealed class EngineCliPreviewRasterizerSamplingTests
{
    [Fact]
    public void SampleRgbaBilinear_ShouldInterpolateNeighborPixels()
    {
        MethodInfo method = GetRequiredMethod("SampleRgbaBilinear");
        byte[] texture =
        [
            255, 0, 0, 255,      // (0,0) red
            0, 255, 0, 255,      // (1,0) green
            0, 0, 255, 255,      // (0,1) blue
            255, 255, 255, 255   // (1,1) white
        ];

        Vector3 result = (Vector3)(method.Invoke(null, [texture, 2, 2, 0.5f, 0.5f]) ?? throw new InvalidOperationException("Method returned null."));

        Assert.InRange(result.X, 0.49f, 0.51f);
        Assert.InRange(result.Y, 0.49f, 0.51f);
        Assert.InRange(result.Z, 0.49f, 0.51f);
    }

    [Fact]
    public void ApplyNormalMap_ShouldKeepFlatNormalAligned()
    {
        MethodInfo method = GetRequiredMethod("ApplyNormalMap");

        Vector3 result = (Vector3)(method.Invoke(
            null,
            [Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0.7f]) ?? throw new InvalidOperationException("Method returned null."));

        Assert.InRange(result.X, -0.01f, 0.01f);
        Assert.InRange(result.Y, -0.01f, 0.01f);
        Assert.InRange(result.Z, 0.99f, 1.01f);
    }

    [Fact]
    public void ApplyNormalMap_ShouldTiltTowardTangent()
    {
        MethodInfo method = GetRequiredMethod("ApplyNormalMap");
        Vector3 tangentSpaceNormal = Vector3.Normalize(new Vector3(1f, 0f, 1f));

        Vector3 result = (Vector3)(method.Invoke(
            null,
            [Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, tangentSpaceNormal, 1.0f]) ?? throw new InvalidOperationException("Method returned null."));

        Assert.True(result.X > 0.6f, $"Expected strong tangent tilt, but got X={result.X:F4}.");
        Assert.True(result.Z < 0.9f, $"Expected reduced Z after tilt, but got Z={result.Z:F4}.");
    }

    private static MethodInfo GetRequiredMethod(string methodName)
    {
        Type rasterizerType = typeof(EngineCliApp).Assembly.GetType("Engine.Cli.ProceduralPreviewRasterizer")
            ?? throw new InvalidOperationException("Could not resolve ProceduralPreviewRasterizer type.");
        return rasterizerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not resolve method '{methodName}'.");
    }
}
