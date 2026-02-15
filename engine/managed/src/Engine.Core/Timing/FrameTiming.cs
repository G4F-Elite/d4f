using System;

namespace Engine.Core.Timing;

public readonly record struct FrameTiming
{
    public FrameTiming(long frameNumber, TimeSpan deltaTime, TimeSpan totalTime)
    {
        if (frameNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameNumber), "Frame number cannot be negative.");
        }

        if (deltaTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time cannot be negative.");
        }

        if (totalTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTime), "Total time cannot be negative.");
        }

        FrameNumber = frameNumber;
        DeltaTime = deltaTime;
        TotalTime = totalTime;
    }

    public long FrameNumber { get; }

    public TimeSpan DeltaTime { get; }

    public TimeSpan TotalTime { get; }
}
