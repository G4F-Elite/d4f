namespace Engine.Net;

public sealed class DeterministicNetClock
{
    public DeterministicNetClock(int tickRateHz)
    {
        if (tickRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickRateHz), "Tick rate must be greater than zero.");
        }

        TickRateHz = tickRateHz;
        FixedDeltaSeconds = 1d / tickRateHz;
    }

    public int TickRateHz { get; }

    public double FixedDeltaSeconds { get; }

    public long CurrentTick { get; private set; }

    public long Step()
    {
        CurrentTick = checked(CurrentTick + 1);
        return CurrentTick;
    }

    public void Reset(long tick = 0)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Tick cannot be negative.");
        }

        CurrentTick = tick;
    }
}
