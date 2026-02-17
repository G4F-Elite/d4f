using Engine.Procedural;

namespace Engine.Tests.Procedural;

public sealed class LevelChunkTagTests
{
    [Theory]
    [InlineData("chunk/room/v0", LevelNodeType.Room, "room", 0)]
    [InlineData("chunk/corridor/v1", LevelNodeType.Corridor, "corridor", 1)]
    [InlineData("chunk/junction/v2", LevelNodeType.Junction, "junction", 2)]
    [InlineData("chunk/deadend/v3", LevelNodeType.DeadEnd, "deadend", 3)]
    [InlineData("CHUNK/SHAFT/V0", LevelNodeType.Shaft, "shaft", 0)]
    public void Parse_ShouldExtractTypeAndVariant(
        string meshTag,
        LevelNodeType expectedType,
        string expectedTypeTag,
        int expectedVariant)
    {
        LevelChunkTag parsed = LevelChunkTag.Parse(meshTag);

        Assert.Equal(expectedType, parsed.NodeType);
        Assert.Equal(expectedTypeTag, parsed.TypeTag);
        Assert.Equal(expectedVariant, parsed.Variant);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Parse_ShouldRejectEmptyTag(string meshTag)
    {
        Assert.Throws<ArgumentException>(() => LevelChunkTag.Parse(meshTag));
    }

    [Theory]
    [InlineData("room/v0")]
    [InlineData("chunk/room")]
    [InlineData("chunk/unknown/v0")]
    [InlineData("chunk/room/v4")]
    [InlineData("chunk/room/v-1")]
    [InlineData("chunk/room/vx")]
    public void Parse_ShouldRejectInvalidFormat(string meshTag)
    {
        Assert.Throws<InvalidDataException>(() => LevelChunkTag.Parse(meshTag));
    }
}
