namespace Engine.Net;

public sealed class ClientInterpolationBuffer
{
    private NetSnapshot? _previous;
    private NetSnapshot? _current;

    public void Push(NetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (_current is not null && snapshot.Tick <= _current.Tick)
        {
            throw new InvalidDataException($"Snapshot tick {snapshot.Tick} must be greater than current tick {_current.Tick}.");
        }

        _previous = _current;
        _current = snapshot;
    }

    public bool TryGetWindow(out NetSnapshot from, out NetSnapshot to)
    {
        if (_previous is null || _current is null)
        {
            from = null!;
            to = null!;
            return false;
        }

        from = _previous;
        to = _current;
        return true;
    }

    public static float ComputeAlpha(long renderTick, NetSnapshot from, NetSnapshot to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (to.Tick <= from.Tick)
        {
            throw new InvalidDataException("Interpolation window must have a positive tick range.");
        }

        if (renderTick <= from.Tick)
        {
            return 0f;
        }

        if (renderTick >= to.Tick)
        {
            return 1f;
        }

        return (float)(renderTick - from.Tick) / (to.Tick - from.Tick);
    }
}
