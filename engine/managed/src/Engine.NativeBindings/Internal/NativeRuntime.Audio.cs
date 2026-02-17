using Engine.NativeBindings.Internal.Interop;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    public ulong CreateSoundFromBlob(ReadOnlySpan<byte> blob)
    {
        return CreateResourceFromBlob(
            blob,
            _audio,
            _interop.AudioCreateSoundFromBlob,
            "audio_create_sound_from_blob");
    }

    public ulong PlaySound(ulong sound, in EngineNativeAudioPlayDesc playDesc)
    {
        ThrowIfDisposed();
        if (sound == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(sound), "Sound handle must be non-zero.");
        }

        NativeStatusGuard.ThrowIfFailed(
            _interop.AudioPlay(_audio, sound, in playDesc, out ulong emitterId),
            "audio_play");
        if (emitterId == 0u)
        {
            throw new InvalidOperationException("Native audio_play returned an invalid emitter identifier.");
        }

        return emitterId;
    }

    public void SetAudioListener(in EngineNativeListenerDesc listenerDesc)
    {
        ThrowIfDisposed();
        NativeStatusGuard.ThrowIfFailed(
            _interop.AudioSetListener(_audio, in listenerDesc),
            "audio_set_listener");
    }

    public void SetAudioEmitterParams(ulong emitterId, in EngineNativeEmitterParams emitterParams)
    {
        ThrowIfDisposed();
        if (emitterId == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(emitterId), "Emitter id must be non-zero.");
        }

        NativeStatusGuard.ThrowIfFailed(
            _interop.AudioSetEmitterParams(_audio, emitterId, in emitterParams),
            "audio_set_emitter_params");
    }
}
