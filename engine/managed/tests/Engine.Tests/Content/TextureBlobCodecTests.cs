using Engine.Content;

namespace Engine.Tests.Content;

public sealed class TextureBlobCodecTests
{
    [Fact]
    public void WriteRead_ShouldRoundTripRgba8TextureWithMipChain()
    {
        var blob = new TextureBlobData(
            TextureBlobFormat.Rgba8Unorm,
            TextureBlobColorSpace.Srgb,
            Width: 2,
            Height: 2,
            MipChain:
            [
                new TextureBlobMip(2, 2, 8, new byte[16]),
                new TextureBlobMip(1, 1, 4, new byte[4])
            ]);

        byte[] bytes = TextureBlobCodec.Write(blob);
        TextureBlobData decoded = TextureBlobCodec.Read(bytes);

        Assert.Equal(TextureBlobCodec.Magic, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(TextureBlobCodec.Version, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(TextureBlobFormat.Rgba8Unorm, decoded.Format);
        Assert.Equal(TextureBlobColorSpace.Srgb, decoded.ColorSpace);
        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal(2, decoded.MipChain.Count);
        Assert.Equal(16, decoded.MipChain[0].Data.Length);
        Assert.Equal(4, decoded.MipChain[1].Data.Length);
    }

    [Fact]
    public void Write_ShouldFail_WhenRgba8MipRowPitchInvalid()
    {
        var blob = new TextureBlobData(
            TextureBlobFormat.Rgba8Unorm,
            TextureBlobColorSpace.Linear,
            Width: 2,
            Height: 2,
            MipChain: [new TextureBlobMip(2, 2, 4, new byte[16])]);

        Assert.Throws<InvalidDataException>(() => TextureBlobCodec.Write(blob));
    }

    [Fact]
    public void Write_ShouldFail_WhenSourceTextureHasMultipleMips()
    {
        var blob = new TextureBlobData(
            TextureBlobFormat.SourcePng,
            TextureBlobColorSpace.Srgb,
            Width: 1,
            Height: 1,
            MipChain:
            [
                new TextureBlobMip(1, 1, 0, [1]),
                new TextureBlobMip(1, 1, 0, [1])
            ]);

        Assert.Throws<InvalidDataException>(() => TextureBlobCodec.Write(blob));
    }
}
