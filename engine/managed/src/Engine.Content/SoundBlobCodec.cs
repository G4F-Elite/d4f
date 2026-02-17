using System.Buffers.Binary;
using System.Text;

namespace Engine.Content;

public enum SoundBlobEncoding : uint
{
    PcmFloat32Interleaved = 1u,
    SourceEncoded = 100u
}

public sealed record SoundBlobData(
    int SampleRate,
    int Channels,
    SoundBlobEncoding Encoding,
    byte[] Data,
    int LoopStartSample = -1,
    int LoopEndSample = -1)
{
    public SoundBlobData Validate()
    {
        if (SampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SampleRate), "Sound sample rate must be greater than zero.");
        }

        if (Channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Channels), "Sound channel count must be greater than zero.");
        }

        if (!Enum.IsDefined(Encoding))
        {
            throw new InvalidDataException($"Unsupported sound encoding '{Encoding}'.");
        }

        ArgumentNullException.ThrowIfNull(Data);
        if (Data.Length == 0)
        {
            throw new InvalidDataException("Sound payload cannot be empty.");
        }

        if ((LoopStartSample >= 0 && LoopEndSample < 0) || (LoopStartSample < 0 && LoopEndSample >= 0))
        {
            throw new InvalidDataException("Sound loop points must be both set or both unset.");
        }

        if (LoopStartSample >= 0)
        {
            if (LoopEndSample <= LoopStartSample)
            {
                throw new InvalidDataException("Sound loop end sample must be greater than loop start sample.");
            }

            if (Encoding == SoundBlobEncoding.PcmFloat32Interleaved)
            {
                int totalSamples = checked(Data.Length / sizeof(float) / Channels);
                if (LoopEndSample > totalSamples)
                {
                    throw new InvalidDataException(
                        $"Sound loop end sample {LoopEndSample} exceeds total sample count {totalSamples}.");
                }
            }
        }

        if (Encoding == SoundBlobEncoding.PcmFloat32Interleaved && (Data.Length % (sizeof(float) * Channels)) != 0)
        {
            throw new InvalidDataException("PCM float32 interleaved payload length is not aligned to channel/sample size.");
        }

        return this;
    }
}

public static class SoundBlobCodec
{
    public const uint Magic = 0x424E5344; // DSNB
    public const uint Version = 1u;

    public static byte[] Write(SoundBlobData blobData)
    {
        ArgumentNullException.ThrowIfNull(blobData);
        blobData = blobData.Validate();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(blobData.SampleRate);
            writer.Write(blobData.Channels);
            writer.Write((uint)blobData.Encoding);
            writer.Write(blobData.LoopStartSample);
            writer.Write(blobData.LoopEndSample);
            writer.Write(blobData.Data.Length);
            writer.Write(blobData.Data);
        }

        return stream.ToArray();
    }

    public static SoundBlobData Read(ReadOnlySpan<byte> blob)
    {
        if (blob.IsEmpty)
        {
            throw new InvalidDataException("Sound blob payload cannot be empty.");
        }

        using var stream = new MemoryStream(blob.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid sound blob magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported sound blob version {version}. Expected {Version}.");
        }

        int sampleRate = reader.ReadInt32();
        int channels = reader.ReadInt32();
        SoundBlobEncoding encoding = checked((SoundBlobEncoding)reader.ReadUInt32());
        int loopStartSample = reader.ReadInt32();
        int loopEndSample = reader.ReadInt32();
        int payloadLength = reader.ReadInt32();
        if (payloadLength <= 0)
        {
            throw new InvalidDataException("Sound payload length must be greater than zero.");
        }

        byte[] payload = reader.ReadBytes(payloadLength);
        if (payload.Length != payloadLength)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading sound payload.");
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Sound blob contains trailing bytes.");
        }

        return new SoundBlobData(
            sampleRate,
            channels,
            encoding,
            payload,
            loopStartSample,
            loopEndSample).Validate();
    }

    public static byte[] EncodeMonoFloat32(ReadOnlySpan<float> monoSamples)
    {
        if (monoSamples.IsEmpty)
        {
            throw new InvalidDataException("PCM sample buffer cannot be empty.");
        }

        var bytes = new byte[checked(monoSamples.Length * sizeof(float))];
        for (int i = 0; i < monoSamples.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(
                bytes.AsSpan(checked(i * sizeof(float)), sizeof(float)),
                monoSamples[i]);
        }

        return bytes;
    }
}
