using Assetc;

namespace Assetc.Tests;

public sealed class AssetcParserTests
{
    [Fact]
    public void Parse_ShouldFail_WhenNoCommandProvided()
    {
        AssetcParseResult result = AssetcParser.Parse([]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Command is required. Available commands: build, list.", result.Error);
    }

    [Fact]
    public void Parse_ShouldCreateBuildCommand_WhenArgumentsValid()
    {
        AssetcParseResult result = AssetcParser.Parse(["build", "--manifest", "assets/manifest.json", "--output", "content.pak"]);

        BuildAssetsCommand command = Assert.IsType<BuildAssetsCommand>(result.Command);
        Assert.Equal("assets/manifest.json", command.ManifestPath);
        Assert.Equal("content.pak", command.OutputPakPath);
    }

    [Fact]
    public void Parse_ShouldFail_WhenListCommandHasNoPak()
    {
        AssetcParseResult result = AssetcParser.Parse(["list"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--pak' is required for 'list'.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFail_WhenOptionMissingValue()
    {
        AssetcParseResult result = AssetcParser.Parse(["build", "--manifest"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--manifest' requires a value.", result.Error);
    }
}
