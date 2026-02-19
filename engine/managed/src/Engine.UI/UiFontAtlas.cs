namespace Engine.UI;

public readonly record struct UiGlyphMetrics(float Advance)
{
    public float Advance { get; } = !float.IsFinite(Advance) || Advance <= 0f
        ? throw new ArgumentOutOfRangeException(nameof(Advance), "Glyph advance must be finite and greater than zero.")
        : Advance;
}

public sealed class UiFontAtlas
{
    private readonly IReadOnlyDictionary<char, UiGlyphMetrics> _glyphs;

    public UiFontAtlas(float lineHeight, float defaultAdvance, IReadOnlyDictionary<char, UiGlyphMetrics>? glyphs = null)
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
}
