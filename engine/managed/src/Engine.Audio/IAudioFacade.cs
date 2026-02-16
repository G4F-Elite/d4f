namespace Engine.Audio;

public interface IAudioFacade
{
    AudioEmitterHandle Play(ProceduralSoundRecipe recipe, AudioPlayRequest request);

    void Stop(AudioEmitterHandle emitter);

    void SetListener(in ListenerState listener);

    void SetEmitterParameters(AudioEmitterHandle emitter, in AudioEmitterParameters parameters);
}
