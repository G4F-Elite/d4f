using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralAnimationLookAtTests
{
    [Fact]
    public void LookAt_ShouldReturnIdentity_WhenTargetMatchesOrigin()
    {
        Quaternion rotation = ProceduralAnimation.LookAt(Vector3.One, Vector3.One, Vector3.UnitY);

        Assert.Equal(Quaternion.Identity, rotation);
    }

    [Fact]
    public void LookAt_ShouldRotateForwardAxisTowardsTarget()
    {
        Quaternion rotation = ProceduralAnimation.LookAt(
            from: Vector3.Zero,
            to: new Vector3(4f, 0f, 0f),
            up: Vector3.UnitY);
        Vector3 forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));

        Assert.InRange(forward.X, 0.999f, 1.001f);
        Assert.InRange(MathF.Abs(forward.Y), 0f, 0.001f);
        Assert.InRange(MathF.Abs(forward.Z), 0f, 0.001f);
    }

    [Fact]
    public void LookAt_ShouldHandleCollinearUp()
    {
        Quaternion rotation = ProceduralAnimation.LookAt(
            from: Vector3.Zero,
            to: Vector3.UnitY * 3f,
            up: Vector3.UnitY);
        Vector3 forward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));

        Assert.True(float.IsFinite(rotation.X));
        Assert.True(float.IsFinite(rotation.Y));
        Assert.True(float.IsFinite(rotation.Z));
        Assert.True(float.IsFinite(rotation.W));
        Assert.InRange(forward.Y, 0.999f, 1.001f);
    }

    [Fact]
    public void AimDirection_ShouldNormalizeDirectionAndFallbackToForward()
    {
        Vector3 normalized = ProceduralAnimation.AimDirection(Vector3.Zero, new Vector3(0f, 2f, 0f));
        Vector3 fallback = ProceduralAnimation.AimDirection(Vector3.One, Vector3.One);

        Assert.Equal(Vector3.UnitY, normalized);
        Assert.Equal(Vector3.UnitZ, fallback);
    }

    [Fact]
    public void AimRotation_ShouldMatchLookAt()
    {
        Quaternion lookAt = ProceduralAnimation.LookAt(
            from: new Vector3(1f, 2f, 3f),
            to: new Vector3(-2f, 4f, 6f),
            up: Vector3.UnitY);
        Quaternion aimRotation = ProceduralAnimation.AimRotation(
            origin: new Vector3(1f, 2f, 3f),
            target: new Vector3(-2f, 4f, 6f),
            up: Vector3.UnitY);

        Assert.Equal(lookAt, aimRotation);
    }

    [Fact]
    public void LookAtAndAim_ShouldValidateFiniteInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.LookAt(
            new Vector3(float.NaN, 0f, 0f),
            Vector3.Zero,
            Vector3.UnitY));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.LookAt(
            Vector3.Zero,
            Vector3.One,
            new Vector3(0f, float.PositiveInfinity, 0f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.AimDirection(
            Vector3.Zero,
            new Vector3(0f, float.NaN, 0f)));
    }
}
