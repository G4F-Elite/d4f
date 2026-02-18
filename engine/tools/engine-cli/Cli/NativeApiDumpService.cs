using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

namespace Engine.Cli;

internal sealed record NativeApiFunction(string ReturnType, string Name, string Declaration);

internal sealed record NativeApiDump(
    uint ApiVersion,
    string HeaderPath,
    IReadOnlyList<NativeApiFunction> Functions);

internal static partial class NativeApiDumpService
{
    [GeneratedRegex(@"^\s*#define\s+ENGINE_NATIVE_API_VERSION\s+(\d+)u\b", RegexOptions.CultureInvariant)]
    private static partial Regex ApiVersionRegex();

    [GeneratedRegex(@"^\s*ENGINE_NATIVE_API\s+(.+?)\s+([A-Za-z_]\w*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex FunctionRegex();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Dump(string headerPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string fullHeaderPath = Path.GetFullPath(headerPath);
        if (!File.Exists(fullHeaderPath))
        {
            throw new FileNotFoundException($"Native API header was not found: {fullHeaderPath}", fullHeaderPath);
        }

        string[] lines = File.ReadAllLines(fullHeaderPath);
        uint? apiVersion = null;
        var functions = new List<NativeApiFunction>();
        Regex versionRegex = ApiVersionRegex();
        Regex functionRegex = FunctionRegex();

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            Match versionMatch = versionRegex.Match(line);
            if (versionMatch.Success)
            {
                apiVersion = uint.Parse(versionMatch.Groups[1].Value);
                continue;
            }

            Match functionMatch = functionRegex.Match(line);
            if (!functionMatch.Success)
            {
                continue;
            }

            string returnType = functionMatch.Groups[1].Value.Trim();
            string name = functionMatch.Groups[2].Value.Trim();
            string declaration = CollectDeclaration(lines, lineIndex, out int declarationEndLineIndex);
            lineIndex = declarationEndLineIndex;
            functions.Add(new NativeApiFunction(returnType, name, declaration));
        }

        if (apiVersion is null)
        {
            throw new InvalidDataException("ENGINE_NATIVE_API_VERSION was not found in header.");
        }

        if (functions.Count == 0)
        {
            throw new InvalidDataException("No ENGINE_NATIVE_API declarations were found in header.");
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var dump = new NativeApiDump(apiVersion.Value, fullHeaderPath, functions);
        string json = JsonSerializer.Serialize(dump, SerializerOptions);
        File.WriteAllText(fullOutputPath, json);
        return fullOutputPath;
    }

    private static string CollectDeclaration(
        IReadOnlyList<string> lines,
        int startIndex,
        out int endIndex)
    {
        if (startIndex < 0 || startIndex >= lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        var builder = new StringBuilder();
        endIndex = startIndex;

        AppendDeclarationLine(builder, lines[startIndex]);
        while (!LooksLikeCompleteDeclaration(builder))
        {
            if (endIndex + 1 >= lines.Count)
            {
                break;
            }

            endIndex++;
            AppendDeclarationLine(builder, lines[endIndex]);
        }

        return builder.ToString();
    }

    private static void AppendDeclarationLine(StringBuilder builder, string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(trimmed);
    }

    private static bool LooksLikeCompleteDeclaration(StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return false;
        }

        int parenDepth = 0;
        for (int i = 0; i < builder.Length; i++)
        {
            char ch = builder[i];
            if (ch == '(')
            {
                parenDepth++;
                continue;
            }

            if (ch == ')' && parenDepth > 0)
            {
                parenDepth--;
            }
        }

        return parenDepth == 0 && builder.ToString().Contains(';', StringComparison.Ordinal);
    }
}
