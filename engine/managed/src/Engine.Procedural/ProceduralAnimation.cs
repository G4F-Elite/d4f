using System.Numerics;

namespace Engine.Procedural;

public enum TweenCurve
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

public static class ProceduralAnimation
{
    public static float EvaluateTween(float t, TweenCurve curve)
    {
        float clamped = Math.Clamp(t, 0f, 1f);
        return curve switch
        {
            TweenCurve.Linear => clamped,
            TweenCurve.EaseIn => clamped * clamped,
            TweenCurve.EaseOut => 1f - (1f - clamped) * (1f - clamped),
            TweenCurve.EaseInOut => clamped < 0.5f
                ? 2f * clamped * clamped
                : 1f - MathF.Pow(-2f * clamped + 2f, 2f) / 2f,
            _ => throw new InvalidDataException($"Unsupported tween curve value: {curve}.")
        };
    }

    public static Quaternion LookAt(Vector3 from, Vector3 to, Vector3 up)
    {
        Vector3 forward = to - from;
        if (forward.LengthSquared() <= 1e-8f)
        {
            return Quaternion.Identity;
        }

        Vector3 normalizedForward = Vector3.Normalize(forward);
        Vector3 normalizedUp = up.LengthSquared() <= 1e-8f ? Vector3.UnitY : Vector3.Normalize(up);

        Matrix4x4 view = Matrix4x4.CreateLookAt(Vector3.Zero, normalizedForward, normalizedUp);
        Matrix4x4 rotation = Matrix4x4.Transpose(view);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation));
    }

    public static Vector3 AimDirection(Vector3 origin, Vector3 target)
    {
        Vector3 dir = target - origin;
        return dir.LengthSquared() <= 1e-8f ? Vector3.UnitZ : Vector3.Normalize(dir);
    }
}
