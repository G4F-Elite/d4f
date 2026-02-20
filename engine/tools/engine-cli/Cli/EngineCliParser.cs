using Engine.Rendering;
using System.Globalization;

namespace Engine.Cli;

public static partial class EngineCliParser
{
    private const string AvailableCommandsText = "new, init, build, update, run, bake, preview, preview audio, preview dump, test, multiplayer demo, multiplayer orchestrate, nfr proof, pack, doctor, api dump.";
    private static readonly HashSet<string> ValidConfigurations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Debug",
        "Release"
    };
    private static readonly HashSet<string> ValidPackRuntimeIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "win-x64",
        "linux-x64"
    };
    private static readonly Dictionary<string, RenderDebugViewMode> ValidDebugViewModes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = RenderDebugViewMode.None,
        ["depth"] = RenderDebugViewMode.Depth,
        ["normals"] = RenderDebugViewMode.Normals,
        ["albedo"] = RenderDebugViewMode.Albedo,
        ["roughness"] = RenderDebugViewMode.Roughness,
        ["shadow"] = RenderDebugViewMode.AmbientOcclusion,
        ["ao"] = RenderDebugViewMode.AmbientOcclusion
    };
    private const string DebugViewOptionError = "Option '--debug-view' must be one of: none, depth, normals, albedo, roughness, shadow, ao.";

    public static EngineCliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return EngineCliParseResult.Failure($"Command is required. Available commands: {AvailableCommandsText}");
        }

        string commandName = args[0].ToLowerInvariant();
        if (string.Equals(commandName, "api", StringComparison.Ordinal))
        {
            return ParseApi(args);
        }

        if (string.Equals(commandName, "dump", StringComparison.Ordinal))
        {
            Dictionary<string, string> dumpOptions = ParseOptions(args[1..], out string? dumpError);
            if (dumpError is not null)
            {
                return EngineCliParseResult.Failure(dumpError);
            }

            return ParseDump(dumpOptions);
        }

        if (string.Equals(commandName, "preview", StringComparison.Ordinal) &&
            args.Length > 1 &&
            string.Equals(args[1], "dump", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, string> dumpOptions = ParseOptions(args[2..], out string? dumpError);
            if (dumpError is not null)
            {
                return EngineCliParseResult.Failure(dumpError);
            }

            return ParsePreviewDump(dumpOptions);
        }

        if (string.Equals(commandName, "preview", StringComparison.Ordinal) &&
            args.Length > 1 &&
            string.Equals(args[1], "audio", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, string> audioOptions = ParseOptions(args[2..], out string? audioError);
            if (audioError is not null)
            {
                return EngineCliParseResult.Failure(audioError);
            }

            return ParsePreviewAudio(audioOptions);
        }

        if (string.Equals(commandName, "multiplayer", StringComparison.Ordinal) &&
            args.Length > 1 &&
            string.Equals(args[1], "demo", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, string> multiplayerOptions = ParseOptions(args[2..], out string? multiplayerError);
            if (multiplayerError is not null)
            {
                return EngineCliParseResult.Failure(multiplayerError);
            }

            return ParseMultiplayerDemo(multiplayerOptions);
        }

        if (string.Equals(commandName, "multiplayer", StringComparison.Ordinal) &&
            args.Length > 1 &&
            string.Equals(args[1], "orchestrate", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, string> orchestrateOptions = ParseOptions(args[2..], out string? orchestrateError);
            if (orchestrateError is not null)
            {
                return EngineCliParseResult.Failure(orchestrateError);
            }

            return ParseMultiplayerOrchestration(orchestrateOptions);
        }

        if (string.Equals(commandName, "nfr", StringComparison.Ordinal) &&
            args.Length > 1 &&
            string.Equals(args[1], "proof", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, string> nfrOptions = ParseOptions(args[2..], out string? nfrError);
            if (nfrError is not null)
            {
                return EngineCliParseResult.Failure(nfrError);
            }

            return ParseNfrProof(nfrOptions);
        }

        int optionsStartIndex = 1;
        string? positionalProjectName = null;
        if ((string.Equals(commandName, "new", StringComparison.Ordinal) ||
             string.Equals(commandName, "init", StringComparison.Ordinal)) &&
            args.Length > 1 &&
            !args[1].StartsWith("-", StringComparison.Ordinal))
        {
            positionalProjectName = args[1];
            optionsStartIndex = 2;
        }

        Dictionary<string, string> optionsResult = ParseOptions(args[optionsStartIndex..], out string? parseError);
        if (parseError is not null)
        {
            return EngineCliParseResult.Failure(parseError);
        }
        if (positionalProjectName is not null)
        {
            if (optionsResult.ContainsKey("name"))
            {
                return EngineCliParseResult.Failure("Project name cannot be provided both as positional argument and '--name'.");
            }

            optionsResult["name"] = positionalProjectName;
        }

        return commandName switch
        {
            "new" => ParseNew(optionsResult),
            "init" => ParseInit(optionsResult),
            "build" => ParseBuild(optionsResult),
            "update" => ParseUpdate(optionsResult),
            "run" => ParseRun(optionsResult),
            "bake" => ParseBake(optionsResult),
            "preview" => ParsePreview(optionsResult),
            "test" => ParseTest(optionsResult),
            "pack" => ParsePack(optionsResult),
            "doctor" => ParseDoctor(optionsResult),
            _ => EngineCliParseResult.Failure($"Unknown command '{args[0]}'. Available commands: {AvailableCommandsText}")
        };
    }

    private static EngineCliParseResult ParseNew(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("name", out string? name))
        {
            return EngineCliParseResult.Failure("Option '--name' is required for 'new'.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return EngineCliParseResult.Failure("Option '--name' cannot be empty.");
        }

        string output = options.TryGetValue("output", out string? outputValue) ? outputValue : ".";
        return EngineCliParseResult.Success(new NewCommand(name, output));
    }

    private static EngineCliParseResult ParseInit(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("name", out string? name))
        {
            return EngineCliParseResult.Failure("Option '--name' is required for 'init'.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return EngineCliParseResult.Failure("Option '--name' cannot be empty.");
        }

        string output = options.TryGetValue("output", out string? outputValue) ? outputValue : ".";
        return EngineCliParseResult.Success(new InitCommand(name, output));
    }

    private static EngineCliParseResult ParseBuild(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'build'.");
        }

        string configuration = options.TryGetValue("configuration", out string? cfg) ? cfg : "Debug";
        if (!ValidConfigurations.Contains(configuration))
        {
            return EngineCliParseResult.Failure("Option '--configuration' must be 'Debug' or 'Release'.");
        }

        return EngineCliParseResult.Success(new BuildCommand(project, configuration));
    }

    private static EngineCliParseResult ParseUpdate(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'update'.");
        }

        string? engineManagedSourcePath = options.TryGetValue("engine-managed-src", out string? managedSourcePath)
            ? managedSourcePath
            : null;
        if (engineManagedSourcePath is not null && string.IsNullOrWhiteSpace(engineManagedSourcePath))
        {
            return EngineCliParseResult.Failure("Option '--engine-managed-src' cannot be empty.");
        }

        return EngineCliParseResult.Success(new UpdateCommand(project, engineManagedSourcePath));
    }

    private static EngineCliParseResult ParseRun(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'run'.");
        }

        string configuration = options.TryGetValue("configuration", out string? cfg) ? cfg : "Debug";
        if (!ValidConfigurations.Contains(configuration))
        {
            return EngineCliParseResult.Failure("Option '--configuration' must be 'Debug' or 'Release'.");
        }

        RenderDebugViewMode debugViewMode = RenderDebugViewMode.None;
        if (options.TryGetValue("debug-view", out string? debugViewValue))
        {
            if (string.IsNullOrWhiteSpace(debugViewValue))
            {
                return EngineCliParseResult.Failure("Option '--debug-view' cannot be empty.");
            }

            if (!ValidDebugViewModes.TryGetValue(debugViewValue, out debugViewMode))
            {
                return EngineCliParseResult.Failure(DebugViewOptionError);
            }
        }

        return EngineCliParseResult.Success(new RunCommand(project, configuration, debugViewMode));
    }

    private static EngineCliParseResult ParseBake(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'bake'.");
        }

        string manifest = options.TryGetValue("manifest", out string? manifestValue)
            ? manifestValue
            : "assets/manifest.json";
        string output = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "build", "content", "Game.pak"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        return EngineCliParseResult.Success(new BakeCommand(project, manifest, output));
    }

    private static EngineCliParseResult ParsePreview(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'preview'.");
        }

        string manifest = options.TryGetValue("manifest", out string? manifestValue)
            ? manifestValue
            : "assets/manifest.json";
        string output = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "artifacts", "preview"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        return EngineCliParseResult.Success(new PreviewCommand(project, manifest, output));
    }

    private static EngineCliParseResult ParsePreviewAudio(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'preview audio'.");
        }

        string manifest = options.TryGetValue("manifest", out string? manifestValue)
            ? manifestValue
            : "assets/manifest.json";
        string output = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "artifacts", "preview-audio"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        return EngineCliParseResult.Success(new PreviewAudioCommand(project, manifest, output));
    }

    private static EngineCliParseResult ParsePreviewDump(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'preview dump'.");
        }

        string manifest = options.TryGetValue("manifest", out string? manifestValue)
            ? manifestValue
            : Path.Combine(project, "artifacts", "preview", "manifest.json");
        if (string.IsNullOrWhiteSpace(manifest))
        {
            return EngineCliParseResult.Failure("Option '--manifest' cannot be empty.");
        }

        return EngineCliParseResult.Success(new PreviewDumpCommand(project, manifest));
    }

    private static EngineCliParseResult ParseMultiplayerDemo(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'multiplayer demo'.");
        }

        string outputDirectory = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "artifacts", "runtime-multiplayer"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        ulong seed = 1337UL;
        if (options.TryGetValue("seed", out string? seedValue))
        {
            if (!ulong.TryParse(seedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                return EngineCliParseResult.Failure("Option '--seed' must be an unsigned integer.");
            }
        }

        double fixedDeltaSeconds = 1.0 / 60.0;
        if (options.TryGetValue("fixed-dt", out string? fixedDtValue))
        {
            if (!double.TryParse(fixedDtValue, NumberStyles.Float, CultureInfo.InvariantCulture, out fixedDeltaSeconds) ||
                !double.IsFinite(fixedDeltaSeconds) ||
                fixedDeltaSeconds <= 0.0)
            {
                return EngineCliParseResult.Failure("Option '--fixed-dt' must be a positive number.");
            }
        }

        bool requireNativeTransportSuccess = false;
        if (options.TryGetValue("require-native-transport", out string? requireNativeTransportValue))
        {
            if (!bool.TryParse(requireNativeTransportValue, out requireNativeTransportSuccess))
            {
                return EngineCliParseResult.Failure("Option '--require-native-transport' must be 'true' or 'false'.");
            }
        }

        return EngineCliParseResult.Success(new MultiplayerDemoCommand(
            ProjectDirectory: project,
            OutputDirectory: outputDirectory,
            Seed: seed,
            FixedDeltaSeconds: fixedDeltaSeconds,
            RequireNativeTransportSuccess: requireNativeTransportSuccess));
    }

    private static EngineCliParseResult ParseMultiplayerOrchestration(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'multiplayer orchestrate'.");
        }

        string outputDirectory = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "artifacts", "runtime-multiplayer-orchestration"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        string configuration = options.TryGetValue("configuration", out string? cfg) ? cfg : "Release";
        if (!ValidConfigurations.Contains(configuration))
        {
            return EngineCliParseResult.Failure("Option '--configuration' must be 'Debug' or 'Release'.");
        }

        ulong seed = 1337UL;
        if (options.TryGetValue("seed", out string? seedValue))
        {
            if (!ulong.TryParse(seedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                return EngineCliParseResult.Failure("Option '--seed' must be an unsigned integer.");
            }
        }

        double fixedDeltaSeconds = 1.0 / 60.0;
        if (options.TryGetValue("fixed-dt", out string? fixedDtValue))
        {
            if (!double.TryParse(fixedDtValue, NumberStyles.Float, CultureInfo.InvariantCulture, out fixedDeltaSeconds) ||
                !double.IsFinite(fixedDeltaSeconds) ||
                fixedDeltaSeconds <= 0.0)
            {
                return EngineCliParseResult.Failure("Option '--fixed-dt' must be a positive number.");
            }
        }

        bool requireNativeTransportSuccess = true;
        if (options.TryGetValue("require-native-transport", out string? requireNativeTransportValue))
        {
            if (!bool.TryParse(requireNativeTransportValue, out requireNativeTransportSuccess))
            {
                return EngineCliParseResult.Failure("Option '--require-native-transport' must be 'true' or 'false'.");
            }
        }

        string cliProjectPath = options.TryGetValue("cli-project", out string? cliProjectPathValue)
            ? cliProjectPathValue
            : Path.Combine("engine", "tools", "engine-cli", "Engine.Cli.csproj");
        if (string.IsNullOrWhiteSpace(cliProjectPath))
        {
            return EngineCliParseResult.Failure("Option '--cli-project' cannot be empty.");
        }

        return EngineCliParseResult.Success(new MultiplayerOrchestrationCommand(
            ProjectDirectory: project,
            OutputDirectory: outputDirectory,
            Configuration: configuration,
            Seed: seed,
            FixedDeltaSeconds: fixedDeltaSeconds,
            RequireNativeTransportSuccess: requireNativeTransportSuccess,
            CliProjectPath: cliProjectPath));
    }

    private static EngineCliParseResult ParseNfrProof(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'nfr proof'.");
        }

        string outputPath = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine(project, "artifacts", "nfr", "release-proof.json"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        string configuration = options.TryGetValue("configuration", out string? cfg) ? cfg : "Release";
        if (!ValidConfigurations.Contains(configuration))
        {
            return EngineCliParseResult.Failure("Option '--configuration' must be 'Debug' or 'Release'.");
        }

        return EngineCliParseResult.Success(new NfrProofCommand(project, outputPath, configuration));
    }

    private static EngineCliParseResult ParseDoctor(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'doctor'.");
        }

        string? runtimePerfMetricsPath = options.TryGetValue("runtime-perf", out string? runtimePerfValue)
            ? runtimePerfValue
            : null;
        if (runtimePerfMetricsPath is not null && string.IsNullOrWhiteSpace(runtimePerfMetricsPath))
        {
            return EngineCliParseResult.Failure("Option '--runtime-perf' cannot be empty.");
        }

        double? maxAverageCaptureCpuMs = null;
        if (options.TryGetValue("max-capture-cpu-ms", out string? maxCpuValue))
        {
            if (!double.TryParse(maxCpuValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedCpuMs) ||
                !double.IsFinite(parsedCpuMs) ||
                parsedCpuMs <= 0.0)
            {
                return EngineCliParseResult.Failure("Option '--max-capture-cpu-ms' must be a positive number.");
            }

            maxAverageCaptureCpuMs = parsedCpuMs;
        }

        long? maxPeakCaptureAllocatedBytes = null;
        if (options.TryGetValue("max-capture-alloc-bytes", out string? maxAllocValue))
        {
            if (!long.TryParse(maxAllocValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedAllocBytes) ||
                parsedAllocBytes < 0L)
            {
                return EngineCliParseResult.Failure("Option '--max-capture-alloc-bytes' must be a non-negative integer.");
            }

            maxPeakCaptureAllocatedBytes = parsedAllocBytes;
        }

        bool requireZeroAllocationCapturePath = false;
        if (options.TryGetValue("require-zero-alloc", out string? requireZeroAllocValue))
        {
            if (!bool.TryParse(requireZeroAllocValue, out requireZeroAllocationCapturePath))
            {
                return EngineCliParseResult.Failure("Option '--require-zero-alloc' must be 'true' or 'false'.");
            }
        }

        bool requireRuntimeTransportSuccess = false;
        if (options.TryGetValue("require-runtime-transport", out string? requireRuntimeTransportValue))
        {
            if (!bool.TryParse(requireRuntimeTransportValue, out requireRuntimeTransportSuccess))
            {
                return EngineCliParseResult.Failure("Option '--require-runtime-transport' must be 'true' or 'false'.");
            }
        }

        string? multiplayerDemoSummaryPath = options.TryGetValue("multiplayer-demo", out string? multiplayerDemoPathValue)
            ? multiplayerDemoPathValue
            : null;
        if (multiplayerDemoSummaryPath is not null && string.IsNullOrWhiteSpace(multiplayerDemoSummaryPath))
        {
            return EngineCliParseResult.Failure("Option '--multiplayer-demo' cannot be empty.");
        }

        bool verifyMultiplayerSnapshotBinary = false;
        if (options.TryGetValue("verify-multiplayer-snapshot", out string? verifySnapshotValue))
        {
            if (!bool.TryParse(verifySnapshotValue, out verifyMultiplayerSnapshotBinary))
            {
                return EngineCliParseResult.Failure("Option '--verify-multiplayer-snapshot' must be 'true' or 'false'.");
            }
        }

        string? multiplayerSnapshotBinaryPath = options.TryGetValue("multiplayer-snapshot", out string? multiplayerSnapshotPathValue)
            ? multiplayerSnapshotPathValue
            : null;
        if (multiplayerSnapshotBinaryPath is not null && string.IsNullOrWhiteSpace(multiplayerSnapshotBinaryPath))
        {
            return EngineCliParseResult.Failure("Option '--multiplayer-snapshot' cannot be empty.");
        }

        bool verifyMultiplayerRpcBinary = false;
        if (options.TryGetValue("verify-multiplayer-rpc", out string? verifyRpcValue))
        {
            if (!bool.TryParse(verifyRpcValue, out verifyMultiplayerRpcBinary))
            {
                return EngineCliParseResult.Failure("Option '--verify-multiplayer-rpc' must be 'true' or 'false'.");
            }
        }

        string? multiplayerRpcBinaryPath = options.TryGetValue("multiplayer-rpc", out string? multiplayerRpcPathValue)
            ? multiplayerRpcPathValue
            : null;
        if (multiplayerRpcBinaryPath is not null && string.IsNullOrWhiteSpace(multiplayerRpcBinaryPath))
        {
            return EngineCliParseResult.Failure("Option '--multiplayer-rpc' cannot be empty.");
        }

        bool verifyMultiplayerOrchestration = false;
        if (options.TryGetValue("verify-multiplayer-orchestration", out string? verifyMultiplayerOrchestrationValue))
        {
            if (!bool.TryParse(verifyMultiplayerOrchestrationValue, out verifyMultiplayerOrchestration))
            {
                return EngineCliParseResult.Failure("Option '--verify-multiplayer-orchestration' must be 'true' or 'false'.");
            }
        }

        string? multiplayerOrchestrationPath = options.TryGetValue("multiplayer-orchestration", out string? multiplayerOrchestrationPathValue)
            ? multiplayerOrchestrationPathValue
            : null;
        if (multiplayerOrchestrationPath is not null && string.IsNullOrWhiteSpace(multiplayerOrchestrationPath))
        {
            return EngineCliParseResult.Failure("Option '--multiplayer-orchestration' cannot be empty.");
        }

        bool verifyCaptureRgba16FloatBinary = false;
        if (options.TryGetValue("verify-capture-rgba16f", out string? verifyCaptureRgba16FValue))
        {
            if (!bool.TryParse(verifyCaptureRgba16FValue, out verifyCaptureRgba16FloatBinary))
            {
                return EngineCliParseResult.Failure("Option '--verify-capture-rgba16f' must be 'true' or 'false'.");
            }
        }

        string? captureRgba16FloatBinaryPath = options.TryGetValue("capture-rgba16f", out string? captureRgba16FPathValue)
            ? captureRgba16FPathValue
            : null;
        if (captureRgba16FloatBinaryPath is not null && string.IsNullOrWhiteSpace(captureRgba16FloatBinaryPath))
        {
            return EngineCliParseResult.Failure("Option '--capture-rgba16f' cannot be empty.");
        }

        string? captureRgba16FloatExrPath = options.TryGetValue("capture-rgba16f-exr", out string? captureRgba16FExrPathValue)
            ? captureRgba16FExrPathValue
            : null;
        if (captureRgba16FloatExrPath is not null && string.IsNullOrWhiteSpace(captureRgba16FloatExrPath))
        {
            return EngineCliParseResult.Failure("Option '--capture-rgba16f-exr' cannot be empty.");
        }

        bool verifyRenderStatsArtifact = false;
        if (options.TryGetValue("verify-render-stats", out string? verifyRenderStatsValue))
        {
            if (!bool.TryParse(verifyRenderStatsValue, out verifyRenderStatsArtifact))
            {
                return EngineCliParseResult.Failure("Option '--verify-render-stats' must be 'true' or 'false'.");
            }
        }

        string? renderStatsArtifactPath = options.TryGetValue("render-stats", out string? renderStatsPathValue)
            ? renderStatsPathValue
            : null;
        if (renderStatsArtifactPath is not null && string.IsNullOrWhiteSpace(renderStatsArtifactPath))
        {
            return EngineCliParseResult.Failure("Option '--render-stats' cannot be empty.");
        }

        bool verifyTestHostConfig = false;
        if (options.TryGetValue("verify-test-host-config", out string? verifyTestHostConfigValue))
        {
            if (!bool.TryParse(verifyTestHostConfigValue, out verifyTestHostConfig))
            {
                return EngineCliParseResult.Failure("Option '--verify-test-host-config' must be 'true' or 'false'.");
            }
        }

        string? testHostConfigPath = options.TryGetValue("test-host-config", out string? testHostConfigPathValue)
            ? testHostConfigPathValue
            : null;
        if (testHostConfigPath is not null && string.IsNullOrWhiteSpace(testHostConfigPath))
        {
            return EngineCliParseResult.Failure("Option '--test-host-config' cannot be empty.");
        }

        bool verifyNetProfileLog = false;
        if (options.TryGetValue("verify-net-profile-log", out string? verifyNetProfileLogValue))
        {
            if (!bool.TryParse(verifyNetProfileLogValue, out verifyNetProfileLog))
            {
                return EngineCliParseResult.Failure("Option '--verify-net-profile-log' must be 'true' or 'false'.");
            }
        }

        string? netProfileLogPath = options.TryGetValue("net-profile-log", out string? netProfileLogPathValue)
            ? netProfileLogPathValue
            : null;
        if (netProfileLogPath is not null && string.IsNullOrWhiteSpace(netProfileLogPath))
        {
            return EngineCliParseResult.Failure("Option '--net-profile-log' cannot be empty.");
        }

        bool verifyReplayRecording = false;
        if (options.TryGetValue("verify-replay-recording", out string? verifyReplayRecordingValue))
        {
            if (!bool.TryParse(verifyReplayRecordingValue, out verifyReplayRecording))
            {
                return EngineCliParseResult.Failure("Option '--verify-replay-recording' must be 'true' or 'false'.");
            }
        }

        string? replayRecordingPath = options.TryGetValue("replay-recording", out string? replayRecordingPathValue)
            ? replayRecordingPathValue
            : null;
        if (replayRecordingPath is not null && string.IsNullOrWhiteSpace(replayRecordingPath))
        {
            return EngineCliParseResult.Failure("Option '--replay-recording' cannot be empty.");
        }

        bool verifyArtifactsManifest = false;
        if (options.TryGetValue("verify-artifacts-manifest", out string? verifyArtifactsManifestValue))
        {
            if (!bool.TryParse(verifyArtifactsManifestValue, out verifyArtifactsManifest))
            {
                return EngineCliParseResult.Failure("Option '--verify-artifacts-manifest' must be 'true' or 'false'.");
            }
        }

        string? artifactsManifestPath = options.TryGetValue("artifacts-manifest", out string? artifactsManifestPathValue)
            ? artifactsManifestPathValue
            : null;
        if (artifactsManifestPath is not null && string.IsNullOrWhiteSpace(artifactsManifestPath))
        {
            return EngineCliParseResult.Failure("Option '--artifacts-manifest' cannot be empty.");
        }

        bool verifyReleaseProof = false;
        if (options.TryGetValue("verify-release-proof", out string? verifyReleaseProofValue))
        {
            if (!bool.TryParse(verifyReleaseProofValue, out verifyReleaseProof))
            {
                return EngineCliParseResult.Failure("Option '--verify-release-proof' must be 'true' or 'false'.");
            }
        }

        string? releaseProofPath = options.TryGetValue("release-proof", out string? releaseProofPathValue)
            ? releaseProofPathValue
            : null;
        if (releaseProofPath is not null && string.IsNullOrWhiteSpace(releaseProofPath))
        {
            return EngineCliParseResult.Failure("Option '--release-proof' cannot be empty.");
        }

        return EngineCliParseResult.Success(
            new DoctorCommand(
                project,
                runtimePerfMetricsPath,
                maxAverageCaptureCpuMs,
                maxPeakCaptureAllocatedBytes,
                requireZeroAllocationCapturePath,
                requireRuntimeTransportSuccess,
                multiplayerDemoSummaryPath,
                verifyMultiplayerSnapshotBinary,
                multiplayerSnapshotBinaryPath,
                verifyMultiplayerRpcBinary,
                multiplayerRpcBinaryPath,
                verifyMultiplayerOrchestration,
                multiplayerOrchestrationPath,
                verifyCaptureRgba16FloatBinary,
                captureRgba16FloatBinaryPath,
                captureRgba16FloatExrPath,
                verifyRenderStatsArtifact,
                renderStatsArtifactPath,
                verifyTestHostConfig,
                testHostConfigPath,
                verifyNetProfileLog,
                netProfileLogPath,
                verifyReplayRecording,
                replayRecordingPath,
                verifyArtifactsManifest,
                artifactsManifestPath,
                verifyReleaseProof,
                releaseProofPath));
    }

    private static string GetOutOrOutputPath(
        IReadOnlyDictionary<string, string> options,
        string defaultValue,
        out string? error)
    {
        bool hasOut = options.TryGetValue("out", out string? outValue);
        bool hasOutput = options.TryGetValue("output", out string? outputValue);

        if (hasOut && hasOutput)
        {
            error = "Options '--out' and '--output' cannot be used together.";
            return defaultValue;
        }

        string result = hasOut ? outValue! : hasOutput ? outputValue! : defaultValue;
        if (string.IsNullOrWhiteSpace(result))
        {
            error = "Output path cannot be empty.";
            return defaultValue;
        }

        error = null;
        return result;
    }

}
