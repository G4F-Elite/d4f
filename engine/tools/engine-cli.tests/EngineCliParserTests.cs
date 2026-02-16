using Engine.Cli;

namespace Engine.Cli.Tests;

public sealed class EngineCliParserTests
{
    [Fact]
    public void Parse_ShouldFail_WhenNoCommandProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Command is required. Available commands: new, init, build, run, bake, preview, test, pack, doctor, api dump.",
            result.Error);
    }

    [Fact]
    public void Parse_ShouldCreateNewCommand_WhenArgumentsValid()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["new", "--name", "Demo", "--output", "templates"]);

        NewCommand command = Assert.IsType<NewCommand>(result.Command);
        Assert.Equal("Demo", command.Name);
        Assert.Equal("templates", command.OutputDirectory);
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
    public void Parse_ShouldUseDefaultBakeOptions_WhenOutNotProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["bake", "--project", "game"]);

        BakeCommand command = Assert.IsType<BakeCommand>(result.Command);
        Assert.Equal("assets/manifest.json", command.ManifestPath);
        Assert.Equal(Path.Combine("game", "build", "content", "Game.pak"), command.OutputPakPath);
    }

    [Fact]
    public void Parse_ShouldUseDefaultPreviewOptions_WhenOutNotProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["preview", "--project", "game"]);

        PreviewCommand command = Assert.IsType<PreviewCommand>(result.Command);
        Assert.Equal("assets/manifest.json", command.ManifestPath);
        Assert.Equal(Path.Combine("game", "artifacts", "preview"), command.OutputDirectory);
    }

    [Fact]
    public void Parse_ShouldUseDefaultTestOptions_WhenOutNotProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["test", "--project", "game"]);

        TestCommand command = Assert.IsType<TestCommand>(result.Command);
        Assert.Equal(Path.Combine("game", "artifacts", "tests"), command.ArtifactsDirectory);
        Assert.Equal("Debug", command.Configuration);
    }

    [Fact]
    public void Parse_ShouldCreateDoctorCommand_WhenProjectProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["doctor", "--project", "game"]);

        DoctorCommand command = Assert.IsType<DoctorCommand>(result.Command);
        Assert.Equal("game", command.ProjectDirectory);
    }

    [Fact]
    public void Parse_ShouldCreateApiDumpCommand_WhenApiDumpUsed()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["api", "dump", "--header", "header.h", "--out", "api.json"]);

        ApiDumpCommand command = Assert.IsType<ApiDumpCommand>(result.Command);
        Assert.Equal("header.h", command.HeaderPath);
        Assert.Equal("api.json", command.OutputPath);
    }

    [Fact]
    public void Parse_ShouldCreateApiDumpCommand_WhenDumpAliasUsed()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["dump", "--header", "header.h", "--out", "api.json"]);

        ApiDumpCommand command = Assert.IsType<ApiDumpCommand>(result.Command);
        Assert.Equal("header.h", command.HeaderPath);
        Assert.Equal("api.json", command.OutputPath);
    }

    [Fact]
    public void Parse_ShouldUseOutAliasForPack_WhenProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "pack",
            "--project", "game",
            "--manifest", "assets/manifest.json",
            "--out", "dist/game.pak"
        ]);

        PackCommand command = Assert.IsType<PackCommand>(result.Command);
        Assert.Equal("dist/game.pak", command.OutputPakPath);
        Assert.Equal("Release", command.Configuration);
        Assert.Equal("win-x64", command.RuntimeIdentifier);
        Assert.Null(command.PublishProjectPath);
        Assert.Null(command.NativeLibraryPath);
        Assert.Null(command.ZipOutputPath);
    }

    [Fact]
    public void Parse_ShouldFail_WhenOutAndOutputUsedTogether()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "pack",
            "--project", "game",
            "--manifest", "assets/manifest.json",
            "--out", "dist/game.pak",
            "--output", "dist/content.pak"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Options '--out' and '--output' cannot be used together.", result.Error);
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
