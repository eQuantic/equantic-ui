namespace eQuantic.UI.Primitives;

/// <summary>
/// The curated glyph set (spec A10) — every icon the design system's components reference. Glyphs
/// are single-path alpha masks drawn in a 24×24 em-box (the ~2dp optical bleed lives in the path
/// data); outline ↔ filled are DISTINCT glyphs, never a style toggle. The set grows by adding a
/// member here plus its path in the web IconRegistry — the TS side is generated, never hand-edited.
/// </summary>
public enum Icons : byte
{
    Search = 0,
    Close = 1,
    Check = 2,
    CheckCircle = 3,
    Info = 4,
    Warning = 5,
    Error = 6,
    Person = 7,
    ChevronLeft = 8,
    ChevronRight = 9,
    ChevronUp = 10,
    ChevronDown = 11,
    Mail = 12,
    Notifications = 13,
    Heart = 14,
    HeartFilled = 15,
}

/// <summary>
/// The Icon node (spec A10): a square em-box glyph tinted by a single <see cref="ColorToken"/>
/// (alpha-mask semantics — it tints like text). Sizes come from the §07 whitelist ONLY
/// (16/20/24/32); anything else throws at construction — the spec calls arbitrary sizes an error,
/// and the native glyph atlas depends on the whitelist. Icons ignore Dynamic Type. Native v1
/// renders a tinted placeholder disc until the W4 atlas (the documented text-bars pattern).
/// </summary>
public sealed class Icon : VisualNode
{
    public sealed override string NodeKind => "icon";

    /// <summary>A curated glyph (spec A10) — resolves through <see cref="CuratedIcons"/>.</summary>
    public Icon(Icons glyph, float size = 24, ColorToken? color = null, string? label = null)
        : this(CuratedIcons.Resolve(glyph), size, color, label)
    {
    }

    /// <summary>Any pack glyph (write-once icon packs are catalogs of <see cref="IconGlyph"/>).</summary>
    public Icon(IconGlyph glyph, float size = 24, ColorToken? color = null, string? label = null)
    {
        if (size is not (16 or 20 or 24 or 32))
            throw new ArgumentOutOfRangeException(nameof(size),
                $"Icon size {size} is not on the §07 whitelist (16/20/24/32 — IconSize.Sm/Dense/Md/Lg).");
        Glyph = glyph;
        Size = size;
        Color = color;
        Label = label;
    }

    /// <summary>The RESOLVED target-neutral glyph data (curated or from any pack).</summary>
    public IconGlyph Glyph { get; }

    public float Size { get; }

    /// <summary>Tint token; null inherits the context text color (like Text).</summary>
    public ColorToken? Color { get; init; }

    /// <summary>Accessibility label; null = decorative (aria-hidden on web).</summary>
    public string? Label { get; init; }
}
