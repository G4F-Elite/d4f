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
        AssetPipelineService.WritePak(resolvedOutputPakPath, compiledEntries);
        string compiledManifestPath = Path.Combine(outputDirectory, AssetPipelineService.CompiledManifestFileName);
        AssetPipelineService.WriteCompiledManifest(compiledManifestPath, compiledEntries);

        return new BakedContentOutput(
            resolvedManifestPath,
            manifestDirectory,
            resolvedOutputPakPath,
            outputDirectory,
            compiledRootDirectory,
            compiledManifestPath,
            compiledEntries);
    }
}
