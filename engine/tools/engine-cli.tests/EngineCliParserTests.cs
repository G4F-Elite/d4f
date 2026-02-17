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
            "Command is required. Available commands: new, init, build, run, bake, preview, preview dump, test, pack, doctor, api dump.",
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
    public void Parse_ShouldCreateNewCommand_WhenNameProvidedAsPositionalArgument()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["new", "Demo", "--output", "templates"]);

        NewCommand command = Assert.IsType<NewCommand>(result.Command);
        Assert.Equal("Demo", command.Name);
        Assert.Equal("templates", command.OutputDirectory);
    }

    [Fact]
    public void Parse_ShouldFail_WhenNewNameProvidedAsPositionalAndOption()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["new", "Demo", "--name", "Other"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Project name cannot be provided both as positional argument and '--name'.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFail_WhenConfigurationIsInvalid()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["build", "--project", ".", "--configuration", "Prod"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--configuration' must be 'Debug' or 'Release'.", result.Error);
    }

    [Fact]
    public void Parse_ShouldAcceptShortConfigurationAlias_ForBuild()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["build", "--project", ".", "-c", "Release"]);

        BuildCommand command = Assert.IsType<BuildCommand>(result.Command);
        Assert.Equal("Release", command.Configuration);
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
        Assert.Null(command.GoldenDirectory);
        Assert.False(command.PixelPerfectGolden);
        Assert.Equal(1, command.CaptureFrame);
        Assert.Equal(1337UL, command.ReplaySeed);
        Assert.Equal(1.0 / 60.0, command.FixedDeltaSeconds, 6);
    }

    [Fact]
    public void Parse_ShouldReadGoldenAndComparison_ForTestCommand()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--golden", "goldens",
            "--comparison", "pixel"
        ]);

        TestCommand command = Assert.IsType<TestCommand>(result.Command);
        Assert.Equal("goldens", command.GoldenDirectory);
        Assert.True(command.PixelPerfectGolden);
    }

    [Fact]
    public void Parse_ShouldReadReplayAndCaptureOptions_ForTestCommand()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--capture-frame", "12",
            "--seed", "9001",
            "--fixed-dt", "0.0333333"
        ]);

        TestCommand command = Assert.IsType<TestCommand>(result.Command);
        Assert.Equal(12, command.CaptureFrame);
        Assert.Equal(9001UL, command.ReplaySeed);
        Assert.Equal(0.0333333, command.FixedDeltaSeconds, 6);
    }

    [Fact]
    public void Parse_ShouldFail_WhenReplayOrCaptureOptionsInvalid()
    {
        EngineCliParseResult invalidCapture = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--capture-frame", "0"
        ]);
        Assert.False(invalidCapture.IsSuccess);
        Assert.Equal("Option '--capture-frame' must be an integer in range [1..100000].", invalidCapture.Error);

        EngineCliParseResult invalidSeed = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--seed", "not-a-number"
        ]);
        Assert.False(invalidSeed.IsSuccess);
        Assert.Equal("Option '--seed' must be an unsigned integer.", invalidSeed.Error);

        EngineCliParseResult invalidFixedDt = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--fixed-dt", "-1"
        ]);
        Assert.False(invalidFixedDt.IsSuccess);
        Assert.Equal("Option '--fixed-dt' must be a positive number.", invalidFixedDt.Error);
    }

    [Fact]
    public void Parse_ShouldFail_WhenComparisonValueInvalid()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--comparison", "strict"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--comparison' must be 'pixel' or 'tolerant'.", result.Error);
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
    public void Parse_ShouldAcceptShortRuntimeAlias_ForPack()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "pack",
            "-p", "game",
            "-m", "assets/manifest.json",
            "-r", "linux-x64",
            "-o", "dist/game.pak"
        ]);

        PackCommand command = Assert.IsType<PackCommand>(result.Command);
        Assert.Equal("linux-x64", command.RuntimeIdentifier);
        Assert.Equal("dist/game.pak", command.OutputPakPath);
    }

    [Fact]
    public void Parse_ShouldFail_WhenPackRuntimeIsUnsupported()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "pack",
            "--project", "game",
            "--manifest", "assets/manifest.json",
            "--runtime", "osx-x64"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--runtime' must be one of: win-x64, linux-x64.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFail_WhenOptionDuplicated()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["run", "--project", ".", "--project", "./other"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--project' is duplicated.", result.Error);
    }
}
