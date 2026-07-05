using eQuantic.UI.Material.ColorScience;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material;

/// <summary>
/// The COLOR half of a Material theme — every surface/text/chrome token and the per-variant color
/// group, as light/dark <see cref="ColorToken"/> pairs. Produced two ways: the fixed M3 BASELINE, or
/// DERIVED from a seed color via HCT tonal palettes (the M3 dynamic-color algorithm). Type, shape and
/// elevation are seed-independent and live on <see cref="MaterialTheme"/>.
/// </summary>
internal sealed class MaterialColors
{
    public required ColorToken Background { get; init; }
    public required ColorToken Surface { get; init; }
    public required ColorToken SurfaceSubtle { get; init; }
    public required ColorToken SurfaceHighlight { get; init; }
    public required ColorToken Border { get; init; }
    public required ColorToken BorderStrong { get; init; }
    public required ColorToken TextPrimary { get; init; }
    public required ColorToken TextSecondary { get; init; }
    public required ColorToken TextMuted { get; init; }
    public required ColorToken TextInverse { get; init; }
    public required ColorToken FocusRing { get; init; }
    public required ColorToken LinkColor { get; init; }
    public required ColorToken Scrim { get; init; }

    public required VariantColors Primary { get; init; }
    public required VariantColors Secondary { get; init; }
    public required VariantColors Tertiary { get; init; }
    public required VariantColors Destructive { get; init; }
    public required VariantColors Success { get; init; }
    public required VariantColors Warning { get; init; }
    public required VariantColors Info { get; init; }

    // Shared, seed-independent decorative/scrim values.
    private static readonly ColorToken Glint = new(Rgba(0xFFFFFF, 140), Rgba(0xFFFFFF, 20));
    private static readonly ColorToken ScrimToken = new(Rgba(0x000000, 82), Rgba(0x000000, 110));

    /// <summary>The fixed M3 baseline scheme (seed #6750A4) — the canonical values, transcribed. The
    /// anchor test asserts <see cref="FromSeed"/>(#6750A4) reproduces these within tolerance, proving
    /// the HCT derivation; keeping the baseline literal keeps the pinned goldens/CSS byte-stable.</summary>
    public static readonly MaterialColors Baseline = new()
    {
        Background = Token(0xFEF7FF, 0x141218),
        Surface = Token(0xF7F2FA, 0x1D1B20),
        SurfaceSubtle = Token(0xE7E0EC, 0x49454F),
        SurfaceHighlight = Glint,
        Border = Token(0xCAC4D0, 0x49454F),
        BorderStrong = Token(0x79747E, 0x938F99),
        TextPrimary = Token(0x1D1B20, 0xE6E0E9),
        TextSecondary = Token(0x49454F, 0xCAC4D0),
        TextMuted = Token(0x79747E, 0x938F99),
        TextInverse = Token(0xF5EFF7, 0x322F35),
        FocusRing = Token(0x6750A4, 0xD0BCFF),
        LinkColor = Token(0x6750A4, 0xD0BCFF),
        Scrim = ScrimToken,
        Primary = new VariantColors(Token(0x6750A4, 0xD0BCFF), Token(0xFFFFFF, 0x381E72),
            Token(0x5B4491, 0xDAC8FF), Token(0xEADDFF, 0x4F378B), Token(0x21005D, 0xEADDFF)),
        Secondary = new VariantColors(Token(0x625B71, 0xCCC2DC), Token(0xFFFFFF, 0x332D41),
            Token(0x565064, 0xD6CCE6), Token(0xE8DEF8, 0x4A4458), Token(0x1D192B, 0xE8DEF8)),
        Tertiary = new VariantColors(Token(0x7D5260, 0xEFB8C8), Token(0xFFFFFF, 0x492532),
            Token(0x6E4854, 0xF6C6D4), Token(0xFFD8E4, 0x633B48), Token(0x31111D, 0xFFD8E4)),
        Destructive = new VariantColors(Token(0xB3261E, 0xF2B8B5), Token(0xFFFFFF, 0x601410),
            Token(0x9A211A, 0xF6C7C4), Token(0xF9DEDC, 0x8C1D18), Token(0x410E0B, 0xF9DEDC)),
        Success = new VariantColors(Token(0x4C662B, 0xB1D18A), Token(0xFFFFFF, 0x1F3701),
            Token(0x415824, 0xBDDC96), Token(0xCDEDA3, 0x354E16), Token(0x102000, 0xCDEDA3)),
        Warning = new VariantColors(Token(0x6D5E0F, 0xDBC66E), Token(0xFFFFFF, 0x3A3000),
            Token(0x5F5108, 0xE6D178), Token(0xF8E287, 0x534619), Token(0x221B00, 0xF8E287)),
        Info = new VariantColors(Token(0x386A6F, 0xA0CFD4), Token(0xFFFFFF, 0x00363B),
            Token(0x2E5C61, 0xACDBE0), Token(0xBCEBF0, 0x1F4D52), Token(0x00201F, 0xBCEBF0)),
    };

