using System.Text;

namespace Engine.Content;

public sealed record MaterialTextureReference(
    string Slot,
    string AssetReference,
    ulong RuntimeTextureHandle = 0u)
{
    public MaterialTextureReference Validate()
    {
        if (string.IsNullOrWhiteSpace(Slot))
        {
            throw new ArgumentException("Material texture slot cannot be empty.", nameof(Slot));
        }

        if (string.IsNullOrWhiteSpace(AssetReference))
        {
            throw new ArgumentException("Material texture asset reference cannot be empty.", nameof(AssetReference));
        }

        return this;
    }
}

public sealed record MaterialBlobData(
    string TemplateId,
    byte[] ParameterBlock,
    IReadOnlyList<MaterialTextureReference> TextureReferences)
{
    public MaterialBlobData Validate()
    {
        if (string.IsNullOrWhiteSpace(TemplateId))
        {
            throw new ArgumentException("Material template id cannot be empty.", nameof(TemplateId));
        }

        ArgumentNullException.ThrowIfNull(ParameterBlock);
        ArgumentNullException.ThrowIfNull(TextureReferences);

        var seenSlots = new HashSet<string>(StringComparer.Ordinal);
        foreach (MaterialTextureReference textureReference in TextureReferences)
        {
            MaterialTextureReference validated = textureReference.Validate();
            if (!seenSlots.Add(validated.Slot))
            {
                throw new InvalidDataException(
                    $"Material texture slot '{validated.Slot}' is duplicated in material blob.");
            }
        }

        return this;
    }
}

public static class MaterialBlobCodec
{
    public const uint Magic = 0x424D4144; // DAMB
    public const uint Version = 1u;

    public static byte[] Write(MaterialBlobData blobData)
    {
        ArgumentNullException.ThrowIfNull(blobData);
        blobData = blobData.Validate();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(blobData.TemplateId);
            writer.Write(blobData.ParameterBlock.Length);
            writer.Write(blobData.ParameterBlock);
            writer.Write(blobData.TextureReferences.Count);
            foreach (MaterialTextureReference textureReference in blobData.TextureReferences)
            {
                writer.Write(textureReference.Slot);
                writer.Write(textureReference.AssetReference);
                writer.Write(textureReference.RuntimeTextureHandle);
            }
        }

        return stream.ToArray();
    }

    public static MaterialBlobData Read(ReadOnlySpan<byte> blob)
    {
        if (blob.IsEmpty)
        {
            throw new InvalidDataException("Material blob payload cannot be empty.");
        }

        using var stream = new MemoryStream(blob.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid material blob magic 0x{magic:X8}. Expected 0x{Magic:X8}.");
        }

        uint version = reader.ReadUInt32();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported material blob version {version}. Expected {Version}.");
        }

        string templateId = reader.ReadString();
        int parameterBlockLength = reader.ReadInt32();
        if (parameterBlockLength < 0)
        {
            throw new InvalidDataException("Material parameter block length cannot be negative.");
        }

        byte[] parameterBlock = reader.ReadBytes(parameterBlockLength);
        if (parameterBlock.Length != parameterBlockLength)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading material parameter block.");
        }

        int referenceCount = reader.ReadInt32();
        if (referenceCount < 0)
        {
            throw new InvalidDataException("Material texture reference count cannot be negative.");
        }

        var textureReferences = new MaterialTextureReference[referenceCount];
        for (int i = 0; i < referenceCount; i++)
        {
            textureReferences[i] = new MaterialTextureReference(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadUInt64());
        }

        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Material blob contains trailing bytes.");
        }

        return new MaterialBlobData(templateId, parameterBlock, textureReferences).Validate();
    }
}
