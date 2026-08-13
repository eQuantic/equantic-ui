namespace eQuantic.UI.Primitives;

/// <summary>Bundled-family weights (Hanken Grotesk 400–800, spec §02).</summary>
public enum FontWeight
{
    Regular = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
}

/// <summary>The seven type roles of the scale (spec §02).</summary>
public enum TypeRole : byte
{
    Display = 0,  // hero numbers
    Heading = 1,  // screen titles
    Title = 2,    // card & section titles
    BodyL = 3,    // default reading
    BodyM = 4,    // dense UI copy
    Label = 5,    // controls, nav, chips
    Caption = 6,  // meta, timestamps

    // The DENSE end of the scale. A phone screen stops at Caption; a desktop chrome does not —
    // a sidebar, a toolbar, a status rail and an inspector all live below it, and without these
    // rungs every one of them comes out a fifth too large. (Material calls them title-small,
    // label-small and overline; Apple, subheadline / caption2 / a tracked all-caps.)
    TitleSmall = 7,   // card headers — the weight of a title at the size of body copy
    LabelSmall = 8,   // dense meta: status rails, inspector read-outs, specimen captions
    Overline = 9,     // the tracked, uppercase eyebrow over a group or a section
}

/// <summary>
/// One row of the type scale: dp size, line height, weight, letter tracking, and the Dynamic Type
/// clamp (spec §02: reading roles scale fully to ×1.3; Display/Heading clamp ×1.15, Title ×1.25).
/// </summary>
/// <param name="Mono">
/// The MONOSPACED face instead of the proportional one — code, keys, versions, anything read
/// column by column. It lives on the STYLE rather than on the node because it changes the glyphs:
/// the measurer, the rasterizer and the raster cache all key on the style, so a face that lives
/// here is honoured by all three for free — and a face that lives anywhere else is a layout that
/// disagrees with its own pixels.
/// </param>
/// <param name="Italic">
/// The SLANTED cut of the same family — emphasis inside prose, a term being defined, the citation
/// under a figure. Here for the same reason <see cref="Mono"/> is: a slant is a different set of
/// glyphs with different advances, so anything that measures without knowing about it measures a
/// paragraph the renderer will not draw.
/// <para>
/// It is an AXIS, not a weight: <c>Italic</c> composes with <see cref="Weight"/> and with
/// <see cref="Mono"/> (bold italic and italic code both exist), which is why it is a flag of its
/// own rather than a value in the weight enum.
/// </para>
/// </param>
public readonly record struct TypeStyle(float Size, float LineHeight, FontWeight Weight, float Tracking, float MaxScale, bool Mono = false, bool Italic = false)
{
    /// <summary>
    /// The effective size under an OS Dynamic Type factor: <c>Size × min(factor, MaxScale)</c>, snapped
    /// to the atlas whitelist step (0.5dp) to bound glyph memory (spec §02 engine notes).
    /// </summary>
    public float ScaledSize(float osFactor)
    {
        var scaled = Size * MathF.Min(MathF.Max(osFactor, 0.5f), MaxScale);
        return MathF.Round(scaled * 2f) / 2f;
    }

    /// <summary>
    /// The same style at another SIZE, with the line box following the ratio — what CSS's unitless
    /// `line-height` does. Patching only the size leaves a bigger glyph in the old box: a 17dp
    /// label in a 16dp line has its descender outside the line, and whoever rasterizes it has to
    /// decide whether to clip the 'g'. Nobody should have to decide that.
    /// </summary>
    public TypeStyle WithSize(float size) => Size <= 0
        ? this with { Size = size }
        : this with { Size = size, LineHeight = MathF.Round(LineHeight * size / Size * 2f) / 2f };

    /// <summary>
    /// A style from a SIZE alone, with the typographic default line box (1.25×) — the ratio that
    /// holds an ascender and a descender without either leaving the line.
    /// </summary>
    public static TypeStyle OfSize(float size, FontWeight weight, float tracking = 0f,
        float maxScale = 1.3f) =>
        new(size, MathF.Round(size * 1.25f * 2f) / 2f, weight, tracking, maxScale);

    /// <summary>Line height under the same factor (same clamp, same snap — the line box grows with the glyphs).</summary>
    public float ScaledLineHeight(float osFactor)
    {
        var scaled = LineHeight * MathF.Min(MathF.Max(osFactor, 0.5f), MaxScale);
        return MathF.Round(scaled * 2f) / 2f;
    }
}
