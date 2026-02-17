using Engine.Content;

namespace Engine.Tests.Content;

public sealed class SoundBlobCodecTests
{
    [Fact]
    public void WriteRead_ShouldRoundTripPcmSoundBlob()
    {
        byte[] pcm = SoundBlobCodec.EncodeMonoFloat32([0f, 0.25f, -0.5f, 1f]);
        var source = new SoundBlobData(
            SampleRate: 44100,
            Channels: 1,
            Encoding: SoundBlobEncoding.PcmFloat32Interleaved,
            Data: pcm,
            LoopStartSample: 1,
            LoopEndSample: 3);

        byte[] bytes = SoundBlobCodec.Write(source);
        SoundBlobData decoded = SoundBlobCodec.Read(bytes);

        Assert.Equal(SoundBlobCodec.Magic, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(SoundBlobCodec.Version, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(44100, decoded.SampleRate);
        Assert.Equal(1, decoded.Channels);
        Assert.Equal(SoundBlobEncoding.PcmFloat32Interleaved, decoded.Encoding);
        Assert.Equal(1, decoded.LoopStartSample);
        Assert.Equal(3, decoded.LoopEndSample);
        Assert.Equal(pcm, decoded.Data);
    }

    [Fact]
    public void EncodeMonoFloat32_ShouldFail_WhenSourceEmpty()
    {
        Assert.Throws<InvalidDataException>(() => SoundBlobCodec.EncodeMonoFloat32(ReadOnlySpan<float>.Empty));
    }

    [Fact]
    public void Write_ShouldFail_WhenLoopRangeInvalid()
    {
        byte[] pcm = SoundBlobCodec.EncodeMonoFloat32([0f, 0f, 0f]);
        var invalid = new SoundBlobData(
            SampleRate: 48000,
            Channels: 1,
            Encoding: SoundBlobEncoding.PcmFloat32Interleaved,
            Data: pcm,
            LoopStartSample: 2,
            LoopEndSample: 2);

        Assert.Throws<InvalidDataException>(() => SoundBlobCodec.Write(invalid));
    }
}
