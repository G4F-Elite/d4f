using System.Text;

namespace Engine.Cli;

internal static class ProjectTemplateInitializer
{
    private const string GameNameToken = "__GAME_NAME__";
    private const string EngineManagedSourceRelativeToken = "__ENGINE_MANAGED_SRC_RELATIVE__";
    private const string RuntimeTemplateDirectoryName = "Game.Runtime";
    private const string RuntimeTemplateProjectName = "Game.Runtime.csproj";

    public static void InitializeProject(string projectDirectory, string gameName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        string? templateDirectory = ResolveTemplateDirectory();
        if (templateDirectory is null)
        {
            throw new DirectoryNotFoundException("game-template directory was not found under 'templates/game-template'.");
        }

        CopyDirectory(templateDirectory, projectDirectory);
        EnsureGameAssetsDirectory(projectDirectory);
        RenameRuntimeProject(projectDirectory, gameName);

        string? engineManagedSourceDirectory = ResolveEngineManagedSourceDirectory();
        if (engineManagedSourceDirectory is null)
        {
            throw new DirectoryNotFoundException(
                "Engine managed source directory was not found under 'engine/managed/src'.");
        }

        string runtimeProjectDirectory = ResolveRuntimeProjectDirectory(projectDirectory, gameName);
        string relativeManagedSourcePath = NormalizeRelativePath(
            Path.GetRelativePath(runtimeProjectDirectory, engineManagedSourceDirectory));

        ReplaceTokensInTextFiles(projectDirectory, GameNameToken, gameName);
        ReplaceTokensInTextFiles(projectDirectory, EngineManagedSourceRelativeToken, relativeManagedSourcePath);
    }

    private static void EnsureGameAssetsDirectory(string projectDirectory)
    {
        string gameAssetsDirectory = Path.Combine(projectDirectory, "GameAssets");
        Directory.CreateDirectory(gameAssetsDirectory);

        string keepFilePath = Path.Combine(gameAssetsDirectory, ".gitkeep");
        if (!File.Exists(keepFilePath))
        {
            File.WriteAllText(keepFilePath, string.Empty, Encoding.UTF8);
        }
    }

    private static void RenameRuntimeProject(string projectDirectory, string gameName)
    {
        string templateRuntimeDirectory = Path.Combine(projectDirectory, "src", RuntimeTemplateDirectoryName);
        if (!Directory.Exists(templateRuntimeDirectory))
        {
            return;
        }

        string runtimeDirectory = Path.Combine(projectDirectory, "src", $"{gameName}.Runtime");
        Directory.Move(templateRuntimeDirectory, runtimeDirectory);

        string templateProjectPath = Path.Combine(runtimeDirectory, RuntimeTemplateProjectName);
        if (File.Exists(templateProjectPath))
        {
            string targetProjectPath = Path.Combine(runtimeDirectory, $"{gameName}.Runtime.csproj");
            File.Move(templateProjectPath, targetProjectPath);
        }
    }

    private static void ReplaceTokensInTextFiles(string rootDirectory, string token, string replacement)
    {
        foreach (string filePath in Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            if (!IsTextLikeFile(filePath))
            {
                continue;
            }

            string content = File.ReadAllText(filePath, Encoding.UTF8);
            if (!content.Contains(token, StringComparison.Ordinal))
            {
                continue;
            }

            string updated = content.Replace(token, replacement, StringComparison.Ordinal);
            File.WriteAllText(filePath, updated, Encoding.UTF8);
        }
    }

    private static bool IsTextLikeFile(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".props", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveTemplateDirectory()
    {
        string fromCurrentDirectory = Path.Combine(Environment.CurrentDirectory, "templates", "game-template");
        if (Directory.Exists(fromCurrentDirectory))
        {
            return fromCurrentDirectory;
        }

        DirectoryInfo? probe = new(AppContext.BaseDirectory);
        while (probe is not null)
        {
            string candidate = Path.Combine(probe.FullName, "templates", "game-template");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        return null;
    }

    private static string? ResolveEngineManagedSourceDirectory()
    {
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

    private static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ".";
        }

        return path.Replace('\\', '/');
    }

    private static string ResolveRuntimeProjectDirectory(string projectDirectory, string gameName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        string expectedRuntimeDirectory = Path.Combine(projectDirectory, "src", $"{gameName}.Runtime");
        if (Directory.Exists(expectedRuntimeDirectory))
        {
            return expectedRuntimeDirectory;
        }

        string sourceDirectory = Path.Combine(projectDirectory, "src");
        if (Directory.Exists(sourceDirectory))
        {
            string? fallbackProjectPath = Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fallbackProjectPath))
            {
                string? runtimeDirectory = Path.GetDirectoryName(fallbackProjectPath);
                if (!string.IsNullOrWhiteSpace(runtimeDirectory))
                {
                    return runtimeDirectory;
                }
            }
        }

        return projectDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Directory was not found: {sourceDirectory}");
        }

        Directory.CreateDirectory(destinationDirectory);

        foreach (string filePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            string destinationPath = Path.Combine(destinationDirectory, relativePath);
            string destinationParent = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidDataException($"Destination path is invalid: {destinationPath}");
            Directory.CreateDirectory(destinationParent);
            File.Copy(filePath, destinationPath, overwrite: true);
        }
    }
}
