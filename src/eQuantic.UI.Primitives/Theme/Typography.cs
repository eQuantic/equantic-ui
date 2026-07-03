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
}

/// <summary>
/// One row of the type scale: dp size, line height, weight, letter tracking, and the Dynamic Type
/// clamp (spec §02: reading roles scale fully to ×1.3; Display/Heading clamp ×1.15, Title ×1.25).
/// </summary>
public readonly record struct TypeStyle(float Size, float LineHeight, FontWeight Weight, float Tracking, float MaxScale)
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

    /// <summary>Line height under the same factor (same clamp, same snap — the line box grows with the glyphs).</summary>
    public float ScaledLineHeight(float osFactor)
    {
        var scaled = LineHeight * MathF.Min(MathF.Max(osFactor, 0.5f), MaxScale);
        return MathF.Round(scaled * 2f) / 2f;
    }
}
