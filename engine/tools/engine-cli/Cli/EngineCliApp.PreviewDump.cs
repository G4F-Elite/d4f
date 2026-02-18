using System.Text.Json;
using Engine.AssetPipeline;
using Engine.Testing;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
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

        TestingArtifactEntry? galleryArtifact = manifest.Artifacts
            .FirstOrDefault(static x => string.Equals(x.Kind, "preview-gallery", StringComparison.Ordinal));
        if (galleryArtifact is not null)
        {
            string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? projectDirectory;
            string galleryPath = AssetPipelineService.ResolveRelativePath(manifestDirectory, galleryArtifact.RelativePath);
            if (File.Exists(galleryPath))
            {
                _stdout.WriteLine("Preview gallery entries:");
                foreach (PreviewGalleryEntry entry in ReadPreviewGalleryEntries(galleryPath))
                {
                    string tags = entry.Tags.Count == 0 ? "-" : string.Join(',', entry.Tags);
                    _stdout.WriteLine(
                        $"gallery\t{entry.Path}\t{entry.Kind}\t{entry.Category}\t{tags}\t{entry.PreviewPath}");
                }
            }
        }

        return 0;
    }

    private static IReadOnlyList<PreviewGalleryEntry> ReadPreviewGalleryEntries(string galleryPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(galleryPath));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Preview gallery must be a JSON array.");
        }

        var entries = new List<PreviewGalleryEntry>();
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            string[] tags = element.TryGetProperty("tags", out JsonElement tagsElement) && tagsElement.ValueKind == JsonValueKind.Array
                ? tagsElement.EnumerateArray().Select(static x => x.GetString() ?? string.Empty).Where(static x => x.Length > 0).ToArray()
                : [];
            entries.Add(
                new PreviewGalleryEntry(
                    element.GetProperty("path").GetString() ?? throw new InvalidDataException("Gallery entry 'path' is missing."),
                    element.GetProperty("kind").GetString() ?? throw new InvalidDataException("Gallery entry 'kind' is missing."),
                    element.GetProperty("category").GetString() ?? string.Empty,
                    tags,
                    element.GetProperty("previewPath").GetString() ?? throw new InvalidDataException("Gallery entry 'previewPath' is missing.")));
        }

        return entries;
    }

    private readonly record struct PreviewGalleryEntry(
        string Path,
        string Kind,
        string Category,
        IReadOnlyList<string> Tags,
        string PreviewPath);
}
