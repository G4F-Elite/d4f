namespace Engine.Procedural;

public sealed record TextureMipLevel(int Width, int Height, byte[] Rgba8)
{
    public TextureMipLevel Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Mip dimensions must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(Rgba8);

        int expected = checked(Width * Height * 4);
        if (Rgba8.Length != expected)
        {
            throw new InvalidDataException(
                $"Mip payload size {Rgba8.Length} does not match dimensions {Width}x{Height} ({expected} bytes).");
        }

        return this;
    }
}

public sealed record ProceduralTextureSurface(
    int Width,
    int Height,
    float[] HeightMap,
    byte[] AlbedoRgba8,
    byte[] NormalRgba8,
    byte[] RoughnessRgba8,
    byte[] AmbientOcclusionRgba8,
    IReadOnlyList<TextureMipLevel> MipChain)
{
    public ProceduralTextureSurface Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Surface dimensions must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(HeightMap);
        ArgumentNullException.ThrowIfNull(AlbedoRgba8);
        ArgumentNullException.ThrowIfNull(NormalRgba8);
        ArgumentNullException.ThrowIfNull(RoughnessRgba8);
        ArgumentNullException.ThrowIfNull(AmbientOcclusionRgba8);
        ArgumentNullException.ThrowIfNull(MipChain);

        int texelCount = checked(Width * Height);
        int rgbaCount = checked(texelCount * 4);
        if (HeightMap.Length != texelCount ||
            AlbedoRgba8.Length != rgbaCount ||
            NormalRgba8.Length != rgbaCount ||
            RoughnessRgba8.Length != rgbaCount ||
            AmbientOcclusionRgba8.Length != rgbaCount)
        {
            throw new InvalidDataException("Surface maps dimensions are inconsistent.");
        }

        if (MipChain.Count == 0)
        {
            throw new InvalidDataException("Mip chain cannot be empty.");
        }

        foreach (TextureMipLevel mip in MipChain)
        {
            _ = mip.Validate();
        }

        return this;
    }
}
