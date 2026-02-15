namespace Assetc;

public static class AssetcParser
{
    public static AssetcParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return AssetcParseResult.Failure("Command is required. Available commands: build, list.");
        }

        string commandName = args[0].ToLowerInvariant();
        Dictionary<string, string> options = ParseOptions(args[1..], out string? parseError);
        if (parseError is not null)
        {
            return AssetcParseResult.Failure(parseError);
        }

        return commandName switch
        {
            "build" => ParseBuild(options),
            "list" => ParseList(options),
            _ => AssetcParseResult.Failure($"Unknown command '{args[0]}'. Available commands: build, list.")
        };
    }

    private static AssetcParseResult ParseBuild(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("manifest", out string? manifestPath))
        {
            return AssetcParseResult.Failure("Option '--manifest' is required for 'build'.");
        }

        if (!options.TryGetValue("output", out string? outputPakPath))
        {
            return AssetcParseResult.Failure("Option '--output' is required for 'build'.");
        }

        return AssetcParseResult.Success(new BuildAssetsCommand(manifestPath, outputPakPath));
    }

    private static AssetcParseResult ParseList(IReadOnlyDictionary<string, string> options)
    {
        if (!options.TryGetValue("pak", out string? pakPath))
        {
            return AssetcParseResult.Failure("Option '--pak' is required for 'list'.");
        }

        return AssetcParseResult.Success(new ListAssetsCommand(pakPath));
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
