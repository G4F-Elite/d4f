using System;
using RenderSettingsValue = Engine.Rendering.RenderSettings;

namespace Engine.App;

public sealed class GameHostOptions
{
    private const int DefaultFrameArenaBytes = 1 * 1024 * 1024;
    private const int DefaultFrameArenaAlignment = 64;
    private const int DefaultMaxSubsteps = 8;
    private static readonly TimeSpan DefaultFixedDt = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
    private static readonly TimeSpan DefaultMaxAccumulatedTime = TimeSpan.FromTicks(DefaultFixedDt.Ticks * DefaultMaxSubsteps);

    public static GameHostOptions Default { get; } = new(
        DefaultFixedDt,
        DefaultMaxSubsteps,
        DefaultFrameArenaBytes,
        DefaultFrameArenaAlignment,
        DefaultMaxAccumulatedTime,
        DeterministicModeOptions.Disabled,
        RenderSettingsValue.Default,
        InteropBudgetOptions.ReleaseStrict,
        maxManagedAllocatedBytesPerFrame: null,
        maxTotalCpuTimePerFrame: null);

    public GameHostOptions(
        TimeSpan fixedDt,
        int maxSubsteps,
        int frameArenaBytes,
        int frameArenaAlignment,
        TimeSpan? maxAccumulatedTime = null,
        DeterministicModeOptions? deterministicMode = null,
        RenderSettingsValue? renderSettings = null,
        InteropBudgetOptions? interopBudgets = null,
        long? maxManagedAllocatedBytesPerFrame = null,
        TimeSpan? maxTotalCpuTimePerFrame = null)
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

        TimeSpan resolvedMaxAccumulatedTime = maxAccumulatedTime ?? TimeSpan.FromTicks(fixedDt.Ticks * maxSubsteps);
        if (resolvedMaxAccumulatedTime < fixedDt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAccumulatedTime),
                "Max accumulated time must be greater than or equal to fixed delta time.");
        }

        if (maxManagedAllocatedBytesPerFrame.HasValue && maxManagedAllocatedBytesPerFrame.Value <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxManagedAllocatedBytesPerFrame),
                "Managed allocation budget per frame must be greater than zero when specified.");
        }

        if (maxTotalCpuTimePerFrame.HasValue && maxTotalCpuTimePerFrame.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTotalCpuTimePerFrame),
                "Total CPU budget per frame must be greater than zero when specified.");
        }

        FixedDt = fixedDt;
        MaxSubsteps = maxSubsteps;
        FrameArenaBytes = frameArenaBytes;
        FrameArenaAlignment = frameArenaAlignment;
        MaxAccumulatedTime = resolvedMaxAccumulatedTime;
        DeterministicMode = deterministicMode ?? DeterministicModeOptions.Disabled;
        RenderSettings = renderSettings ?? RenderSettingsValue.Default;
        InteropBudgets = interopBudgets ?? InteropBudgetOptions.ReleaseStrict;
        MaxManagedAllocatedBytesPerFrame = maxManagedAllocatedBytesPerFrame;
        MaxTotalCpuTimePerFrame = maxTotalCpuTimePerFrame;
    }

    public TimeSpan FixedDt { get; }

    public int MaxSubsteps { get; }

    public int FrameArenaBytes { get; }

    public int FrameArenaAlignment { get; }

    public TimeSpan MaxAccumulatedTime { get; }

    public DeterministicModeOptions DeterministicMode { get; }

    public RenderSettingsValue RenderSettings { get; }

    public InteropBudgetOptions InteropBudgets { get; }

    public long? MaxManagedAllocatedBytesPerFrame { get; }

    public TimeSpan? MaxTotalCpuTimePerFrame { get; }

    private static bool IsPowerOfTwo(int value)
    {
        return (value & (value - 1)) == 0;
    }
}
