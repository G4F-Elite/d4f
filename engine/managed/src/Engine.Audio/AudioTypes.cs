namespace Engine.Audio;

public readonly record struct AudioEmitterHandle(ulong Value)
{
    public bool IsValid => Value != 0u;
}

public readonly record struct ListenerState(float PositionX, float PositionY, float PositionZ);

public readonly record struct AudioEmitterParameters(
    float Volume,
    float Pitch,
    float PositionX,
    float PositionY,
    float PositionZ)
{
    public AudioEmitterParameters Validate()
    {
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
