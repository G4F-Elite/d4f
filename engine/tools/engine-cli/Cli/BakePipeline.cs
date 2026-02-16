using Engine.AssetPipeline;

namespace Engine.Cli;

internal sealed record BakedContentOutput(
    string ManifestPath,
    string ManifestDirectory,
    string OutputPakPath,
    string OutputDirectory,
    string CompiledRootDirectory,
    string CompiledManifestPath,
    IReadOnlyList<PakEntry> CompiledEntries);

internal static class BakePipeline
{
    public static BakedContentOutput BakeProject(
        string projectDirectory,
        string manifestPath,
        string outputPakPath)
    {
        string fullProjectDirectory = Path.GetFullPath(projectDirectory);
        string resolvedManifestPath = AssetPipelineService.ResolveRelativePath(fullProjectDirectory, manifestPath);
        string manifestDirectory = Path.GetDirectoryName(resolvedManifestPath) ?? fullProjectDirectory;

        AssetManifest manifest = AssetPipelineService.LoadManifest(resolvedManifestPath);
        AssetPipelineService.ValidateAssetsExist(manifest, manifestDirectory);

        string resolvedOutputPakPath = AssetPipelineService.ResolveRelativePath(fullProjectDirectory, outputPakPath);
        string outputDirectory = Path.GetDirectoryName(resolvedOutputPakPath) ?? fullProjectDirectory;
        string compiledRootDirectory = Path.Combine(outputDirectory, "compiled");
        IReadOnlyList<PakEntry> compiledEntries = AssetPipelineService.CompileAssets(
            manifest,
            manifestDirectory,
            compiledRootDirectory);
        PakArchive pakArchive = AssetPipelineService.WritePak(resolvedOutputPakPath, compiledEntries);
        IReadOnlyList<PakEntry> resolvedPakEntries = pakArchive.Entries;
        string compiledManifestPath = Path.Combine(outputDirectory, AssetPipelineService.CompiledManifestFileName);
        AssetPipelineService.WriteCompiledManifest(compiledManifestPath, resolvedPakEntries);

        return new BakedContentOutput(
            resolvedManifestPath,
            manifestDirectory,
            resolvedOutputPakPath,
            outputDirectory,
            compiledRootDirectory,
            compiledManifestPath,
            resolvedPakEntries);
    }
}
