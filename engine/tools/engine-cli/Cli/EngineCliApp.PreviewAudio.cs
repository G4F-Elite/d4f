using Engine.AssetPipeline;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandlePreviewAudio(PreviewAudioCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string outputDirectory = AssetPipelineService.ResolveRelativePath(projectDirectory, command.OutputDirectory);
        string previewPakPath = Path.Combine(outputDirectory, "preview-audio-content.pak");
        BakedContentOutput baked = BakePipeline.BakeProject(
            projectDirectory,
            command.ManifestPath,
            previewPakPath);
        string artifactsManifestPath = PreviewArtifactGenerator.GenerateAudioOnly(outputDirectory, baked.CompiledEntries);

        _stdout.WriteLine($"Audio preview artifacts created: {outputDirectory}");
        _stdout.WriteLine($"Audio preview manifest created: {artifactsManifestPath}");
        return 0;
    }
}
