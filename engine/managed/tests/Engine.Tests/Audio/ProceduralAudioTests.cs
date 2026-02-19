using System.IO;
using Engine.Audio;

namespace Engine.Tests.Audio;

public sealed class ProceduralAudioTests
{
    [Fact]
    public void GenerateMono_ShouldBeDeterministic_ForSameRecipe()
    {
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 123u, oscillator: OscillatorType.Noise);

        float[] first = ProceduralSoundSynthesizer.GenerateMono(recipe, durationSeconds: 0.5f);
        float[] second = ProceduralSoundSynthesizer.GenerateMono(recipe, durationSeconds: 0.5f);

        Assert.Equal(first.Length, second.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateMono_ShouldChangeOutput_WhenNoiseSeedChanges()
    {
        ProceduralSoundRecipe firstRecipe = CreateRecipe(seed: 1u, oscillator: OscillatorType.Noise);
        ProceduralSoundRecipe secondRecipe = CreateRecipe(seed: 2u, oscillator: OscillatorType.Noise);

        float[] first = ProceduralSoundSynthesizer.GenerateMono(firstRecipe, durationSeconds: 0.25f);
        float[] second = ProceduralSoundSynthesizer.GenerateMono(secondRecipe, durationSeconds: 0.25f);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GenerateMono_ShouldRespectEnvelope_WithReleaseToZero()
    {
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 42u, oscillator: OscillatorType.Sine) with
        {
            Envelope = new AdsrEnvelope(AttackSeconds: 0.02f, DecaySeconds: 0.02f, SustainLevel: 0.6f, ReleaseSeconds: 0.05f)
        };

        float[] samples = ProceduralSoundSynthesizer.GenerateMono(recipe, durationSeconds: 0.3f);

        Assert.True(MathF.Abs(samples[0]) < 0.05f);
        Assert.True(MathF.Abs(samples[^1]) < 0.05f);
    }

    [Fact]
    public void GenerateMono_ShouldFail_WhenDurationInvalid()
    {
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 5u, oscillator: OscillatorType.Sine);

        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralSoundSynthesizer.GenerateMono(recipe, durationSeconds: 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralSoundSynthesizer.GenerateMono(recipe, durationSeconds: float.NaN));
    }

    [Fact]
    public void RecipeValidation_ShouldFail_ForInvalidSampleRate()
    {
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 7u, oscillator: OscillatorType.Square) with
        {
            SampleRate = 1000
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => recipe.Validate());
    }

    [Fact]
    public void AudioValueValidation_ShouldFail_ForNonFiniteInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AudioBusParameters(AudioBus.Sfx, float.NaN, 1f, 0f, false).Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AudioEmitterParameters(float.PositiveInfinity, 1f, 0f, 0f, 0f).Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AudioPlayRequest(AudioBus.Music, 1f, float.NaN, false).Validate());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ListenerState(float.NaN, 0f, 0f).Validate());

        ProceduralSoundRecipe nonFiniteRecipe = CreateRecipe(seed: 13u, oscillator: OscillatorType.Sine) with
        {
            FrequencyHz = float.PositiveInfinity
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => nonFiniteRecipe.Validate());

        var nonFiniteLayer = new AmbienceLayer("wind", float.NaN, 0.5f, 0.5f);
        Assert.Throws<ArgumentOutOfRangeException>(() => nonFiniteLayer.Validate());
    }

    [Fact]
    public void AmbienceDirector_ShouldEmitDeterministicSequence_ForSameSeed()
    {
        AmbienceDirectorConfig config = CreateAmbienceConfig(seed: 1234u);
        AmbienceDirector first = new(config);
        AmbienceDirector second = new(config);

        IReadOnlyList<AmbienceEvent> firstEvents = first.AdvanceTo(500);
        IReadOnlyList<AmbienceEvent> secondEvents = second.AdvanceTo(500);

        Assert.Equal(firstEvents.Count, secondEvents.Count);
        for (int i = 0; i < firstEvents.Count; i++)
        {
            Assert.Equal(firstEvents[i], secondEvents[i]);
        }
    }

    [Fact]
    public void AmbienceDirector_ShouldProduceDifferentEvents_ForDifferentSeed()
    {
        AmbienceDirector first = new(CreateAmbienceConfig(seed: 1u));
        AmbienceDirector second = new(CreateAmbienceConfig(seed: 987654321u));

        IReadOnlyList<AmbienceEvent> firstEvents = first.AdvanceTo(500);
        IReadOnlyList<AmbienceEvent> secondEvents = second.AdvanceTo(500);

        Assert.NotEqual(firstEvents, secondEvents);
    }

    [Fact]
    public void AmbienceDirector_ShouldFail_WhenTickMovesBackwards()
    {
        AmbienceDirector director = new(CreateAmbienceConfig(seed: 5u));

        _ = director.AdvanceTo(100);

        Assert.Throws<InvalidDataException>(() => director.AdvanceTo(99));
    }

    [Fact]
    public void NoopAudioFacade_ShouldReturnHandles_AndAllowParameterUpdates()
    {
        NoopAudioFacade audio = new();
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 9u, oscillator: OscillatorType.Triangle);
        AudioPlayRequest request = new(AudioBus.Sfx, Volume: 0.8f, Pitch: 1f, Loop: false);

        AudioEmitterHandle first = audio.Play(recipe, request);
        AudioEmitterHandle second = audio.Play(recipe, request);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.NotEqual(first, second);

        audio.SetListener(new ListenerState(0f, 1f, 2f));
        audio.SetEmitterParameters(second, new AudioEmitterParameters(Volume: 0.5f, Pitch: 1.2f, PositionX: 1f, PositionY: 2f, PositionZ: 3f));
        audio.Stop(first);
        audio.Stop(second);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            audio.SetListener(new ListenerState(float.NaN, 0f, 0f)));
    }

    private static ProceduralSoundRecipe CreateRecipe(uint seed, OscillatorType oscillator)
    {
        return new ProceduralSoundRecipe(
            Oscillator: oscillator,
            FrequencyHz: 220f,
            Gain: 0.8f,
            SampleRate: 22050,
            Seed: seed,
            Envelope: new AdsrEnvelope(AttackSeconds: 0.01f, DecaySeconds: 0.03f, SustainLevel: 0.7f, ReleaseSeconds: 0.05f),
            Lfo: new LfoSettings(FrequencyHz: 4f, Depth: 0.1f),
            Filter: new OnePoleLowPassFilter(CutoffHz: 8000f));
    }

    private static AmbienceDirectorConfig CreateAmbienceConfig(ulong seed)
    {
        return new AmbienceDirectorConfig(
            Seed: seed,
            TickRateHz: 30,
            Layers:
            [
                new AmbienceLayer("wind", AverageIntervalSeconds: 0.8f, TriggerProbability: 0.8f, Gain: 0.5f),
                new AmbienceLayer("metal_creak", AverageIntervalSeconds: 1.2f, TriggerProbability: 0.4f, Gain: 0.4f)
            ]);
    }
}
