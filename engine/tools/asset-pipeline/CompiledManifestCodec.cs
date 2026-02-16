using System.Text;

namespace Engine.AssetPipeline;

public static class CompiledManifestCodec
{
    public const uint Magic = 0x4D464644; // DFFM
    public const uint Version = 2;

    public static void Write(string outputPath, IReadOnlyList<PakEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(entries);

        string directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(outputPath);
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(entries.Count);
        foreach (PakEntry entry in entries)
        {
            writer.Write(entry.Path);
            writer.Write(entry.Kind);
            writer.Write(entry.CompiledPath);
            writer.Write(entry.SizeBytes);
            writer.Write(entry.OffsetBytes);
            writer.Write(entry.AssetKey);
        }
    }

    public static IReadOnlyList<PakEntry> Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Compiled manifest file was not found: {inputPath}", inputPath);
        }

        using FileStream stream = File.OpenRead(inputPath);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid compiled manifest magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != 1u && version != Version)
        {
            throw new InvalidDataException($"Unsupported compiled manifest version {version}. Expected 1 or {Version}.");
        }

        int entryCount = reader.ReadInt32();
        if (entryCount < 0)
        {
            throw new InvalidDataException($"Compiled manifest entry count cannot be negative: {entryCount}.");
        }

        var entries = new List<PakEntry>(entryCount);
        for (int i = 0; i < entryCount; i++)
        {
            string path = reader.ReadString();
            string kind = reader.ReadString();
            string compiledPath = reader.ReadString();
            long sizeBytes = reader.ReadInt64();
            long offsetBytes = 0;
            string assetKey = string.Empty;
            if (version >= 2u)
            {
                offsetBytes = reader.ReadInt64();
                assetKey = reader.ReadString();
            }

            if (string.IsNullOrWhiteSpace(assetKey))
            {
                assetKey = PakEntryKeyBuilder.Compute(path, kind, compiledPath, sizeBytes);
            }

            entries.Add(new PakEntry(path, kind, compiledPath, sizeBytes, offsetBytes, assetKey));
        }

        return entries;
    }
}
