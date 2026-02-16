using System.Text;

namespace Engine.AssetPipeline;

internal static class PakBinaryCodec
{
    private const uint Magic = 0x50464644; // DFFP
    private const int Version = 3;
    private const int HeaderSizeBytes = sizeof(uint) + sizeof(uint) + sizeof(int) + sizeof(uint) + sizeof(long);

    public static PakArchive WriteFromCompiledEntries(string outputPakPath, IReadOnlyList<PakEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPakPath);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new InvalidDataException("Pak must contain at least one entry.");
        }

        string fullOutputPath = Path.GetFullPath(outputPakPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        string compiledRootDirectory = Path.Combine(outputDirectory, "compiled");
        var sourceItems = new List<PakEntrySource>(entries.Count);
        foreach (PakEntry entry in entries)
        {
            string compiledPath = Path.GetFullPath(
                Path.Combine(compiledRootDirectory, entry.CompiledPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(compiledPath))
            {
                throw new FileNotFoundException($"Compiled asset file was not found: {compiledPath}", compiledPath);
            }

            long sizeBytes = new FileInfo(compiledPath).Length;
            string assetKey = string.IsNullOrWhiteSpace(entry.AssetKey)
                ? PakEntryKeyBuilder.Compute(entry.Path, entry.Kind, entry.CompiledPath, sizeBytes)
                : entry.AssetKey;
            sourceItems.Add(
                new PakEntrySource(
                    new PakEntry(entry.Path, entry.Kind, entry.CompiledPath, sizeBytes, 0, assetKey),
                    compiledPath));
        }

        return WriteInternal(fullOutputPath, sourceItems, metadataOnly: false);
    }

    public static PakArchive WriteMetadataOnly(string outputPakPath, IReadOnlyList<PakEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPakPath);
        ArgumentNullException.ThrowIfNull(entries);

        string fullOutputPath = Path.GetFullPath(outputPakPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var sourceItems = new List<PakEntrySource>(entries.Count);
        foreach (PakEntry entry in entries)
        {
            string assetKey = string.IsNullOrWhiteSpace(entry.AssetKey)
                ? PakEntryKeyBuilder.Compute(entry.Path, entry.Kind, entry.CompiledPath, 0)
                : entry.AssetKey;
            sourceItems.Add(
                new PakEntrySource(
                    new PakEntry(entry.Path, entry.Kind, entry.CompiledPath, 0, 0, assetKey),
                    FullCompiledPath: null));
        }

        return WriteInternal(fullOutputPath, sourceItems, metadataOnly: true);
    }

    public static PakArchive Read(string inputPakPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPakPath);
        string fullInputPath = Path.GetFullPath(inputPakPath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException($"Pak file was not found: {fullInputPath}", fullInputPath);
        }

        using FileStream stream = File.OpenRead(fullInputPath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid pak magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported pak version {version}. Expected {Version}.");
        }

        int entryCount = reader.ReadInt32();
        if (entryCount < 0)
        {
            throw new InvalidDataException($"Pak entry count cannot be negative: {entryCount}.");
        }

        _ = reader.ReadUInt32(); // reserved.
        long createdAtTicks = reader.ReadInt64();
        DateTime createdAtUtc = DateTime.UnixEpoch;
        if (createdAtTicks > 0)
        {
            createdAtUtc = new DateTime(createdAtTicks, DateTimeKind.Utc);
        }

        var entries = new List<PakEntry>(entryCount);
        for (int i = 0; i < entryCount; i++)
        {
            string path = reader.ReadString();
            string kind = reader.ReadString();
            string compiledPath = reader.ReadString();
            string assetKey = reader.ReadString();
            long offsetBytes = reader.ReadInt64();
            long sizeBytes = reader.ReadInt64();

            if (offsetBytes < 0 || sizeBytes < 0)
            {
                throw new InvalidDataException($"Pak entry '{path}' has negative offset or size.");
            }

            if (string.IsNullOrWhiteSpace(assetKey))
            {
                assetKey = PakEntryKeyBuilder.Compute(path, kind, compiledPath, sizeBytes);
            }

            entries.Add(new PakEntry(path, kind, compiledPath, sizeBytes, offsetBytes, assetKey));
        }

        long fileLength = stream.Length;
        foreach (PakEntry entry in entries)
        {
            if (entry.SizeBytes == 0)
            {
                continue;
            }

            long end = checked(entry.OffsetBytes + entry.SizeBytes);
            if (end > fileLength)
            {
                throw new InvalidDataException(
                    $"Pak entry '{entry.Path}' points outside file bounds ({entry.OffsetBytes}+{entry.SizeBytes}>{fileLength}).");
            }
        }

        return new PakArchive(Version, createdAtUtc, entries);
    }

    private static PakArchive WriteInternal(
        string fullOutputPath,
        IReadOnlyList<PakEntrySource> sourceItems,
        bool metadataOnly)
    {
        DateTime createdAtUtc = DateTime.UtcNow;
        int indexSize = ComputeIndexSize(sourceItems);
        long nextOffset = checked(HeaderSizeBytes + indexSize);

        var resolvedEntries = new List<PakEntry>(sourceItems.Count);
        for (int i = 0; i < sourceItems.Count; i++)
        {
            PakEntry baseEntry = sourceItems[i].Entry;
            long offset = metadataOnly || baseEntry.SizeBytes == 0 ? 0 : nextOffset;
            resolvedEntries.Add(baseEntry with { OffsetBytes = offset });
            if (!metadataOnly)
            {
                nextOffset = checked(nextOffset + baseEntry.SizeBytes);
            }
        }

        using FileStream output = File.Create(fullOutputPath);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write((uint)Version);
        writer.Write(resolvedEntries.Count);
        writer.Write(0u); // reserved.
        writer.Write(createdAtUtc.Ticks);

        foreach (PakEntry entry in resolvedEntries)
        {
            writer.Write(entry.Path);
            writer.Write(entry.Kind);
            writer.Write(entry.CompiledPath);
            writer.Write(entry.AssetKey);
            writer.Write(entry.OffsetBytes);
            writer.Write(entry.SizeBytes);
        }

        if (!metadataOnly)
        {
            foreach ((PakEntry entry, string? fullCompiledPath) in sourceItems)
            {
                if (entry.SizeBytes == 0)
                {
                    continue;
                }

                if (fullCompiledPath is null)
                {
                    throw new InvalidDataException($"Missing compiled source for asset '{entry.Path}'.");
                }

                using FileStream input = File.OpenRead(fullCompiledPath);
                input.CopyTo(output);
            }
        }

        return new PakArchive(Version, createdAtUtc, resolvedEntries);
    }

    private static int ComputeIndexSize(IReadOnlyList<PakEntrySource> sourceItems)
    {
        int size = 0;
        foreach (PakEntrySource item in sourceItems)
        {
            PakEntry entry = item.Entry;
            size = checked(size + GetSerializedStringSize(entry.Path));
            size = checked(size + GetSerializedStringSize(entry.Kind));
            size = checked(size + GetSerializedStringSize(entry.CompiledPath));
            size = checked(size + GetSerializedStringSize(entry.AssetKey));
            size = checked(size + sizeof(long) + sizeof(long));
        }

        return size;
    }

    private static int GetSerializedStringSize(string value)
    {
        int byteLength = Encoding.UTF8.GetByteCount(value);
        return checked(Get7BitEncodedIntByteCount(byteLength) + byteLength);
    }

    private static int Get7BitEncodedIntByteCount(int value)
    {
        uint remaining = checked((uint)value);
        int count = 0;
        do
        {
            count++;
            remaining >>= 7;
        }
        while (remaining != 0);

        return count;
    }

    private sealed record PakEntrySource(PakEntry Entry, string? FullCompiledPath);
}
