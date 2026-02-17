using System.Numerics;
using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class ProceduralAnimationTimelineTests
{
    [Fact]
    public void EvaluateTimeline_InterpolatesBetweenKeyframes()
    {
        IReadOnlyList<TimelineKeyframe> keyframes =
        [
            new TimelineKeyframe(0f, 0f),
            new TimelineKeyframe(2f, 10f)
        ];

        float sample = ProceduralAnimation.EvaluateTimeline(keyframes, 1f);

        Assert.Equal(5f, sample, 3);
    }

    [Fact]
    public void EvaluateTimeline_UsesCurveFromCurrentKeyframe()
    {
        IReadOnlyList<TimelineKeyframe> keyframes =
        [
            new TimelineKeyframe(0f, 0f, TweenCurve.EaseIn),
            new TimelineKeyframe(1f, 10f)
        ];

        float sample = ProceduralAnimation.EvaluateTimeline(keyframes, 0.5f);

        Assert.Equal(2.5f, sample, 3);
    }

    [Fact]
    public void EvaluateTimeline_SupportsLooping()
    {
        IReadOnlyList<TimelineKeyframe> keyframes =
        [
            new TimelineKeyframe(0f, 0f),
            new TimelineKeyframe(1f, 10f),
            new TimelineKeyframe(2f, 0f)
        ];

        float looped = ProceduralAnimation.EvaluateTimeline(keyframes, 2.5f, loop: true);
        float clamped = ProceduralAnimation.EvaluateTimeline(keyframes, 2.5f, loop: false);

        Assert.Equal(5f, looped, 3);
        Assert.Equal(0f, clamped, 3);
    }

    [Fact]
    public void EvaluateTimeline_ValidatesArguments()
    {
        Assert.Throws<InvalidDataException>(() => ProceduralAnimation.EvaluateTimeline([], 0f));
        Assert.Throws<InvalidDataException>(() => ProceduralAnimation.EvaluateTimeline(
            [new TimelineKeyframe(1f, 1f), new TimelineKeyframe(0f, 0f)],
            0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineKeyframe(float.NaN, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineKeyframe(0f, float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimelineKeyframe(0f, 0f, (TweenCurve)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.EvaluateTimeline(
            [new TimelineKeyframe(0f, 1f)],
            float.NaN));
    }

    [Fact]
    public void EvaluateWobble_IsDeterministicAndBounded()
    {
        float first = ProceduralAnimation.EvaluateSeededWobble(10f, 1.25f, 2f, 3f, seed: 7u);
        float second = ProceduralAnimation.EvaluateSeededWobble(10f, 1.25f, 2f, 3f, seed: 7u);
        float differentSeed = ProceduralAnimation.EvaluateSeededWobble(10f, 1.25f, 2f, 3f, seed: 8u);

        Assert.Equal(first, second, 6);
        Assert.NotEqual(first, differentSeed);
        Assert.InRange(first, 8f, 12f);
    }

    [Fact]
    public void EvaluateWobble_VectorVariantAppliesAxisOffsets()
    {
        Vector3 value = ProceduralAnimation.EvaluateWobble(
            baseValue: new Vector3(1f, 2f, 3f),
            time: 0.75f,
            amplitude: new Vector3(0.5f, 0.25f, 0.75f),
            frequency: 2f,
            phase: 0.1f);

        Assert.InRange(value.X, 0.5f, 1.5f);
        Assert.InRange(value.Y, 1.75f, 2.25f);
        Assert.InRange(value.Z, 2.25f, 3.75f);
        Assert.NotEqual(value.X, value.Y);
    }

    [Fact]
    public void EvaluateWobble_ValidatesArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.EvaluateWobble(0f, float.NaN, 1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.EvaluateWobble(0f, 0f, -1f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.EvaluateWobble(0f, 0f, 1f, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralAnimation.EvaluateWobble(
            Vector3.Zero,
            0f,
            new Vector3(-1f, 0f, 0f),
            1f));
    }
}
