using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Engine.AssetPipeline;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandleUpdate(UpdateCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string srcDirectory = Path.Combine(projectDirectory, "src");
        if (!Directory.Exists(srcDirectory))
        {
            _stderr.WriteLine($"Project source directory does not exist: {srcDirectory}");
            return 1;
        }

        string? engineManagedSourceDirectory = ResolveEngineManagedSourceDirectory(projectDirectory, command.EngineManagedSourcePath);
        if (engineManagedSourceDirectory is null)
        {
            _stderr.WriteLine(
                "Engine managed source directory was not found. Provide '--engine-managed-src <path-to-engine/managed/src>' or run from d4f repository root.");
            return 1;
        }

        string[] projectFiles = Directory
            .GetFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (projectFiles.Length == 0)
        {
            _stderr.WriteLine($"No .csproj files were found under: {srcDirectory}");
            return 1;
        }

        int updatedProjectFileCount = 0;
        int updatedReferenceCount = 0;

        foreach (string projectFilePath in projectFiles)
        {
            if (!TryUpdateEngineReferencesInProject(
                    projectFilePath,
                    engineManagedSourceDirectory,
                    out bool fileChanged,
                    out int fileUpdatedReferenceCount,
                    out string? errorMessage))
            {
                _stderr.WriteLine(errorMessage);
                return 1;
            }

            if (fileChanged)
            {
                updatedProjectFileCount++;
            }

            updatedReferenceCount += fileUpdatedReferenceCount;
        }

        if (updatedReferenceCount == 0)
        {
            _stdout.WriteLine("No Engine.* project references were found to update.");
        }
        else
        {
            _stdout.WriteLine(
                $"Updated {updatedReferenceCount} engine project reference(s) across {updatedProjectFileCount} .csproj file(s).");
        }

        string projectMetadataPath = Path.Combine(projectDirectory, "project.json");
        if (File.Exists(projectMetadataPath))
        {
            string engineVersion = ResolveCurrentEngineVersion();
            if (!TryUpdateProjectEngineVersion(projectMetadataPath, engineVersion, out bool versionChanged, out string? errorMessage))
            {
                _stderr.WriteLine(errorMessage);
                return 1;
            }

            if (versionChanged)
            {
                _stdout.WriteLine($"Updated project.json engineVersion to '{engineVersion}'.");
            }
            else
            {
                _stdout.WriteLine($"project.json engineVersion is already '{engineVersion}'.");
            }
        }
        else
        {
            _stdout.WriteLine("project.json was not found. Engine version metadata update skipped.");
        }

        _stdout.WriteLine("Engine update completed.");
        return 0;
    }

    private static bool TryUpdateEngineReferencesInProject(
        string projectFilePath,
        string engineManagedSourceDirectory,
        out bool fileChanged,
        out int updatedReferenceCount,
        out string? errorMessage)
    {
        fileChanged = false;
        updatedReferenceCount = 0;
        errorMessage = null;

        XDocument projectDocument;
        try
        {
            projectDocument = XDocument.Load(projectFilePath, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            errorMessage = $"Failed to read project file '{projectFilePath}': {ex.Message}";
            return false;
        }

        string projectFileDirectory = Path.GetDirectoryName(projectFilePath)
            ?? throw new InvalidDataException($"Project file path is invalid: {projectFilePath}");
        string relativeManagedSourcePath = NormalizeRelativePath(
            Path.GetRelativePath(projectFileDirectory, engineManagedSourceDirectory));

        List<XAttribute> includeAttributes = projectDocument
            .Descendants()
            .Where(static element =>
                string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
            .Select(static element => element.Attribute("Include"))
            .Where(static attribute => attribute is not null)
            .Cast<XAttribute>()
            .ToList();

        foreach (XAttribute includeAttribute in includeAttributes)
        {
            string currentInclude = includeAttribute.Value ?? string.Empty;
            if (!TryBuildUpdatedEngineReferenceInclude(
                    currentInclude,
                    projectFileDirectory,
                    relativeManagedSourcePath,
                    out string? updatedIncludeCandidate,
                    out string? absoluteTargetPathCandidate))
            {
                continue;
            }

            string updatedInclude = updatedIncludeCandidate ?? currentInclude;
            string absoluteTargetPath = absoluteTargetPathCandidate ?? string.Empty;

            if (!File.Exists(absoluteTargetPath))
            {
                errorMessage =
                    $"Engine project reference target does not exist for '{projectFilePath}': '{currentInclude}' -> '{absoluteTargetPath}'.";
                return false;
            }

            if (string.Equals(currentInclude, updatedInclude, StringComparison.Ordinal))
            {
                continue;
            }

            includeAttribute.Value = updatedInclude;
            fileChanged = true;
            updatedReferenceCount++;
        }

        if (!fileChanged)
        {
            return true;
        }

        try
        {
            projectDocument.Save(projectFilePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errorMessage = $"Failed to write project file '{projectFilePath}': {ex.Message}";
            return false;
        }
    }

    private static bool TryBuildUpdatedEngineReferenceInclude(
        string currentInclude,
        string projectFileDirectory,
        string relativeManagedSourcePath,
        out string? updatedInclude,
        out string? absoluteTargetPath)
    {
        updatedInclude = null;
        absoluteTargetPath = null;

        if (string.IsNullOrWhiteSpace(currentInclude))
        {
            return false;
        }

        string normalizedInclude = currentInclude.Replace('\\', '/');
        int engineSegmentIndex = normalizedInclude.IndexOf("Engine.", StringComparison.Ordinal);
        if (engineSegmentIndex < 0)
        {
            return false;
        }

        string engineRelativeSuffix = normalizedInclude[engineSegmentIndex..];
        string fileName = Path.GetFileName(engineRelativeSuffix);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.StartsWith("Engine.", StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        updatedInclude = relativeManagedSourcePath == "."
            ? engineRelativeSuffix
            : $"{relativeManagedSourcePath}/{engineRelativeSuffix}";
        updatedInclude = updatedInclude.Replace("//", "/", StringComparison.Ordinal);

        string localPath = updatedInclude.Replace('/', Path.DirectorySeparatorChar);
        absoluteTargetPath = Path.GetFullPath(Path.Combine(projectFileDirectory, localPath));
        return true;
    }

    private static bool TryUpdateProjectEngineVersion(
        string projectMetadataPath,
        string engineVersion,
        out bool changed,
        out string? errorMessage)
    {
        changed = false;
        errorMessage = null;

        JsonObject? jsonObject;
        try
        {
            JsonNode? rootNode = JsonNode.Parse(File.ReadAllText(projectMetadataPath));
            jsonObject = rootNode as JsonObject;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            errorMessage = $"Failed to read project metadata '{projectMetadataPath}': {ex.Message}";
            return false;
        }

        if (jsonObject is null)
        {
            errorMessage = $"Project metadata '{projectMetadataPath}' must contain a JSON object.";
            return false;
        }

        string? currentVersion = jsonObject["engineVersion"]?.GetValue<string>();
        if (string.Equals(currentVersion, engineVersion, StringComparison.Ordinal))
        {
            return true;
        }

        jsonObject["engineVersion"] = engineVersion;
        changed = true;

        try
        {
            string updatedJson = jsonObject.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(projectMetadataPath, updatedJson + Environment.NewLine);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errorMessage = $"Failed to write project metadata '{projectMetadataPath}': {ex.Message}";
            return false;
        }
    }

    private static string? ResolveEngineManagedSourceDirectory(string projectDirectory, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string configuredRoot = AssetPipelineService.ResolveRelativePath(projectDirectory, configuredPath);

            if (Directory.Exists(configuredRoot))
            {
                string directManagedSource = Path.GetFullPath(configuredRoot);
                if (IsManagedSourceDirectory(directManagedSource))
                {
                    return directManagedSource;
                }

                string nestedManagedSource = Path.Combine(directManagedSource, "engine", "managed", "src");
                if (Directory.Exists(nestedManagedSource))
                {
                    return nestedManagedSource;
                }
            }

            return null;
        }

        string fromCurrentDirectory = Path.Combine(Environment.CurrentDirectory, "engine", "managed", "src");
        if (Directory.Exists(fromCurrentDirectory))
        {
            return fromCurrentDirectory;
        }

        DirectoryInfo? probe = new(AppContext.BaseDirectory);
        while (probe is not null)
        {
            string candidate = Path.Combine(probe.FullName, "engine", "managed", "src");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return null;
    }

    private static bool IsManagedSourceDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        return File.Exists(Path.Combine(directoryPath, "Engine.Core", "Engine.Core.csproj"));
    }

    private static string ResolveCurrentEngineVersion()
    {
        foreach (string candidatePath in EnumerateTemplateProjectJsonCandidates())
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                JsonNode? node = JsonNode.Parse(File.ReadAllText(candidatePath));
                if (node is JsonObject jsonObject)
                {
                    string? value = jsonObject["engineVersion"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return "0.1.0";
    }

    private static IEnumerable<string> EnumerateTemplateProjectJsonCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string fromCurrentDirectory = Path.Combine(Environment.CurrentDirectory, "templates", "game-template", "project.json");
        if (seen.Add(fromCurrentDirectory))
        {
            yield return fromCurrentDirectory;
        }

        DirectoryInfo? probe = new(AppContext.BaseDirectory);
        while (probe is not null)
        {
            string candidate = Path.Combine(probe.FullName, "templates", "game-template", "project.json");
            if (seen.Add(candidate))
            {
                yield return candidate;
            }

            probe = probe.Parent;
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ".";
        }

        return path.Replace('\\', '/');
    }
}
