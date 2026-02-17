using Engine.Content;

namespace Engine.Audio;

public static class ProceduralSoundBlobBuilder
{
    public static byte[] BuildMonoPcmBlob(
        ProceduralSoundRecipe recipe,
        float durationSeconds,
        bool loop = false)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _ = recipe.Validate();

        float[] samples = ProceduralSoundSynthesizer.GenerateMono(recipe, durationSeconds);
        byte[] pcmPayload = SoundBlobCodec.EncodeMonoFloat32(samples);
        int loopStartSample = loop ? 0 : -1;
        int loopEndSample = loop ? samples.Length : -1;

        var blobData = new SoundBlobData(
            recipe.SampleRate,
            Channels: 1,
            SoundBlobEncoding.PcmFloat32Interleaved,
            pcmPayload,
            loopStartSample,
            loopEndSample);
        return SoundBlobCodec.Write(blobData);
    }
}
