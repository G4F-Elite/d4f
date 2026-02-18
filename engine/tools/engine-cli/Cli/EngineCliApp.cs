using System.Text.RegularExpressions;
using Engine.AssetPipeline;
using Engine.Testing;

namespace Engine.Cli;

public sealed partial class EngineCliApp
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
                NewCommand cmd => HandleNew(cmd),
                InitCommand cmd => HandleInit(cmd),
                BuildCommand cmd => HandleBuild(cmd),
                RunCommand cmd => HandleRun(cmd),
                BakeCommand cmd => HandleBake(cmd),
                PreviewCommand cmd => HandlePreview(cmd),
                PreviewDumpCommand cmd => HandlePreviewDump(cmd),
                TestCommand cmd => HandleTest(cmd),
                PackCommand cmd => HandlePack(cmd),
                DoctorCommand cmd => HandleDoctor(cmd),
                ApiDumpCommand cmd => HandleApiDump(cmd),
                _ => throw new NotSupportedException("Unsupported command type.")
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FileNotFoundException)
        {
            _stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private int HandleNew(NewCommand command)
    {
        return HandleCreateProject(command.Name, command.OutputDirectory);
    }

    private int HandleInit(InitCommand command)
    {
        return HandleCreateProject(command.Name, command.OutputDirectory);
    }

    private int HandleCreateProject(string name, string outputDirectoryInput)
    {
        if (!ProjectNameRegex.IsMatch(name))
        {
            _stderr.WriteLine("Project name must contain only letters, numbers, '-' or '_'.");
            return 1;
        }

        string outputDirectory = Path.GetFullPath(outputDirectoryInput);
        string projectDirectory = Path.Combine(outputDirectory, name);
        if (Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory already exists: {projectDirectory}");
            return 1;
        }

        ProjectTemplateInitializer.InitializeProject(projectDirectory, name);

        _stdout.WriteLine($"Project initialized at: {projectDirectory}");
        return 0;
    }

    private int HandleBuild(BuildCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string? runtimeProjectPath = ResolvePublishProjectPath(projectDirectory, configuredPath: null);
        if (runtimeProjectPath is null)
        {
            _stderr.WriteLine(
                $"Runtime .csproj was not found under '{Path.Combine(projectDirectory, "src")}'.");
            return 1;
        }

        string[] arguments =
        [
            "build",
            runtimeProjectPath,
            "-c",
            command.Configuration,
            "--nologo"
        ];

        int exitCode = _commandRunner.Run("dotnet", arguments, projectDirectory, _stdout, _stderr);
        if (exitCode != 0)
        {
            _stderr.WriteLine($"dotnet build failed with exit code {exitCode}.");
            return 1;
        }

        _stdout.WriteLine($"Build completed: {runtimeProjectPath}");
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

        string? runtimeProjectPath = ResolvePublishProjectPath(projectDirectory, configuredPath: null);
        if (runtimeProjectPath is null)
        {
            _stderr.WriteLine(
                $"Runtime .csproj was not found under '{Path.Combine(projectDirectory, "src")}'.");
            return 1;
        }

        string debugViewLabel = command.DebugViewMode.ToString().ToLowerInvariant();
        string[] arguments =
        [
            "run",
            "--project",
            runtimeProjectPath,
            "-c",
            command.Configuration,
            "--no-launch-profile",
            "--",
            "--debug-view",
            debugViewLabel
        ];
        int exitCode = _commandRunner.Run("dotnet", arguments, projectDirectory, _stdout, _stderr);
        if (exitCode != 0)
        {
            _stderr.WriteLine($"dotnet run failed with exit code {exitCode}.");
            return 1;
        }

        _stdout.WriteLine(
            $"Run completed for '{runtimeProjectPath}'. Debug view: {debugViewLabel}.");
        return 0;
    }

    private int HandleBake(BakeCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        BakedContentOutput baked = BakePipeline.BakeProject(
            projectDirectory,
            command.ManifestPath,
            command.OutputPakPath);

        _stdout.WriteLine($"Pak created: {baked.OutputPakPath}");
        _stdout.WriteLine($"Compiled manifest created: {baked.CompiledManifestPath}");
        return 0;
    }

    private int HandlePreview(PreviewCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string outputDirectory = AssetPipelineService.ResolveRelativePath(projectDirectory, command.OutputDirectory);
        string previewPakPath = Path.Combine(outputDirectory, "preview-content.pak");
        BakedContentOutput baked = BakePipeline.BakeProject(
            projectDirectory,
            command.ManifestPath,
            previewPakPath);
        string artifactsManifestPath = PreviewArtifactGenerator.Generate(outputDirectory, baked.CompiledEntries);

        _stdout.WriteLine($"Preview artifacts created: {outputDirectory}");
        _stdout.WriteLine($"Preview manifest created: {artifactsManifestPath}");
        return 0;
    }

    private int HandlePreviewDump(PreviewDumpCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string manifestPath = AssetPipelineService.ResolveRelativePath(projectDirectory, command.ManifestPath);
        TestingArtifactManifest manifest = TestingArtifactManifestCodec.Read(manifestPath);

        _stdout.WriteLine($"Preview artifact manifest: {manifestPath}");
        _stdout.WriteLine($"Generated at: {manifest.GeneratedAtUtc:O}");
        foreach (TestingArtifactEntry artifact in manifest.Artifacts)
        {
            _stdout.WriteLine($"{artifact.Kind}\t{artifact.RelativePath}\t{artifact.Description}");
        }

        return 0;
    }

    private int HandleTest(TestCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string[] arguments =
        [
            "test",
            projectDirectory,
            "-c",
            command.Configuration,
            "--nologo"
        ];

        int exitCode = _commandRunner.Run("dotnet", arguments, projectDirectory, _stdout, _stderr);
        if (exitCode != 0)
        {
            _stderr.WriteLine($"dotnet test failed with exit code {exitCode}.");
            return 1;
        }

        string artifactsDirectory = AssetPipelineService.ResolveRelativePath(projectDirectory, command.ArtifactsDirectory);
        var generationOptions = new TestArtifactGenerationOptions(
            command.CaptureFrame,
            command.ReplaySeed,
            command.FixedDeltaSeconds);
        TestArtifactsOutput artifactsOutput = TestArtifactGenerator.Generate(artifactsDirectory, generationOptions);

        if (!string.IsNullOrWhiteSpace(command.GoldenDirectory))
        {
            string goldenDirectory = AssetPipelineService.ResolveRelativePath(projectDirectory, command.GoldenDirectory);
            GoldenImageComparisonOptions comparisonOptions = command.PixelPerfectGolden
                ? GoldenImageComparisonOptions.PixelPerfect
                : new GoldenImageComparisonOptions
                {
                    PixelPerfectMatch = false,
                    MaxMeanAbsoluteError = command.TolerantMaxMae,
                    MinPsnrDb = command.TolerantMinPsnrDb
                };

            GoldenComparisonSummary summary = GoldenArtifactsComparer.Compare(
                artifactsDirectory,
                goldenDirectory,
                artifactsOutput.Captures,
                comparisonOptions);
            if (!summary.IsSuccess)
            {
                foreach (string failure in summary.Failures)
                {
                    _stderr.WriteLine(failure);
                }

                _stderr.WriteLine(
                    $"Golden comparison failed for {summary.Failures.Count} capture(s) out of {artifactsOutput.Captures.Count}.");
                return 1;
            }

            string modeSummary = command.PixelPerfectGolden
                ? "pixel-perfect"
                : $"tolerant: MAE<={command.TolerantMaxMae:F4}, PSNR>={command.TolerantMinPsnrDb:F4}dB";
            _stdout.WriteLine($"Golden comparison passed for {summary.ComparedCount} capture(s) ({modeSummary}).");
        }

        _stdout.WriteLine($"Test artifacts created: {artifactsDirectory}");
        _stdout.WriteLine($"Test manifest created: {artifactsOutput.ManifestPath}");
        return 0;
    }

    private int HandleDoctor(DoctorCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string[] requiredPaths =
        [
            Path.Combine(projectDirectory, "project.json"),
            Path.Combine(projectDirectory, "assets", "manifest.json"),
            Path.Combine(projectDirectory, "src")
        ];

        var failures = new List<string>();
        foreach (string requiredPath in requiredPaths)
        {
            bool exists = requiredPath.EndsWith("src", StringComparison.OrdinalIgnoreCase)
                ? Directory.Exists(requiredPath)
                : File.Exists(requiredPath);
            if (!exists)
            {
                failures.Add($"Missing required path: {requiredPath}");
            }
        }

        if (!RunToolVersionCheck("dotnet", projectDirectory))
        {
            failures.Add("dotnet CLI is unavailable or returned a non-zero exit code.");
        }

        if (!RunToolVersionCheck("cmake", projectDirectory))
        {
            failures.Add("cmake is unavailable or returned a non-zero exit code.");
        }

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                _stderr.WriteLine(failure);
            }

            return 1;
        }

        _stdout.WriteLine("Doctor checks passed.");
        return 0;
    }

    private bool RunToolVersionCheck(string executable, string workingDirectory)
    {
        int exitCode = _commandRunner.Run(
            executable,
            ["--version"],
            workingDirectory,
            _stdout,
            _stderr);
        return exitCode == 0;
    }

    private int HandleApiDump(ApiDumpCommand command)
    {
        string outputPath = NativeApiDumpService.Dump(command.HeaderPath, command.OutputPath);
        _stdout.WriteLine($"Native API dump created: {outputPath}");
        return 0;
    }
}