    private static ColorToken Token(uint lightRgb, uint darkRgb) => new(
        Color.FromRgb((byte)(lightRgb >> 16), (byte)(lightRgb >> 8), (byte)lightRgb),
        Color.FromRgb((byte)(darkRgb >> 16), (byte)(darkRgb >> 8), (byte)darkRgb));

    /// <summary>
    /// The M3 dynamic-color scheme (TonalSpot) derived from a seed: primary/secondary/tertiary/neutral/
    /// neutral-variant palettes come from the seed's hue, error and the eQuantic Success/Warning/Info
    /// extensions from fixed hues. Roles are read from the canonical M3 tones per mode.
    /// </summary>
    public static MaterialColors FromSeed(Color seed)
    {
        var hct = Hct.FromColor(seed);
        var primary = TonalPalette.FromHueAndChroma(hct.Hue, 36.0);
        var secondary = TonalPalette.FromHueAndChroma(hct.Hue, 16.0);
        var tertiary = TonalPalette.FromHueAndChroma(ColorMath.SanitizeDegrees(hct.Hue + 60.0), 24.0);
        var neutral = TonalPalette.FromHueAndChroma(hct.Hue, 6.0);
        var neutralVariant = TonalPalette.FromHueAndChroma(hct.Hue, 8.0);
        var error = TonalPalette.FromHueAndChroma(25.0, 84.0);
        // eQuantic extensions — fixed hues toned within the M3 language.
        var success = TonalPalette.FromHueAndChroma(142.0, 40.0);
        var warning = TonalPalette.FromHueAndChroma(85.0, 55.0);
        var info = TonalPalette.FromHueAndChroma(230.0, 32.0);

        return new MaterialColors
        {
            Background = Pair(neutral, 98, 6),
            Surface = Pair(neutral, 96, 10),          // surface-container-low
            SurfaceSubtle = Pair(neutralVariant, 90, 30), // surface-variant
            SurfaceHighlight = Glint,
            Border = Pair(neutralVariant, 80, 30),    // outline-variant
            BorderStrong = Pair(neutralVariant, 50, 60), // outline
            TextPrimary = Pair(neutral, 10, 90),      // on-surface
            TextSecondary = Pair(neutralVariant, 30, 80), // on-surface-variant
            TextMuted = Pair(neutralVariant, 50, 60), // outline
            TextInverse = Pair(neutral, 95, 20),      // inverse-on-surface
            FocusRing = Pair(primary, 40, 80),
            LinkColor = Pair(primary, 40, 80),
            Scrim = ScrimToken,
            Primary = Variant(primary),
            Secondary = Variant(secondary),
            Tertiary = Variant(tertiary),
            Destructive = Variant(error),
            Success = Variant(success),
            Warning = Variant(warning),
            Info = Variant(info),
        };
    }

    /// <summary>A light/dark ColorToken from two tones of one palette.</summary>
    private static ColorToken Pair(TonalPalette p, double lightTone, double darkTone) =>
        new(p.Tone(lightTone), p.Tone(darkTone));

    /// <summary>The five-sub-token variant group from a palette, at the M3 role tones (light + dark).</summary>
    private static VariantColors Variant(TonalPalette p) => new(
        Base: new ColorToken(p.Tone(40), p.Tone(80)),
        OnBase: new ColorToken(p.Tone(100), p.Tone(20)),
        Pressed: new ColorToken(p.Tone(30), p.Tone(90)),
        Subtle: new ColorToken(p.Tone(90), p.Tone(30)),
        OnSubtle: new ColorToken(p.Tone(10), p.Tone(90)));

    private static Color Rgba(uint rgb, byte alpha) =>
        Color.FromRgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);
}
