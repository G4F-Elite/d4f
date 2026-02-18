using Engine.Audio;
using Engine.NativeBindings;
using Xunit;

namespace Engine.Tests.NativeBindings;

public sealed class NativeFacadeFactoryAudioTests
{
    [Fact]
    public void CreateAudioFacade_ShouldPlayAndUpdateEmitter_WithStubBackend()
    {
        IAudioFacade audio = NativeFacadeFactory.CreateAudioFacade();
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 10u);
        AudioPlayRequest request = new(
            AudioBus.Sfx,
            Volume: 0.8f,
            Pitch: 1.0f,
            Loop: false,
            InitialEmitter: new AudioEmitterParameters(0.8f, 1.0f, 1f, 2f, 3f));

        AudioEmitterHandle emitter = audio.Play(recipe, request);
        Assert.True(emitter.IsValid);

        audio.SetListener(new ListenerState(10f, 20f, 30f));
        audio.SetEmitterParameters(
            emitter,
            new AudioEmitterParameters(Volume: 0.25f, Pitch: 1.1f, PositionX: 4f, PositionY: 5f, PositionZ: 6f));
        audio.Stop(emitter);

        Assert.Throws<KeyNotFoundException>(() => audio.Stop(emitter));
    }

    [Fact]
    public void NativeAudioFacade_ShouldUseNativeInterop_AndReuseSoundPerRecipe()
    {
        var backend = new FakeNativeInteropApi
        {
            AudioSoundHandleToReturn = 0xABCDUL,
            AudioEmitterIdToReturn = 0x1234UL
        };

        using var nativeSet = NativeFacadeFactory.CreateNativeFacadeSet(backend);
        ProceduralSoundRecipe recipe = CreateRecipe(seed: 42u);
        AudioPlayRequest request = new(
            AudioBus.Ambience,
            Volume: 0.6f,
            Pitch: 0.9f,
            Loop: true,
            InitialEmitter: new AudioEmitterParameters(0.6f, 0.9f, 7f, 8f, 9f));

        AudioEmitterHandle first = nativeSet.Audio.Play(recipe, request);
        AudioEmitterHandle second = nativeSet.Audio.Play(recipe, request);
        nativeSet.Audio.SetListener(new ListenerState(1f, 2f, 3f));
        nativeSet.Audio.SetEmitterParameters(
            first,
            new AudioEmitterParameters(Volume: 0.4f, Pitch: 1.05f, PositionX: -1f, PositionY: -2f, PositionZ: -3f));
        nativeSet.Audio.Stop(first);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Equal(1, backend.CountCall("audio_create_sound_from_blob"));
        Assert.Equal(2, backend.CountCall("audio_play"));
        Assert.Equal(1, backend.CountCall("audio_set_listener"));
        Assert.Equal(2, backend.CountCall("audio_set_emitter_params"));
        Assert.Equal(0xABCDUL, backend.LastAudioPlaySoundHandle);

        Assert.True(backend.LastAudioPlayDesc.HasValue);
        Assert.Equal((byte)Engine.NativeBindings.Internal.Interop.EngineNativeAudioBus.Ambience, backend.LastAudioPlayDesc.Value.Bus);
        Assert.Equal((byte)1, backend.LastAudioPlayDesc.Value.Loop);
        Assert.Equal((byte)1, backend.LastAudioPlayDesc.Value.IsSpatialized);
        Assert.Equal(7f, backend.LastAudioPlayDesc.Value.Position0);
        Assert.Equal(8f, backend.LastAudioPlayDesc.Value.Position1);
        Assert.Equal(9f, backend.LastAudioPlayDesc.Value.Position2);

        Assert.True(backend.LastAudioListenerDesc.HasValue);
        Assert.Equal(1f, backend.LastAudioListenerDesc.Value.Position0);
        Assert.Equal(2f, backend.LastAudioListenerDesc.Value.Position1);
        Assert.Equal(3f, backend.LastAudioListenerDesc.Value.Position2);
        Assert.Equal(0f, backend.LastAudioListenerDesc.Value.Forward0);
        Assert.Equal(0f, backend.LastAudioListenerDesc.Value.Forward1);
        Assert.Equal(-1f, backend.LastAudioListenerDesc.Value.Forward2);

        Assert.True(backend.LastAudioEmitterParams.HasValue);
        Assert.Equal(0f, backend.LastAudioEmitterParams.Value.Volume);
        Assert.Equal(1f, backend.LastAudioEmitterParams.Value.Pitch);
        Assert.Equal(0x1234UL, backend.LastAudioSetEmitterId);
    }

    private static ProceduralSoundRecipe CreateRecipe(uint seed)
    {
        return new ProceduralSoundRecipe(
            Oscillator: OscillatorType.Sine,
            FrequencyHz: 220f,
            Gain: 0.7f,
            SampleRate: 22050,
            Seed: seed,
            Envelope: new AdsrEnvelope(AttackSeconds: 0.01f, DecaySeconds: 0.02f, SustainLevel: 0.8f, ReleaseSeconds: 0.05f),
            Lfo: new LfoSettings(FrequencyHz: 3f, Depth: 0.02f),
            Filter: new OnePoleLowPassFilter(CutoffHz: 6000f));
    }
}
