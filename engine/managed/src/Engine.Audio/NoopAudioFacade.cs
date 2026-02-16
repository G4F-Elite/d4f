namespace Engine.Audio;

public sealed class NoopAudioFacade : IAudioFacade
{
    private readonly Dictionary<AudioEmitterHandle, AudioEmitterParameters> _emitters = new();
    private ulong _nextEmitterId = 1u;

    public AudioEmitterHandle Play(ProceduralSoundRecipe recipe, AudioPlayRequest request)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(request);

        _ = recipe.Validate();
        _ = request.Validate();

        AudioEmitterHandle handle = new(_nextEmitterId);
        _nextEmitterId = checked(_nextEmitterId + 1u);
        _emitters[handle] = request.InitialEmitter ?? new AudioEmitterParameters(request.Volume, request.Pitch, 0f, 0f, 0f);
        return handle;
    }

    public void Stop(AudioEmitterHandle emitter)
    {
        if (!emitter.IsValid)
        {
            throw new ArgumentException("Emitter handle is invalid.", nameof(emitter));
        }

        _emitters.Remove(emitter);
    }

    public void SetListener(in ListenerState listener)
    {
        _ = listener;
    }

    public void SetEmitterParameters(AudioEmitterHandle emitter, in AudioEmitterParameters parameters)
    {
        if (!emitter.IsValid)
        {
            throw new ArgumentException("Emitter handle is invalid.", nameof(emitter));
        }

        if (!_emitters.ContainsKey(emitter))
        {
            throw new KeyNotFoundException($"Emitter '{emitter.Value}' is not active.");
        }

        _emitters[emitter] = parameters.Validate();
    }
}
