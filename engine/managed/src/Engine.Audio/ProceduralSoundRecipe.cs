namespace Engine.Audio;

public enum OscillatorType
{
    Sine = 0,
    Square = 1,
    Saw = 2,
    Triangle = 3,
    Noise = 4
}

public readonly record struct AdsrEnvelope(
    float AttackSeconds,
    float DecaySeconds,
    float SustainLevel,
    float ReleaseSeconds)
{
    public AdsrEnvelope Validate()
    {
        if (AttackSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(AttackSeconds), "Attack cannot be negative.");
        }

        if (DecaySeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(DecaySeconds), "Decay cannot be negative.");
        }

        if (SustainLevel < 0f || SustainLevel > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(SustainLevel), "Sustain level must be within [0,1].");
        }

        if (ReleaseSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(ReleaseSeconds), "Release cannot be negative.");
        }

        return this;
    }
}

public readonly record struct LfoSettings(float FrequencyHz, float Depth)
{
    public LfoSettings Validate()
    {
        if (FrequencyHz < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(FrequencyHz), "LFO frequency cannot be negative.");
        }

        if (Depth < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(Depth), "LFO depth cannot be negative.");
        }

        return this;
    }
}

public readonly record struct OnePoleLowPassFilter(float CutoffHz)
{
    public OnePoleLowPassFilter Validate()
    {
        if (CutoffHz <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(CutoffHz), "Filter cutoff must be greater than zero.");
        }

        return this;
    }
}

public sealed record ProceduralSoundRecipe(
    OscillatorType Oscillator,
    float FrequencyHz,
    float Gain,
    int SampleRate,
    uint Seed,
    AdsrEnvelope Envelope,
    LfoSettings Lfo,
    OnePoleLowPassFilter Filter)
{
    public ProceduralSoundRecipe Validate()
    {
        if (!Enum.IsDefined(Oscillator))
        {
            throw new InvalidDataException($"Unsupported oscillator value: {Oscillator}.");
        }

        if (FrequencyHz <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(FrequencyHz), "Frequency must be greater than zero.");
        }

        if (Gain < 0f || Gain > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(Gain), "Gain must be within [0,1].");
        }

        if (SampleRate < 8000)
        {
            throw new ArgumentOutOfRangeException(nameof(SampleRate), "Sample rate must be at least 8000 Hz.");
        }

        _ = Envelope.Validate();
        _ = Lfo.Validate();
        _ = Filter.Validate();
        return this;
    }
}
