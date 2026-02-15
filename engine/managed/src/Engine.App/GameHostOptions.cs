using System;

namespace Engine.App;

public sealed class GameHostOptions
{
    private const int DefaultFrameArenaBytes = 1 * 1024 * 1024;
    private const int DefaultFrameArenaAlignment = 64;
    private const int DefaultMaxSubsteps = 8;
    private static readonly TimeSpan DefaultFixedDt = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

    public static GameHostOptions Default { get; } = new(
        DefaultFixedDt,
        DefaultMaxSubsteps,
        DefaultFrameArenaBytes,
        DefaultFrameArenaAlignment);

    public GameHostOptions(TimeSpan fixedDt, int maxSubsteps, int frameArenaBytes, int frameArenaAlignment)
    {
        if (fixedDt <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedDt), "Fixed delta time must be positive.");
        }

        if (maxSubsteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSubsteps), "Max substeps must be positive.");
        }

        if (frameArenaBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameArenaBytes), "Frame arena size must be positive.");
        }

        if (frameArenaAlignment <= 0 || !IsPowerOfTwo(frameArenaAlignment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameArenaAlignment),
                "Frame arena alignment must be a positive power of two.");
        }

        FixedDt = fixedDt;
        MaxSubsteps = maxSubsteps;
        FrameArenaBytes = frameArenaBytes;
        FrameArenaAlignment = frameArenaAlignment;
    }

    public TimeSpan FixedDt { get; }

    public int MaxSubsteps { get; }

    public int FrameArenaBytes { get; }

    public int FrameArenaAlignment { get; }

    private static bool IsPowerOfTwo(int value)
    {
        return (value & (value - 1)) == 0;
    }
}
