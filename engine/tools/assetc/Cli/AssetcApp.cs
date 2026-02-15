using Engine.AssetPipeline;

namespace Assetc;

public sealed class AssetcApp
{
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public AssetcApp(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public int Run(string[] args)
    {
        AssetcParseResult parseResult = AssetcParser.Parse(args);
        if (!parseResult.IsSuccess)
        {
            _stderr.WriteLine(parseResult.Error);
            return 1;
        }

        try
        {
            return parseResult.Command switch
            {
                BuildAssetsCommand command => HandleBuild(command),
                ListAssetsCommand command => HandleList(command),
                _ => throw new NotSupportedException("Unsupported command type.")
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or FileNotFoundException)
        {
            _stderr.WriteLine(ex.Message);
            return 1;
        }
    }

    private int HandleBuild(BuildAssetsCommand command)
    {
        string manifestPath = Path.GetFullPath(command.ManifestPath);
        string outputPakPath = Path.GetFullPath(command.OutputPakPath);
        string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? throw new InvalidDataException("Manifest path is invalid.");
        string outputDirectory = Path.GetDirectoryName(outputPakPath) ?? throw new InvalidDataException("Output pak path is invalid.");
        string compiledRootDirectory = Path.Combine(outputDirectory, "compiled");

        AssetManifest manifest = AssetPipelineService.LoadManifest(manifestPath);
        AssetPipelineService.ValidateAssetsExist(manifest, manifestDirectory);
        IReadOnlyList<PakEntry> compiledEntries = AssetPipelineService.CompileAssets(
            manifest,
            manifestDirectory,
            compiledRootDirectory);
        AssetPipelineService.WritePak(outputPakPath, compiledEntries);

        _stdout.WriteLine($"Pak created: {outputPakPath}");
        return 0;
    }

    private int HandleList(ListAssetsCommand command)
    {
        string pakPath = Path.GetFullPath(command.PakPath);
        PakArchive archive = AssetPipelineService.ReadPak(pakPath);

        foreach (PakEntry entry in archive.Entries)
        {
            _stdout.WriteLine($"{entry.Kind}\t{entry.Path}");
        }

        return 0;
    }
}
