using System.Numerics;

namespace Engine.Procedural;

public enum TweenCurve
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3
}

public readonly record struct TimelineKeyframe
{
    public TimelineKeyframe(float time, float value, TweenCurve curveToNext = TweenCurve.Linear)
    {
        if (!float.IsFinite(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Timeline keyframe time must be finite.");
        }

        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Timeline keyframe value must be finite.");
        }

        if (!Enum.IsDefined(curveToNext))
        {
            throw new ArgumentOutOfRangeException(nameof(curveToNext), curveToNext, "Unsupported tween curve.");
        }

        Time = time;
        Value = value;
        CurveToNext = curveToNext;
    }

    public float Time { get; }

    public float Value { get; }

    public TweenCurve CurveToNext { get; }
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

    public static float EvaluateTimeline(IReadOnlyList<TimelineKeyframe> keyframes, float time, bool loop = false)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        ValidateKeyframes(keyframes);

        if (!float.IsFinite(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Timeline time must be finite.");
        }

        if (keyframes.Count == 1)
        {
            return keyframes[0].Value;
        }

        float sampledTime = loop
            ? WrapTime(time, keyframes[0].Time, keyframes[^1].Time)
            : Math.Clamp(time, keyframes[0].Time, keyframes[^1].Time);

        if (sampledTime <= keyframes[0].Time)
        {
            return keyframes[0].Value;
        }

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            TimelineKeyframe current = keyframes[i];
            TimelineKeyframe next = keyframes[i + 1];
            if (sampledTime > next.Time)
            {
                continue;
            }

            float duration = next.Time - current.Time;
            if (duration <= 0f)
            {
                return next.Value;
            }

            float normalized = (sampledTime - current.Time) / duration;
            float eased = EvaluateTween(normalized, current.CurveToNext);
            return current.Value + (next.Value - current.Value) * eased;
        }

        return keyframes[^1].Value;
    }

    public static float EvaluateWobble(float baseValue, float time, float amplitude, float frequency, float phase = 0f)
    {
        if (!float.IsFinite(baseValue))
        {
            throw new ArgumentOutOfRangeException(nameof(baseValue), "Base value must be finite.");
        }

        if (!float.IsFinite(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Wobble time must be finite.");
        }

        if (!float.IsFinite(amplitude) || amplitude < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude), "Wobble amplitude must be finite and non-negative.");
        }

        if (!float.IsFinite(frequency) || frequency < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "Wobble frequency must be finite and non-negative.");
        }

        if (!float.IsFinite(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), "Wobble phase must be finite.");
        }

        float angle = (time + phase) * frequency * MathF.Tau;
        return baseValue + MathF.Sin(angle) * amplitude;
    }

    public static float EvaluateSeededWobble(float baseValue, float time, float amplitude, float frequency, uint seed)
    {
        float seededPhase = SeedToPhase(seed);
        return EvaluateWobble(baseValue, time, amplitude, frequency, seededPhase);
    }

    public static Vector3 EvaluateWobble(Vector3 baseValue, float time, Vector3 amplitude, float frequency, float phase = 0f)
    {
        ValidateFinite(baseValue, nameof(baseValue));
        ValidateFinite(amplitude, nameof(amplitude));
        if (amplitude.X < 0f || amplitude.Y < 0f || amplitude.Z < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(amplitude), "Vector wobble amplitudes must be non-negative.");
        }

        return new Vector3(
            EvaluateWobble(baseValue.X, time, amplitude.X, frequency, phase),
            EvaluateWobble(baseValue.Y, time, amplitude.Y, frequency, phase + 0.125f),
            EvaluateWobble(baseValue.Z, time, amplitude.Z, frequency, phase + 0.25f));
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

    private static void ValidateKeyframes(IReadOnlyList<TimelineKeyframe> keyframes)
    {
        if (keyframes.Count == 0)
        {
            throw new InvalidDataException("Timeline requires at least one keyframe.");
        }

        float previousTime = keyframes[0].Time;
        for (int i = 1; i < keyframes.Count; i++)
        {
            float currentTime = keyframes[i].Time;
            if (currentTime <= previousTime)
            {
                throw new InvalidDataException("Timeline keyframes must be sorted by strictly increasing time.");
            }

            previousTime = currentTime;
        }
    }

    private static float WrapTime(float time, float start, float end)
    {
        float length = end - start;
        if (length <= 0f)
        {
            return start;
        }

        float normalized = (time - start) % length;
        if (normalized < 0f)
        {
            normalized += length;
        }

        return start + normalized;
    }

    private static float SeedToPhase(uint seed)
    {
        uint hashed = seed;
        hashed ^= hashed >> 17;
        hashed *= 0xED5AD4BBu;
        hashed ^= hashed >> 11;
        hashed *= 0xAC4C1B51u;
        hashed ^= hashed >> 15;
        return (hashed & 0x00FFFFFFu) / 16777216f;
    }

    private static void ValidateFinite(Vector3 value, string paramName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentOutOfRangeException(paramName, "Vector components must be finite.");
        }
    }
}
