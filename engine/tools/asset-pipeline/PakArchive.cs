namespace Engine.AssetPipeline;

public sealed record PakArchive(int Version, DateTime CreatedAtUtc, IReadOnlyList<PakEntry> Entries);

public sealed record PakEntry(string Path, string Kind);
