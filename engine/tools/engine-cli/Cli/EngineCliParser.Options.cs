namespace Engine.Cli;

public static partial class EngineCliParser
{
    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args, out string? error)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i += 2)
        {
            string current = args[i];

            if (i + 1 >= args.Count)
            {
                error = $"Option '{current}' requires a value.";
                return result;
            }

            string key;
            if (current.StartsWith("--", StringComparison.Ordinal))
            {
                key = current[2..];
            }
            else if (current.StartsWith("-", StringComparison.Ordinal) && current.Length == 2)
            {
                key = NormalizeShortOption(current[1]);
                if (key.Length == 0)
                {
                    error = $"Unknown short option '{current}'.";
                    return result;
                }
            }
            else
            {
                error = $"Expected option name, but got '{current}'.";
                return result;
            }

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

    private static string NormalizeShortOption(char option)
    {
        return char.ToLowerInvariant(option) switch
        {
            'c' => "configuration",
            'r' => "runtime",
            'o' => "out",
            'p' => "project",
            'm' => "manifest",
            'n' => "name",
            'e' => "engine-managed-src",
            _ => string.Empty
        };
    }
}
