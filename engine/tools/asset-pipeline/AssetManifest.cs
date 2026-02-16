namespace Engine.AssetPipeline;

public sealed record AssetManifest(int Version, IReadOnlyList<AssetManifestEntry> Assets);

public sealed record AssetManifestEntry(string Path, string Kind);
