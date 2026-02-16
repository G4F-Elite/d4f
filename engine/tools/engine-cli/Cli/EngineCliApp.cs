using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Engine.AssetPipeline;

namespace Engine.Cli;

public sealed class EngineCliApp
{
    private static readonly Regex ProjectNameRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly IExternalCommandRunner _commandRunner;

    public EngineCliApp(TextWriter stdout, TextWriter stderr)
        : this(stdout, stderr, new ProcessExternalCommandRunner())
    {
    }

    public EngineCliApp(TextWriter stdout, TextWriter stderr, IExternalCommandRunner commandRunner)
    {
        _stdout = stdout;
        _stderr = stderr;
        _commandRunner = commandRunner;
    }

    public int Run(string[] args)
    {
        EngineCliParseResult parseResult = EngineCliParser.Parse(args);
        if (!parseResult.IsSuccess)
        {
            _stderr.WriteLine(parseResult.Error);
            return 1;
        }

        try
        {
            return parseResult.Command switch
            {
                InitCommand cmd => HandleInit(cmd),
                BuildCommand cmd => HandleBuild(cmd),
                RunCommand cmd => HandleRun(cmd),
                PackCommand cmd => HandlePack(cmd),
                _ => throw new NotSupportedException("Unsupported command type.")
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FileNotFoundException)
        {
            _stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private int HandleInit(InitCommand command)
    {
        if (!ProjectNameRegex.IsMatch(command.Name))
        {
            _stderr.WriteLine("Project name must contain only letters, numbers, '-' or '_'.");
            return 1;
        }

        string outputDirectory = Path.GetFullPath(command.OutputDirectory);
        string projectDirectory = Path.Combine(outputDirectory, command.Name);
        if (Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory already exists: {projectDirectory}");
            return 1;
        }

        ProjectTemplateInitializer.InitializeProject(projectDirectory, command.Name);

        _stdout.WriteLine($"Project initialized at: {projectDirectory}");
        return 0;
    }

    private int HandleBuild(BuildCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        string srcDirectory = Path.Combine(projectDirectory, "src");
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        if (!Directory.Exists(srcDirectory))
        {
            _stderr.WriteLine($"Missing source directory: {srcDirectory}");
            return 1;
        }

        string outputDirectory = Path.Combine(projectDirectory, "build", command.Configuration);
        Directory.CreateDirectory(outputDirectory);
        string outputFile = Path.Combine(outputDirectory, "game.bin");
        File.WriteAllText(outputFile, $"build:{DateTime.UtcNow:O}");

        _stdout.WriteLine($"Build completed: {outputFile}");
        return 0;
    }

    private int HandleRun(RunCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string binaryPath = Path.Combine(projectDirectory, "build", command.Configuration, "game.bin");
        if (!File.Exists(binaryPath))
        {
            _stderr.WriteLine($"Build artifact was not found: {binaryPath}. Run 'build' first.");
            return 1;
        }

        _stdout.WriteLine($"Running project '{projectDirectory}' with '{binaryPath}'.");
        return 0;
    }

    private int HandlePack(PackCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string manifestPath = AssetPipelineService.ResolveRelativePath(projectDirectory, command.ManifestPath);
        string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? projectDirectory;
        AssetManifest manifest = AssetPipelineService.LoadManifest(manifestPath);
        AssetPipelineService.ValidateAssetsExist(manifest, manifestDirectory);

        string outputPakPath = AssetPipelineService.ResolveRelativePath(projectDirectory, command.OutputPakPath);
        string outputDirectory = Path.GetDirectoryName(outputPakPath) ?? projectDirectory;
        string compiledRootDirectory = Path.Combine(outputDirectory, "compiled");
        IReadOnlyList<PakEntry> compiledEntries = AssetPipelineService.CompileAssets(
            manifest,
            manifestDirectory,
            compiledRootDirectory);
        AssetPipelineService.WritePak(outputPakPath, compiledEntries);
        string compiledManifestPath = Path.Combine(outputDirectory, AssetPipelineService.CompiledManifestFileName);
        AssetPipelineService.WriteCompiledManifest(compiledManifestPath, compiledEntries);

        string packageRoot = Path.Combine(outputDirectory, "package");
        string appDirectory = Path.Combine(packageRoot, "App");
        string contentDirectory = Path.Combine(packageRoot, "Content");
        string contentCompiledDirectory = Path.Combine(contentDirectory, "compiled");

        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(contentDirectory);

        File.Copy(outputPakPath, Path.Combine(contentDirectory, "Game.pak"), overwrite: true);
        File.Copy(compiledManifestPath, Path.Combine(contentDirectory, AssetPipelineService.CompiledManifestFileName), overwrite: true);
        CopyDirectory(compiledRootDirectory, contentCompiledDirectory);

        string? publishProjectPath = ResolvePublishProjectPath(projectDirectory, command.PublishProjectPath);
        if (publishProjectPath is not null)
        {
            RunDotnetPublish(projectDirectory, command, publishProjectPath, appDirectory);
        }
        else
        {
            _stdout.WriteLine("Publish skipped: runtime .csproj was not found and '--publish-project' is not set.");
        }

        CopyNativeLibrary(projectDirectory, command, appDirectory);
        WritePackConfig(packageRoot, command);

        if (!string.IsNullOrWhiteSpace(command.ZipOutputPath))
        {
            string zipOutputPath = AssetPipelineService.ResolveRelativePath(projectDirectory, command.ZipOutputPath);
            CreateZipArchive(packageRoot, zipOutputPath);
            _stdout.WriteLine($"Package archive created: {zipOutputPath}");
        }

        _stdout.WriteLine($"Pak created: {outputPakPath}");
        _stdout.WriteLine($"Compiled manifest created: {compiledManifestPath}");
        _stdout.WriteLine($"Portable package prepared: {packageRoot}");
        return 0;
    }

    private static string? ResolvePublishProjectPath(string projectDirectory, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            string fullConfiguredPath = AssetPipelineService.ResolveRelativePath(projectDirectory, configuredPath);
            if (!File.Exists(fullConfiguredPath))
            {
                throw new FileNotFoundException($"Publish project file was not found: {fullConfiguredPath}", fullConfiguredPath);
            }

            return fullConfiguredPath;
        }

        string srcDirectory = Path.Combine(projectDirectory, "src");
        if (!Directory.Exists(srcDirectory))
        {
            return null;
        }

        string[] runtimeProjects = Directory.GetFiles(srcDirectory, "*Runtime*.csproj", SearchOption.AllDirectories);
        if (runtimeProjects.Length > 0)
        {
            Array.Sort(runtimeProjects, StringComparer.OrdinalIgnoreCase);
            return runtimeProjects[0];
        }

        string[] projects = Directory.GetFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories);
        if (projects.Length == 0)
        {
            return null;
        }

        Array.Sort(projects, StringComparer.OrdinalIgnoreCase);
        return projects[0];
    }

    private void RunDotnetPublish(
        string projectDirectory,
        PackCommand command,
        string publishProjectPath,
        string appDirectory)
    {
        Directory.CreateDirectory(appDirectory);

        string[] arguments =
        [
            "publish",
            publishProjectPath,
            "-c",
            command.Configuration,
            "-r",
            command.RuntimeIdentifier,
            "--self-contained",
            "true",
            "-o",
            appDirectory
        ];

        int exitCode = _commandRunner.Run("dotnet", arguments, projectDirectory, _stdout, _stderr);
        if (exitCode != 0)
        {
            throw new InvalidDataException($"dotnet publish failed with exit code {exitCode}.");
        }
    }

    private void CopyNativeLibrary(string projectDirectory, PackCommand command, string appDirectory)
    {
        string? nativeLibraryPath = ResolveNativeLibraryPath(projectDirectory, command);
        if (nativeLibraryPath is null)
        {
            _stdout.WriteLine("Native library copy skipped: native DLL was not found.");
            return;
        }

        Directory.CreateDirectory(appDirectory);
        string destinationPath = Path.Combine(appDirectory, Path.GetFileName(nativeLibraryPath));
        File.Copy(nativeLibraryPath, destinationPath, overwrite: true);
    }

    private static string? ResolveNativeLibraryPath(string projectDirectory, PackCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.NativeLibraryPath))
        {
            string configuredPath = AssetPipelineService.ResolveRelativePath(projectDirectory, command.NativeLibraryPath);
            if (!File.Exists(configuredPath))
            {
                throw new FileNotFoundException($"Native library was not found: {configuredPath}", configuredPath);
            }

            return configuredPath;
        }

