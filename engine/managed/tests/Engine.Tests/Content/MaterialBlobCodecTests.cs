using Engine.Content;

namespace Engine.Tests.Content;

public sealed class MaterialBlobCodecTests
{
    [Fact]
    public void WriteRead_ShouldRoundTripMaterialBlob()
    {
        var source = new MaterialBlobData(
            TemplateId: "DffLitPbr",
            ParameterBlock: [1, 2, 3, 4],
            TextureReferences:
            [
                new MaterialTextureReference("albedo", "tex/albedo", 11u),
                new MaterialTextureReference("normal", "tex/normal", 12u)
            ]);

        byte[] bytes = MaterialBlobCodec.Write(source);
        MaterialBlobData decoded = MaterialBlobCodec.Read(bytes);

        Assert.Equal(MaterialBlobCodec.Magic, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(MaterialBlobCodec.Version, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal("DffLitPbr", decoded.TemplateId);
        Assert.Equal([1, 2, 3, 4], decoded.ParameterBlock);
        Assert.Equal(2, decoded.TextureReferences.Count);
        Assert.Equal("albedo", decoded.TextureReferences[0].Slot);
        Assert.Equal("tex/albedo", decoded.TextureReferences[0].AssetReference);
        Assert.Equal(11u, decoded.TextureReferences[0].RuntimeTextureHandle);
    }

    [Fact]
    public void Write_ShouldFail_WhenTextureSlotsDuplicate()
    {
        var source = new MaterialBlobData(
            TemplateId: "DffLitPbr",
            ParameterBlock: Array.Empty<byte>(),
            TextureReferences:
            [
                new MaterialTextureReference("albedo", "tex/a"),
                new MaterialTextureReference("albedo", "tex/b")
            ]);

        Assert.Throws<InvalidDataException>(() => MaterialBlobCodec.Write(source));
    }
}
