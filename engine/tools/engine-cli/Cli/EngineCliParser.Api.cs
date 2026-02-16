namespace Engine.Cli;

public static partial class EngineCliParser
{
    private static EngineCliParseResult ParseApi(string[] args)
    {
        if (args.Length < 2)
        {
            return EngineCliParseResult.Failure("Subcommand is required for 'api'. Available subcommands: dump.");
        }

        string subcommand = args[1].ToLowerInvariant();
        Dictionary<string, string> options = ParseOptions(args[2..], out string? parseError);
        if (parseError is not null)
        {
            return EngineCliParseResult.Failure(parseError);
        }

        return subcommand switch
        {
            "dump" => ParseDump(options),
            _ => EngineCliParseResult.Failure($"Unknown subcommand '{args[1]}' for 'api'. Available subcommands: dump.")
        };
    }

    private static EngineCliParseResult ParseDump(IReadOnlyDictionary<string, string> options)
    {
        string header = options.TryGetValue("header", out string? headerValue)
            ? headerValue
            : Path.Combine("engine", "native", "include", "engine_native.h");

        string output = GetOutOrOutputPath(
            options,
            defaultValue: Path.Combine("artifacts", "api", "native-api.json"),
            out string? outError);
        if (outError is not null)
        {
            return EngineCliParseResult.Failure(outError);
        }

        return EngineCliParseResult.Success(new ApiDumpCommand(header, output));
    }
}