        string autoPath = Path.GetFullPath(
            Path.Combine(
                Environment.CurrentDirectory,
                "engine",
                "native",
                "build",
                command.Configuration,
                "dff_native.dll"));
        return File.Exists(autoPath) ? autoPath : null;
    }

    private static void WritePackConfig(string packageRoot, PackCommand command)
    {
        string configDirectory = Path.Combine(packageRoot, "config");
        Directory.CreateDirectory(configDirectory);

        var config = new
        {
            runtime = command.RuntimeIdentifier,
            configuration = command.Configuration,
            contentPak = "Content/Game.pak",
            compiledManifest = $"Content/{AssetPipelineService.CompiledManifestFileName}",
            appDirectory = "App",
            generatedAtUtc = DateTime.UtcNow
        };

        string configPath = Path.Combine(configDirectory, "runtime.json");
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(configPath, json);
    }

    private static void CreateZipArchive(string packageRoot, string zipOutputPath)
    {
        string zipDirectory = Path.GetDirectoryName(zipOutputPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(zipDirectory))
        {
            Directory.CreateDirectory(zipDirectory);
        }

        if (File.Exists(zipOutputPath))
        {
            File.Delete(zipOutputPath);
        }

        ZipFile.CreateFromDirectory(packageRoot, zipOutputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
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
