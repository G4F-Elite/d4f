using Engine.NativeBindings.Internal;
using Engine.NativeBindings.Internal.Interop;

namespace Engine.Tests.NativeBindings;

public sealed class NativeRuntimeAudioInteropTests
{
    [Fact]
    public void CreateSoundAndPlay_ShouldUseAudioInterop()
    {
        var backend = new FakeNativeInteropApi
        {
            AudioSoundHandleToReturn = 0x1_0000_2001UL,
            AudioEmitterIdToReturn = 0x1_0000_2002UL
        };
        using var runtime = new NativeRuntime(backend);

        ulong sound = runtime.CreateSoundFromBlob([1, 2, 3, 4]);
        Assert.Equal(backend.AudioSoundHandleToReturn, sound);
        Assert.Equal(1, backend.CountCall("audio_create_sound_from_blob"));

        var playDesc = new EngineNativeAudioPlayDesc
        {
            Volume = 0.75f,
            Pitch = 1.25f,
            Bus = (byte)EngineNativeAudioBus.Sfx,
            Loop = 1,
            IsSpatialized = 1,
            Position0 = 2f,
            Position1 = 3f,
            Position2 = 4f,
            Velocity0 = 0.1f,
            Velocity1 = 0.2f,
            Velocity2 = 0.3f
        };

        ulong emitter = runtime.PlaySound(sound, in playDesc);
        Assert.Equal(backend.AudioEmitterIdToReturn, emitter);
        Assert.Equal(sound, backend.LastAudioPlaySoundHandle);
        Assert.True(backend.LastAudioPlayDesc.HasValue);
        Assert.Equal(playDesc.Volume, backend.LastAudioPlayDesc.Value.Volume);
        Assert.Equal(playDesc.Pitch, backend.LastAudioPlayDesc.Value.Pitch);
        Assert.Equal(playDesc.Bus, backend.LastAudioPlayDesc.Value.Bus);
    }

    [Fact]
    public void SetAudioListenerAndEmitterParams_ShouldForwardToNative()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        var listener = new EngineNativeListenerDesc
        {
            Position0 = 10f,
            Position1 = 11f,
            Position2 = 12f,
            Forward0 = 0f,
            Forward1 = 0f,
            Forward2 = -1f,
            Up0 = 0f,
            Up1 = 1f,
            Up2 = 0f
        };
        runtime.SetAudioListener(in listener);
        Assert.Equal(1, backend.CountCall("audio_set_listener"));
        Assert.True(backend.LastAudioListenerDesc.HasValue);
        Assert.Equal(listener.Position0, backend.LastAudioListenerDesc.Value.Position0);
        Assert.Equal(listener.Up1, backend.LastAudioListenerDesc.Value.Up1);

        var emitterParams = new EngineNativeEmitterParams
        {
            Volume = 0.5f,
            Pitch = 1.1f,
            Position0 = -3f,
            Position1 = 0f,
            Position2 = 8f,
            Velocity0 = 0.4f,
            Velocity1 = 0.5f,
            Velocity2 = 0.6f,
            Lowpass = 0.8f,
            ReverbSend = 0.2f
        };
        runtime.SetAudioEmitterParams(17u, in emitterParams);
        Assert.Equal(1, backend.CountCall("audio_set_emitter_params"));
        Assert.Equal((ulong)17u, backend.LastAudioSetEmitterId);
        Assert.True(backend.LastAudioEmitterParams.HasValue);
        Assert.Equal(emitterParams.Lowpass, backend.LastAudioEmitterParams.Value.Lowpass);

        var busParams = new EngineNativeAudioBusParams
        {
            Bus = (byte)EngineNativeAudioBus.Sfx,
            Muted = 0,
            Gain = 0.6f,
            Lowpass = 0.7f,
            ReverbSend = 0.15f
        };
        runtime.SetAudioBusParams(in busParams);
        Assert.Equal(1, backend.CountCall("audio_set_bus_params"));
        Assert.True(backend.LastAudioBusParams.HasValue);
        Assert.Equal(busParams.Bus, backend.LastAudioBusParams.Value.Bus);
        Assert.Equal(busParams.Gain, backend.LastAudioBusParams.Value.Gain);
    }

    [Fact]
    public void AudioInterop_ShouldValidateManagedArguments()
    {
        var backend = new FakeNativeInteropApi();
        using var runtime = new NativeRuntime(backend);

        Assert.Throws<ArgumentException>(() => runtime.CreateSoundFromBlob([]));
        var playDesc = new EngineNativeAudioPlayDesc { Volume = 1f, Pitch = 1f };
        var emitterParams = new EngineNativeEmitterParams { Volume = 1f, Pitch = 1f };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            runtime.PlaySound(0u, in playDesc));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            runtime.SetAudioEmitterParams(0u, in emitterParams));
        Assert.Equal(0, backend.CountCall("audio_create_sound_from_blob"));
        Assert.Equal(0, backend.CountCall("audio_play"));
        Assert.Equal(0, backend.CountCall("audio_set_emitter_params"));
        Assert.Equal(0, backend.CountCall("audio_set_bus_params"));
    }

    [Fact]
    public void AudioInterop_ShouldThrowWhenNativeReturnsFailure()
    {
        var backend = new FakeNativeInteropApi
        {
            AudioCreateSoundFromBlobStatus = EngineNativeStatus.InvalidArgument
        };
        using var runtime = new NativeRuntime(backend);

        NativeCallException createEx = Assert.Throws<NativeCallException>(() => runtime.CreateSoundFromBlob([9]));
        Assert.Contains("audio_create_sound_from_blob", createEx.Message, StringComparison.Ordinal);

        backend.AudioCreateSoundFromBlobStatus = EngineNativeStatus.Ok;
        ulong sound = runtime.CreateSoundFromBlob([1, 2]);

        var playDesc = new EngineNativeAudioPlayDesc { Volume = 1f, Pitch = 1f };
        backend.AudioPlayStatus = EngineNativeStatus.InvalidState;
        NativeCallException playEx = Assert.Throws<NativeCallException>(() =>
            runtime.PlaySound(sound, in playDesc));
        Assert.Contains("audio_play", playEx.Message, StringComparison.Ordinal);

        backend.AudioPlayStatus = EngineNativeStatus.Ok;
        backend.AudioEmitterIdToReturn = 0u;
        InvalidOperationException invalidEmitterEx = Assert.Throws<InvalidOperationException>(() =>
            runtime.PlaySound(sound, in playDesc));
        Assert.Contains("invalid emitter", invalidEmitterEx.Message, StringComparison.OrdinalIgnoreCase);

        backend.AudioSetBusParamsStatus = EngineNativeStatus.InvalidArgument;
        var busParams = new EngineNativeAudioBusParams
        {
            Bus = (byte)EngineNativeAudioBus.Master,
            Gain = 1f,
            Lowpass = 1f,
            ReverbSend = 0f,
            Muted = 0
        };
        NativeCallException busEx = Assert.Throws<NativeCallException>(() => runtime.SetAudioBusParams(in busParams));
        Assert.Contains("audio_set_bus_params", busEx.Message, StringComparison.Ordinal);
    }
}
