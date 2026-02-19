namespace Engine.UI;

public readonly record struct UiGlyphMetrics(float Advance)
{
    public float Advance { get; } = !float.IsFinite(Advance) || Advance <= 0f
        ? throw new ArgumentOutOfRangeException(nameof(Advance), "Glyph advance must be finite and greater than zero.")
        : Advance;
}

public readonly record struct UiKerningPair(char Left, char Right);

public sealed class UiFontAtlas
{
    private readonly IReadOnlyDictionary<char, UiGlyphMetrics> _glyphs;
    private readonly IReadOnlyDictionary<int, float> _kerningAdjustments;

    public UiFontAtlas(
        float lineHeight,
        float defaultAdvance,
        IReadOnlyDictionary<char, UiGlyphMetrics>? glyphs = null,
        IReadOnlyDictionary<UiKerningPair, float>? kerningPairs = null)
    {
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(lineHeight), "Line height must be finite and greater than zero.");
        }

        if (!float.IsFinite(defaultAdvance) || defaultAdvance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultAdvance), "Default advance must be finite and greater than zero.");
        }

        if (glyphs is null || glyphs.Count == 0)
        {
            _glyphs = new Dictionary<char, UiGlyphMetrics>();
        }
        else
        {
            var normalized = new Dictionary<char, UiGlyphMetrics>(glyphs.Count);
            foreach ((char key, UiGlyphMetrics value) in glyphs)
            {
                normalized[key] = value;
            }

            _glyphs = normalized;
        }

        if (kerningPairs is null || kerningPairs.Count == 0)
        {
            _kerningAdjustments = new Dictionary<int, float>();
        }
        else
        {
            var normalizedKerning = new Dictionary<int, float>(kerningPairs.Count);
            foreach ((UiKerningPair pair, float value) in kerningPairs)
            {
                if (!float.IsFinite(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(kerningPairs), "Kerning values must be finite.");
                }

                normalizedKerning[ComposeKerningKey(pair.Left, pair.Right)] = value;
            }

            _kerningAdjustments = normalizedKerning;
        }

        LineHeight = lineHeight;
        DefaultAdvance = defaultAdvance;
    }

    public float LineHeight { get; }

    public float DefaultAdvance { get; }

    public float GetAdvance(char glyph)
    {
        return _glyphs.TryGetValue(glyph, out UiGlyphMetrics metrics)
            ? metrics.Advance
            : DefaultAdvance;
    }

    public float GetKerning(char previousGlyph, char glyph)
    {
        return _kerningAdjustments.TryGetValue(ComposeKerningKey(previousGlyph, glyph), out float adjustment)
            ? adjustment
            : 0f;
    }

    private static int ComposeKerningKey(char left, char right)
    {
        return (left << 16) | right;
    }
}
