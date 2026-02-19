namespace Engine.Audio;

public readonly record struct AudioEmitterHandle(ulong Value)
{
    public bool IsValid => Value != 0u;
}

public readonly record struct ListenerState(float PositionX, float PositionY, float PositionZ)
{
    public ListenerState Validate()
    {
        if (!float.IsFinite(PositionX) || !float.IsFinite(PositionY) || !float.IsFinite(PositionZ))
        {
            throw new ArgumentOutOfRangeException(nameof(PositionX), "Listener position components must be finite.");
        }

        return this;
    }
}

public readonly record struct AudioBusParameters(
    AudioBus Bus,
    float Gain,
    float Lowpass,
    float ReverbSend,
    bool Muted)
{
    public AudioBusParameters Validate()
    {
        if (!Enum.IsDefined(Bus))
        {
            throw new InvalidDataException($"Unsupported audio bus value: {Bus}.");
        }

        if (!float.IsFinite(Gain) || !float.IsFinite(Lowpass) || !float.IsFinite(ReverbSend))
        {
            throw new ArgumentOutOfRangeException(nameof(Gain), "Bus parameters must be finite.");
        }

        if (Gain < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Gain), "Bus gain cannot be negative.");
        }

        if (Lowpass < 0f || Lowpass > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Lowpass), "Bus lowpass must be within [0, 1].");
        }

        if (ReverbSend < 0f || ReverbSend > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(ReverbSend), "Bus reverb send must be within [0, 1].");
        }

        return this;
    }
}

public readonly record struct AudioEmitterParameters(
    float Volume,
    float Pitch,
    float PositionX,
    float PositionY,
    float PositionZ)
{
    public AudioEmitterParameters Validate()
    {
        if (!float.IsFinite(Volume) || !float.IsFinite(Pitch) ||
            !float.IsFinite(PositionX) || !float.IsFinite(PositionY) || !float.IsFinite(PositionZ))
        {
            throw new ArgumentOutOfRangeException(nameof(Volume), "Emitter parameters must be finite.");
        }

        if (Volume < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Volume), "Volume cannot be negative.");
        }

        if (Pitch <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Pitch), "Pitch must be greater than zero.");
        }

        return this;
    }
}

public sealed record AudioPlayRequest(
    AudioBus Bus,
    float Volume,
    float Pitch,
    bool Loop,
    AudioEmitterParameters? InitialEmitter = null)
{
    public AudioPlayRequest Validate()
    {
        if (!Enum.IsDefined(Bus))
        {
            throw new InvalidDataException($"Unsupported audio bus value: {Bus}.");
        }

        if (!float.IsFinite(Volume) || !float.IsFinite(Pitch))
        {
            throw new ArgumentOutOfRangeException(nameof(Volume), "Play request volume and pitch must be finite.");
        }

        if (Volume < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Volume), "Volume cannot be negative.");
        }

        if (Pitch <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Pitch), "Pitch must be greater than zero.");
        }

        _ = InitialEmitter?.Validate();
        return this;
    }
}
