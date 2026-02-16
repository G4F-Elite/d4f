namespace Engine.Audio;

public sealed record AmbienceLayer(
    string EventId,
    float AverageIntervalSeconds,
    float TriggerProbability,
    float Gain)
{
    public AmbienceLayer Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId))
        {
            throw new ArgumentException("Event id cannot be empty.", nameof(EventId));
        }

        if (AverageIntervalSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(AverageIntervalSeconds), "Average interval must be greater than zero.");
        }

        if (TriggerProbability < 0f || TriggerProbability > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(TriggerProbability), "Trigger probability must be within [0,1].");
        }

        if (Gain < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Gain), "Gain cannot be negative.");
        }

        return this;
    }
}

public sealed record AmbienceDirectorConfig(
    ulong Seed,
    int TickRateHz,
    IReadOnlyList<AmbienceLayer> Layers)
{
    public AmbienceDirectorConfig Validate()
    {
        if (TickRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TickRateHz), "Tick rate must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(Layers);
        if (Layers.Count == 0)
        {
            throw new InvalidDataException("Ambience director requires at least one layer.");
        }

        foreach (AmbienceLayer layer in Layers)
        {
            _ = layer.Validate();
        }

        return this;
    }
}

public sealed record AmbienceEvent(long Tick, string EventId, float Gain);

public sealed class AmbienceDirector
{
    private readonly int _tickRateHz;
    private readonly LayerState[] _layers;
    private readonly XorShift64 _rng;
    private long _currentTick;

    public AmbienceDirector(AmbienceDirectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _ = config.Validate();

        _tickRateHz = config.TickRateHz;
        _rng = new XorShift64(config.Seed == 0u ? 0xDFF12345u : config.Seed);
        _layers = config.Layers.Select(CreateInitialLayerState).ToArray();
    }

    public long CurrentTick => _currentTick;

    public IReadOnlyList<AmbienceEvent> AdvanceTo(long tick)
    {
        if (tick < _currentTick)
        {
            throw new InvalidDataException($"Tick {tick} cannot be less than current tick {_currentTick}.");
        }

        var events = new List<AmbienceEvent>();

        for (int i = 0; i < _layers.Length; i++)
        {
            LayerState layer = _layers[i];
            while (layer.NextTick <= tick)
            {
                if (_rng.NextFloat01() <= layer.Layer.TriggerProbability)
                {
                    events.Add(new AmbienceEvent(layer.NextTick, layer.Layer.EventId, layer.Layer.Gain));
                }

                layer = layer with { NextTick = layer.NextTick + ComputeNextIntervalTicks(layer.Layer) };
            }

            _layers[i] = layer;
        }

        _currentTick = tick;
        return events.OrderBy(static x => x.Tick).ThenBy(static x => x.EventId, StringComparer.Ordinal).ToArray();
    }

    private LayerState CreateInitialLayerState(AmbienceLayer layer)
    {
        long firstTick = ComputeNextIntervalTicks(layer);
        return new LayerState(layer, firstTick);
    }

    private long ComputeNextIntervalTicks(AmbienceLayer layer)
    {
        float jitter = 0.75f + _rng.NextFloat01() * 0.5f;
        float intervalSeconds = MathF.Max(0.001f, layer.AverageIntervalSeconds * jitter);
        return Math.Max(1, (long)MathF.Round(intervalSeconds * _tickRateHz));
    }

    private readonly record struct LayerState(AmbienceLayer Layer, long NextTick);

    private struct XorShift64
    {
        private ulong _state;

        public XorShift64(ulong seed)
        {
            _state = seed == 0u ? 0x9E3779B97F4A7C15u : seed;
        }

        public float NextFloat01()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            double normalized = (_state >> 11) * (1.0 / (1ul << 53));
            return (float)normalized;
        }
    }
}
