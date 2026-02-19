namespace Engine.Audio;

public static class ProceduralSoundSynthesizer
{
    public static float[] GenerateMono(ProceduralSoundRecipe recipe, float durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _ = recipe.Validate();

        if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be greater than zero.");
        }

        int sampleCount = checked((int)MathF.Ceiling(durationSeconds * recipe.SampleRate));
        var samples = new float[sampleCount];

        float phase = 0f;
        float dt = 1f / recipe.SampleRate;
        float filterState = 0f;
        uint noiseState = recipe.Seed == 0u ? 0xA5A5A5A5u : recipe.Seed;

        float rc = 1f / (MathF.PI * 2f * recipe.Filter.CutoffHz);
        float alpha = dt / (rc + dt);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i * dt;
            float envelope = EvaluateEnvelope(recipe.Envelope, durationSeconds, t);
            float lfo = recipe.Lfo.Depth <= 0f || recipe.Lfo.FrequencyHz <= 0f
                ? 0f
                : MathF.Sin(MathF.PI * 2f * recipe.Lfo.FrequencyHz * t) * recipe.Lfo.Depth;
            float frequency = MathF.Max(1f, recipe.FrequencyHz * (1f + lfo));

            phase += frequency * dt;
            if (phase >= 1f)
            {
                phase -= MathF.Floor(phase);
            }

            float raw = recipe.Oscillator switch
            {
                OscillatorType.Sine => MathF.Sin(MathF.PI * 2f * phase),
                OscillatorType.Square => phase < 0.5f ? 1f : -1f,
                OscillatorType.Saw => phase * 2f - 1f,
                OscillatorType.Triangle => 1f - 4f * MathF.Abs(phase - 0.5f),
                OscillatorType.Noise => NextNoise(ref noiseState),
                _ => throw new InvalidDataException($"Unsupported oscillator value: {recipe.Oscillator}.")
            };

            float value = raw * recipe.Gain * envelope;
            filterState += alpha * (value - filterState);
            samples[i] = Math.Clamp(filterState, -1f, 1f);
        }

        return samples;
    }

    private static float EvaluateEnvelope(AdsrEnvelope envelope, float durationSeconds, float timeSeconds)
    {
        float sustainStart = envelope.AttackSeconds + envelope.DecaySeconds;
        float releaseStart = MathF.Max(sustainStart, durationSeconds - envelope.ReleaseSeconds);

        if (timeSeconds < envelope.AttackSeconds && envelope.AttackSeconds > 0f)
        {
            return timeSeconds / envelope.AttackSeconds;
        }

        if (timeSeconds < sustainStart && envelope.DecaySeconds > 0f)
        {
            float decayT = (timeSeconds - envelope.AttackSeconds) / envelope.DecaySeconds;
            return 1f - (1f - envelope.SustainLevel) * decayT;
        }

        if (timeSeconds < releaseStart)
        {
            return envelope.SustainLevel;
        }

        if (envelope.ReleaseSeconds <= 0f)
        {
            return 0f;
        }

        float releaseT = (timeSeconds - releaseStart) / envelope.ReleaseSeconds;
        return MathF.Max(0f, envelope.SustainLevel * (1f - releaseT));
    }

    private static float NextNoise(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state / (float)uint.MaxValue) * 2f - 1f;
    }
}
