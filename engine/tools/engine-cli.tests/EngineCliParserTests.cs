using Engine.Cli;
using Engine.Rendering;

namespace Engine.Cli.Tests;

public sealed class EngineCliParserTests
{
    [Fact]
    public void Parse_ShouldFail_WhenNoCommandProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Command is required. Available commands: new, init, build, run, bake, preview, preview audio, preview dump, test, pack, doctor, api dump.",
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
    public void Parse_ShouldUseDefaultPreviewAudioOptions_WhenOutNotProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["preview", "audio", "--project", "game"]);

        PreviewAudioCommand command = Assert.IsType<PreviewAudioCommand>(result.Command);
        Assert.Equal("assets/manifest.json", command.ManifestPath);
        Assert.Equal(Path.Combine("game", "artifacts", "preview-audio"), command.OutputDirectory);
    }

    [Fact]
    public void Parse_ShouldFail_WhenPreviewAudioProjectMissing()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["preview", "audio", "--manifest", "assets/manifest.json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--project' is required for 'preview audio'.", result.Error);
    }

    [Fact]
    public void Parse_ShouldUseDefaultTestOptions_WhenOutNotProvided()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["test", "--project", "game"]);

        TestCommand command = Assert.IsType<TestCommand>(result.Command);
        Assert.Equal(Path.Combine("game", "artifacts", "tests"), command.ArtifactsDirectory);
        Assert.Equal("Debug", command.Configuration);
        Assert.Equal(TestHostMode.HeadlessOffscreen, command.HostMode);
        Assert.Null(command.GoldenDirectory);
        Assert.False(command.PixelPerfectGolden);
        Assert.Equal(1, command.CaptureFrame);
        Assert.Equal(1337UL, command.ReplaySeed);
        Assert.Equal(1.0 / 60.0, command.FixedDeltaSeconds, 6);
        Assert.Equal(1.0, command.TolerantMaxMae, 6);
        Assert.Equal(48.0, command.TolerantMinPsnrDb, 6);
        Assert.Null(command.ReplayPath);
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
            "--host", "hidden-window",
            "--capture-frame", "12",
            "--seed", "9001",
            "--fixed-dt", "0.0333333",
            "--mae-threshold", "0.25",
            "--psnr-threshold", "52.5",
            "--replay", "artifacts/replay/recording.json"
        ]);

        TestCommand command = Assert.IsType<TestCommand>(result.Command);
        Assert.Equal(TestHostMode.HiddenWindow, command.HostMode);
        Assert.Equal(12, command.CaptureFrame);
        Assert.Equal(9001UL, command.ReplaySeed);
        Assert.Equal(0.0333333, command.FixedDeltaSeconds, 6);
        Assert.Equal(0.25, command.TolerantMaxMae, 6);
        Assert.Equal(52.5, command.TolerantMinPsnrDb, 6);
        Assert.Equal("artifacts/replay/recording.json", command.ReplayPath);
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

        EngineCliParseResult invalidMae = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--mae-threshold", "0"
        ]);
        Assert.False(invalidMae.IsSuccess);
        Assert.Equal("Option '--mae-threshold' must be a positive number.", invalidMae.Error);

        EngineCliParseResult invalidPsnr = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--psnr-threshold", "-5"
        ]);
        Assert.False(invalidPsnr.IsSuccess);
        Assert.Equal("Option '--psnr-threshold' must be a positive number.", invalidPsnr.Error);

        EngineCliParseResult invalidHost = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--host", "windowed"
        ]);
        Assert.False(invalidHost.IsSuccess);
        Assert.Equal(
            "Option '--host' must be one of: headless, offscreen, headless-offscreen, hidden, hidden-window.",
            invalidHost.Error);

        EngineCliParseResult invalidReplayPath = EngineCliParser.Parse(
        [
            "test",
            "--project", "game",
            "--replay", " "
        ]);
        Assert.False(invalidReplayPath.IsSuccess);
        Assert.Equal("Option '--replay' cannot be empty.", invalidReplayPath.Error);
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
        Assert.Null(command.RuntimePerfMetricsPath);
        Assert.Null(command.MaxAverageCaptureCpuMs);
        Assert.Null(command.MaxPeakCaptureAllocatedBytes);
        Assert.False(command.RequireZeroAllocationCapturePath);
        Assert.False(command.RequireRuntimeTransportSuccess);
        Assert.Null(command.MultiplayerDemoSummaryPath);
        Assert.False(command.VerifyMultiplayerSnapshotBinary);
        Assert.Null(command.MultiplayerSnapshotBinaryPath);
        Assert.False(command.VerifyMultiplayerRpcBinary);
        Assert.Null(command.MultiplayerRpcBinaryPath);
        Assert.False(command.VerifyCaptureRgba16FloatBinary);
        Assert.Null(command.CaptureRgba16FloatBinaryPath);
        Assert.False(command.VerifyRenderStatsArtifact);
        Assert.Null(command.RenderStatsArtifactPath);
        Assert.False(command.VerifyTestHostConfig);
        Assert.Null(command.TestHostConfigPath);
    }

    [Fact]
    public void Parse_ShouldCreateDoctorCommand_WithPerfOptions()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--runtime-perf", "artifacts/tests/runtime/perf-metrics.json",
            "--max-capture-cpu-ms", "2.5",
            "--max-capture-alloc-bytes", "1024",
            "--require-zero-alloc", "true",
            "--require-runtime-transport", "true",
            "--multiplayer-demo", "artifacts/tests/net/multiplayer-demo.json",
            "--verify-multiplayer-snapshot", "true",
            "--multiplayer-snapshot", "artifacts/tests/net/multiplayer-snapshot.bin",
            "--verify-multiplayer-rpc", "true",
            "--multiplayer-rpc", "artifacts/tests/net/multiplayer-rpc.bin",
            "--verify-capture-rgba16f", "true",
            "--capture-rgba16f", "artifacts/tests/screenshots/frame-0001.rgba16f.bin",
            "--verify-render-stats", "true",
            "--render-stats", "artifacts/tests/render/frame-stats.json",
            "--verify-test-host-config", "true",
            "--test-host-config", "artifacts/tests/runtime/test-host.json"
        ]);

        DoctorCommand command = Assert.IsType<DoctorCommand>(result.Command);
        Assert.Equal("game", command.ProjectDirectory);
        Assert.Equal("artifacts/tests/runtime/perf-metrics.json", command.RuntimePerfMetricsPath);
        Assert.Equal(2.5, command.MaxAverageCaptureCpuMs);
        Assert.Equal(1024L, command.MaxPeakCaptureAllocatedBytes);
        Assert.True(command.RequireZeroAllocationCapturePath);
        Assert.True(command.RequireRuntimeTransportSuccess);
        Assert.Equal("artifacts/tests/net/multiplayer-demo.json", command.MultiplayerDemoSummaryPath);
        Assert.True(command.VerifyMultiplayerSnapshotBinary);
        Assert.Equal("artifacts/tests/net/multiplayer-snapshot.bin", command.MultiplayerSnapshotBinaryPath);
        Assert.True(command.VerifyMultiplayerRpcBinary);
        Assert.Equal("artifacts/tests/net/multiplayer-rpc.bin", command.MultiplayerRpcBinaryPath);
        Assert.True(command.VerifyCaptureRgba16FloatBinary);
        Assert.Equal("artifacts/tests/screenshots/frame-0001.rgba16f.bin", command.CaptureRgba16FloatBinaryPath);
        Assert.True(command.VerifyRenderStatsArtifact);
        Assert.Equal("artifacts/tests/render/frame-stats.json", command.RenderStatsArtifactPath);
        Assert.True(command.VerifyTestHostConfig);
        Assert.Equal("artifacts/tests/runtime/test-host.json", command.TestHostConfigPath);
    }

    [Theory]
    [InlineData("--max-capture-cpu-ms", "0", "Option '--max-capture-cpu-ms' must be a positive number.")]
    [InlineData("--max-capture-cpu-ms", "abc", "Option '--max-capture-cpu-ms' must be a positive number.")]
    [InlineData("--max-capture-alloc-bytes", "-1", "Option '--max-capture-alloc-bytes' must be a non-negative integer.")]
    [InlineData("--max-capture-alloc-bytes", "1.25", "Option '--max-capture-alloc-bytes' must be a non-negative integer.")]
    [InlineData("--require-zero-alloc", "yes", "Option '--require-zero-alloc' must be 'true' or 'false'.")]
    [InlineData("--require-runtime-transport", "yes", "Option '--require-runtime-transport' must be 'true' or 'false'.")]
    [InlineData("--verify-multiplayer-snapshot", "yes", "Option '--verify-multiplayer-snapshot' must be 'true' or 'false'.")]
    [InlineData("--verify-multiplayer-rpc", "yes", "Option '--verify-multiplayer-rpc' must be 'true' or 'false'.")]
    [InlineData("--verify-capture-rgba16f", "yes", "Option '--verify-capture-rgba16f' must be 'true' or 'false'.")]
    [InlineData("--verify-render-stats", "yes", "Option '--verify-render-stats' must be 'true' or 'false'.")]
    [InlineData("--verify-test-host-config", "yes", "Option '--verify-test-host-config' must be 'true' or 'false'.")]
    public void Parse_ShouldFailDoctor_WhenPerfOptionValueInvalid(string optionName, string optionValue, string expectedError)
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            optionName, optionValue
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public void Parse_ShouldFailDoctor_WhenMultiplayerDemoPathEmpty()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--multiplayer-demo", " "
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--multiplayer-demo' cannot be empty.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFailDoctor_WhenMultiplayerSnapshotPathEmpty()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--multiplayer-snapshot", " "
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--multiplayer-snapshot' cannot be empty.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFailDoctor_WhenMultiplayerRpcPathEmpty()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--multiplayer-rpc", " "
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--multiplayer-rpc' cannot be empty.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFailDoctor_WhenCaptureRgba16FPathEmpty()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--capture-rgba16f", " "
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--capture-rgba16f' cannot be empty.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFailDoctor_WhenRenderStatsPathEmpty()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--render-stats", " "
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--render-stats' cannot be empty.", result.Error);
    }

    [Fact]
    public void Parse_ShouldFailDoctor_WhenTestHostConfigPathEmpty()
    {
        EngineCliParseResult result = EngineCliParser.Parse(
        [
            "doctor",
            "--project", "game",
            "--test-host-config", " "
        ]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--test-host-config' cannot be empty.", result.Error);
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

    [Fact]
    public void Parse_RunShouldDefaultDebugViewToNone()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["run", "--project", "."]);

        RunCommand command = Assert.IsType<RunCommand>(result.Command);
        Assert.Equal(RenderDebugViewMode.None, command.DebugViewMode);
    }

    [Theory]
    [InlineData("depth", RenderDebugViewMode.Depth)]
    [InlineData("normals", RenderDebugViewMode.Normals)]
    [InlineData("albedo", RenderDebugViewMode.Albedo)]
    [InlineData("roughness", RenderDebugViewMode.Roughness)]
    [InlineData("ao", RenderDebugViewMode.AmbientOcclusion)]
    [InlineData("none", RenderDebugViewMode.None)]
    public void Parse_RunShouldAcceptKnownDebugViews(string debugView, RenderDebugViewMode expected)
    {
        EngineCliParseResult result = EngineCliParser.Parse(["run", "--project", ".", "--debug-view", debugView]);

        RunCommand command = Assert.IsType<RunCommand>(result.Command);
        Assert.Equal(expected, command.DebugViewMode);
    }

    [Fact]
    public void Parse_ShouldFail_WhenDebugViewInvalid()
    {
        EngineCliParseResult result = EngineCliParser.Parse(["run", "--project", ".", "--debug-view", "ambient"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Option '--debug-view' must be one of: none, depth, normals, albedo, roughness, ao.", result.Error);
    }
}
