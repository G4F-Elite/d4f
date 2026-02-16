using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliParserTests
{
    [Fact]
    public void Parse_ShouldFail_WhenNoCommandProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse([]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Command is required. Available commands: init, build, run, pack.", result.Error);
    }

    [Fact]
    public void Parse_ShouldCreateInitCommand_WhenArgumentsValid()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["init", "--name", "Demo", "--output", "templates"]);

        InitCommand command = Assert.IsType<InitCommand>(result.Command);
        Assert.Equal("Demo", command.Name);
        Assert.Equal("templates", command.OutputDirectory);
    }

    [Fact]
    public void Parse_ShouldFail_WhenConfigurationIsInvalid()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["build", "--project", ".", "--configuration", "Prod"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--configuration' must be 'Debug' or 'Release'.", result.Error);
    }

    [Fact]
    public void Parse_ShouldUseDefaultPackOutput_WhenOutputNotProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["pack", "--project", "game", "--manifest", "assets/manifest.json"]);

        PackCommand command = Assert.IsType<PackCommand>(result.Command);
        Assert.Equal(Path.Combine("game", "dist", "content.pak"), command.OutputPakPath);
        Assert.Equal("Release", command.Configuration);
        Assert.Equal("win-x64", command.RuntimeIdentifier);
        Assert.Null(command.PublishProjectPath);
        Assert.Null(command.NativeLibraryPath);
        Assert.Null(command.ZipOutputPath);
    }

    [Fact]
    public void Parse_ShouldReadExtendedPackOptions_WhenProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "pack",
            "--project", "game",
            "--manifest", "assets/manifest.json",
            "--output", "dist/game.pak",
            "--configuration", "Debug",
            "--runtime", "linux-x64",
            "--publish-project", "src/MyGame.Runtime/MyGame.Runtime.csproj",
            "--native-lib", "native/dff_native.dll",
            "--zip", "dist/package.zip"
        ]);

        PackCommand command = Assert.IsType<PackCommand>(result.Command);
        Assert.Equal("dist/game.pak", command.OutputPakPath);
        Assert.Equal("Debug", command.Configuration);
        Assert.Equal("linux-x64", command.RuntimeIdentifier);
        Assert.Equal("src/MyGame.Runtime/MyGame.Runtime.csproj", command.PublishProjectPath);
        Assert.Equal("native/dff_native.dll", command.NativeLibraryPath);
        Assert.Equal("dist/package.zip", command.ZipOutputPath);
    }

    [Fact]
    public void Parse_ShouldFail_WhenOptionDuplicated()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["run", "--project", ".", "--project", "./other"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--project' is duplicated.", result.Error);
    }
}
