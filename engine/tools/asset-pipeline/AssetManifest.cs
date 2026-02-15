namespace Engine.AssetPipeline;

public sealed record AssetManifest(IReadOnlyList<AssetManifestEntry> Assets);

public sealed record AssetManifestEntry(string Path, string Kind);
