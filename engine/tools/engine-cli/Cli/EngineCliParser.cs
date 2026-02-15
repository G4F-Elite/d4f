namespace Engine.Cli;

public static class EngineCliParser
{
    private static readonly HashSet<string> ValidConfigurations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Debug",
        "Release"
    };

    public static EngineCliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return EngineCliParseResult.Failure("Command is required. Available commands: init, build, run, pack.");
        }

        string commandName = args[0].ToLowerInvariant();
        Dictionary<string, string> optionsResult = ParseOptions(args[1..], out string? parseError);
        if (parseError is not null)
        {
            return EngineCliParseResult.Failure(parseError);
        }

        return commandName switch
        {
            "init" => ParseInit(optionsResult),
            "build" => ParseBuild(optionsResult),
            "run" => ParseRun(optionsResult),
            "pack" => ParsePack(optionsResult),
            _ => EngineCliParseResult.Failure($"Unknown command '{args[0]}'. Available commands: init, build, run, pack.")
        };
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

        return EngineCliParseResult.Success(new RunCommand(project, configuration));
    }

    private static EngineCliParseResult ParsePack(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("project", out string? project))
        {
            return EngineCliParseResult.Failure("Option '--project' is required for 'pack'.");
        }

        if (!options.TryGetValue("manifest", out string? manifest))
        {
            return EngineCliParseResult.Failure("Option '--manifest' is required for 'pack'.");
        }

        string output = options.TryGetValue("output", out string? outputValue)
            ? outputValue
            : Path.Combine(project, "dist", "content.pak");

        return EngineCliParseResult.Success(new PackCommand(project, manifest, output));
    }

    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args, out string? error)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i += 2)
        {
            string current = args[i];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Expected option name, but got '{current}'.";
                return result;
            }

            if (i + 1 >= args.Count)
            {
                error = $"Option '{current}' requires a value.";
                return result;
            }

            string key = current[2..];
            string value = args[i + 1];

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Option name cannot be empty.";
                return result;
            }

            if (result.ContainsKey(key))
            {
                error = $"Option '--{key}' is duplicated.";
                return result;
            }

            result[key] = value;
        }

        error = null;
        return result;
    }
}
