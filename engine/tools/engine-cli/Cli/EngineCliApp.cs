using System.Text.RegularExpressions;
using Engine.AssetPipeline;

namespace Engine.Cli;

public sealed class EngineCliApp
{
    private static readonly Regex ProjectNameRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public EngineCliApp(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
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

        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(Path.Combine(projectDirectory, "src"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "assets"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "tests"));

        File.WriteAllText(
            Path.Combine(projectDirectory, "project.json"),
            """
            {
              "name": "__NAME__",
              "engineVersion": "0.1.0"
            }
            """.Replace("__NAME__", command.Name, StringComparison.Ordinal));

        File.WriteAllText(
            Path.Combine(projectDirectory, "assets", "manifest.json"),
            """
            {
              "version": 1,
              "assets": [
                {
                  "path": "example.txt",
                  "kind": "text"
                }
              ]
            }
            """);

        File.WriteAllText(Path.Combine(projectDirectory, "assets", "example.txt"), "placeholder asset");
        File.WriteAllText(Path.Combine(projectDirectory, "src", "Main.scene"), "// scene placeholder");
        File.WriteAllText(Path.Combine(projectDirectory, "tests", "README.md"), "Tests go here.");

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

        _stdout.WriteLine($"Pak created: {outputPakPath}");
        _stdout.WriteLine($"Compiled manifest created: {compiledManifestPath}");
        return 0;
    }
}
