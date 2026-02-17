using Engine.Audio;
using Engine.Content;

namespace Engine.Tests.Audio;

public sealed class ProceduralSoundBlobBuilderTests
{
    [Fact]
    public void BuildMonoPcmBlob_ShouldCreateValidSoundBlob()
    {
        ProceduralSoundRecipe recipe = new(
            Oscillator: OscillatorType.Sine,
            FrequencyHz: 220f,
            Gain: 0.5f,
            SampleRate: 22050,
            Seed: 42u,
            Envelope: new AdsrEnvelope(0.01f, 0.02f, 0.8f, 0.03f),
            Lfo: new LfoSettings(3f, 0.05f),
            Filter: new OnePoleLowPassFilter(6000f));

        byte[] blob = ProceduralSoundBlobBuilder.BuildMonoPcmBlob(recipe, durationSeconds: 0.1f, loop: true);
        SoundBlobData decoded = SoundBlobCodec.Read(blob);

        Assert.Equal(SoundBlobEncoding.PcmFloat32Interleaved, decoded.Encoding);
        Assert.Equal(recipe.SampleRate, decoded.SampleRate);
        Assert.Equal(1, decoded.Channels);
        Assert.Equal(0, decoded.LoopStartSample);
        Assert.True(decoded.LoopEndSample > 0);
        Assert.NotEmpty(decoded.Data);
    }
}
