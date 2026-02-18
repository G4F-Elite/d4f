using Engine.Rendering;

namespace Engine.Cli;

public static partial class EngineCliParser
{
    private const string AvailableCommandsText = "new, init, build, run, bake, preview, preview dump, test, pack, doctor, api dump.";
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

        return EngineCliParseResult.Success(new DoctorCommand(project));
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
