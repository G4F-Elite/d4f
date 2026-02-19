using Engine.Rendering;
using System.Globalization;

namespace Engine.Cli;

public static partial class EngineCliParser
{
    private const string AvailableCommandsText = "new, init, build, run, bake, preview, preview audio, preview dump, test, pack, doctor, api dump.";
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
        ["ao"] = RenderDebugViewMode.AmbientOcclusion
    };
    private const string DebugViewOptionError = "Option '--debug-view' must be one of: none, depth, normals, albedo, roughness, ao.";

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
                multiplayerRpcBinaryPath));
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
