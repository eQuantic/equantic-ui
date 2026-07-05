using eQuantic.UI.Primitives;

namespace eQuantic.UI.Material;

/// <summary>
/// Material Design 3 as a write-once <see cref="IAppTheme"/> — the M3 BASELINE scheme (the default
/// dynamic-color seed #6750A4) transcribed into the target-neutral token contract. Because it is just
/// an <see cref="IAppTheme"/>, it themes every shared component on BOTH targets (web realizer → DOM+CSS,
/// native realizer → Photon pixels) with no per-component code — exactly the surface a developer uses to
/// brand eQuantic.UI. M3 role → eQuantic token mapping:
/// primary/secondary/tertiary/error → the corresponding <see cref="Variant"/> (Base/OnBase =
/// role/on-role, Subtle/OnSubtle = container/on-container); surfaces/outline/on-surface → the surface,
/// border and text tiers; the M3 type scale → <see cref="TypeRole"/>; the M3 shape scale →
/// <see cref="ShapeScale"/>. Success/Warning/Info are eQuantic extensions toned within the M3 language.
/// v1: fixed baseline; dynamic color from a seed (HCT tonal palettes) is a later slice.
/// </summary>
public sealed class MaterialTheme : IAppTheme
{
    public static readonly MaterialTheme Instance = new();

    // ---- Surfaces & structure (M3 surface roles) ------------------------------------------------
    // Background = M3 surface (the page); Surface = surface-container-low (cards sit above);
    // SurfaceSubtle = surface-variant (muted fills — tracks, search fields, segments).
    public ColorToken Background { get; } = Token(0xFEF7FF, 0x141218);
    public ColorToken Surface { get; } = Token(0xF7F2FA, 0x1D1B20);
    public ColorToken SurfaceSubtle { get; } = Token(0xE7E0EC, 0x49454F);
    public ColorToken SurfaceHighlight { get; } = new(Rgba(0xFFFFFF, 140), Rgba(0xFFFFFF, 20)); // decorative glint
    public ColorToken Border { get; } = Token(0xCAC4D0, 0x49454F);       // M3 outline-variant
    public ColorToken BorderStrong { get; } = Token(0x79747E, 0x938F99); // M3 outline

    // ---- Text tiers (M3 on-surface roles) --------------------------------------------------------
    public ColorToken TextPrimary { get; } = Token(0x1D1B20, 0xE6E0E9);   // on-surface
    public ColorToken TextSecondary { get; } = Token(0x49454F, 0xCAC4D0); // on-surface-variant
    public ColorToken TextMuted { get; } = Token(0x79747E, 0x938F99);     // outline (placeholders/meta)
    public ColorToken TextInverse { get; } = Token(0xF5EFF7, 0x322F35);   // inverse-on-surface

    // ---- Interaction chrome ----------------------------------------------------------------------
    public ColorToken FocusRing { get; } = Token(0x6750A4, 0xD0BCFF); // primary
    public ColorToken LinkColor { get; } = Token(0x6750A4, 0xD0BCFF); // primary
    public ColorToken Scrim { get; } = new(Rgba(0x000000, 82), Rgba(0x000000, 110)); // M3 scrim 32% / 43%

    public float DisabledOpacity => 0.38f;

    // ---- Variants: M3 role groups (Base/OnBase = role/on-role, Subtle/OnSubtle = container) -------
    private static readonly VariantColors PrimaryColors = new(
        Base: Token(0x6750A4, 0xD0BCFF),
        OnBase: Token(0xFFFFFF, 0x381E72),
        Pressed: Token(0x5B4491, 0xDAC8FF),
        Subtle: Token(0xEADDFF, 0x4F378B),
        OnSubtle: Token(0x21005D, 0xEADDFF));

    private static readonly VariantColors SecondaryColors = new(
        Base: Token(0x625B71, 0xCCC2DC),
        OnBase: Token(0xFFFFFF, 0x332D41),
        Pressed: Token(0x565064, 0xD6CCE6),
        Subtle: Token(0xE8DEF8, 0x4A4458),
        OnSubtle: Token(0x1D192B, 0xE8DEF8));

    private static readonly VariantColors TertiaryColors = new(
        Base: Token(0x7D5260, 0xEFB8C8),
        OnBase: Token(0xFFFFFF, 0x492532),
        Pressed: Token(0x6E4854, 0xF6C6D4),
        Subtle: Token(0xFFD8E4, 0x633B48),
        OnSubtle: Token(0x31111D, 0xFFD8E4));

    private static readonly VariantColors DestructiveColors = new(
        Base: Token(0xB3261E, 0xF2B8B5),
        OnBase: Token(0xFFFFFF, 0x601410),
        Pressed: Token(0x9A211A, 0xF6C7C4),
        Subtle: Token(0xF9DEDC, 0x8C1D18),
        OnSubtle: Token(0x410E0B, 0xF9DEDC));

    // Success / Warning / Info: eQuantic extensions, toned within the M3 language (tonal palettes).
    private static readonly VariantColors SuccessColors = new(
        Base: Token(0x4C662B, 0xB1D18A),
        OnBase: Token(0xFFFFFF, 0x1F3701),
        Pressed: Token(0x415824, 0xBDDC96),
        Subtle: Token(0xCDEDA3, 0x354E16),
        OnSubtle: Token(0x102000, 0xCDEDA3));

    private static readonly VariantColors WarningColors = new(
        Base: Token(0x6D5E0F, 0xDBC66E),
        OnBase: Token(0xFFFFFF, 0x3A3000),
        Pressed: Token(0x5F5108, 0xE6D178),
        Subtle: Token(0xF8E287, 0x534619),
        OnSubtle: Token(0x221B00, 0xF8E287));

    private static readonly VariantColors InfoColors = new(
        Base: Token(0x386A6F, 0xA0CFD4),
        OnBase: Token(0xFFFFFF, 0x00363B),
        Pressed: Token(0x2E5C61, 0xACDBE0),
        Subtle: Token(0xBCEBF0, 0x1F4D52),
        OnSubtle: Token(0x00201F, 0xBCEBF0));

    public VariantColors Colors(Variant variant)
    {
        var transparent = new ColorToken(Color.Transparent);
        return variant switch
        {
            Variant.Primary => PrimaryColors,
            Variant.Secondary => SecondaryColors,
            Variant.Tertiary => TertiaryColors,
            Variant.Destructive => DestructiveColors,
            Variant.Success => SuccessColors,
            Variant.Warning => WarningColors,
            Variant.Info => InfoColors,
            // Derived (transparent chrome): fill transparent, content = text, pressed = surface-variant.
            Variant.Outline or Variant.Ghost => new VariantColors(
                Base: transparent, OnBase: TextPrimary, Pressed: SurfaceSubtle,
                Subtle: transparent, OnSubtle: TextPrimary),
            Variant.Link => new VariantColors(
                Base: transparent, OnBase: LinkColor, Pressed: transparent,
                Subtle: transparent, OnSubtle: LinkColor),
            _ => PrimaryColors,
        };
    }

    // ---- Type scale — the M3 type scale mapped onto the eQuantic roles ----------------------------
    public TypeStyle Type(TypeRole role) => role switch
    {
        TypeRole.Display => new TypeStyle(45, 52, FontWeight.Regular, 0f, 1.2f),   // display-medium
        TypeRole.Heading => new TypeStyle(28, 36, FontWeight.Regular, 0f, 1.25f),  // headline-medium
        TypeRole.Title => new TypeStyle(22, 28, FontWeight.Regular, 0f, 1.3f),     // title-large
        TypeRole.BodyL => new TypeStyle(16, 24, FontWeight.Regular, 0.5f, 1.4f),   // body-large
        TypeRole.BodyM => new TypeStyle(14, 20, FontWeight.Regular, 0.25f, 1.4f),  // body-medium
        TypeRole.Label => new TypeStyle(14, 20, FontWeight.Medium, 0.1f, 1.3f),    // label-large
        TypeRole.Caption => new TypeStyle(12, 16, FontWeight.Regular, 0.4f, 1.3f), // body-small
        _ => new TypeStyle(14, 20, FontWeight.Regular, 0.25f, 1.4f),
    };

    // ---- Shape — the M3 shape scale --------------------------------------------------------------
    public float Shape(ShapeScale scale) => scale switch
    {
        ShapeScale.None => 0,
        ShapeScale.ExtraSmall => 4,
        ShapeScale.Small => 8,
        ShapeScale.Medium => 12,
        ShapeScale.Large => 16,
        ShapeScale.ExtraLarge => 28,
        _ => 999, // Full — engine-clamped
    };

    // ---- Elevation — the M3 levels (0/1/3/6/8/12 dp), soft single-shadow approximation -----------
    public ShadowSpec Elevation(int level) => level switch
    {
        <= 0 => new ShadowSpec(0, 0, 0, new ColorToken(Color.Transparent)),
        1 => new ShadowSpec(1, 3, 0, Shadow(0.15f, 0.36f)),
        2 => new ShadowSpec(2, 6, 0, Shadow(0.16f, 0.40f)),
        3 => new ShadowSpec(4, 8, 0, Shadow(0.18f, 0.44f)),
        4 => new ShadowSpec(6, 10, 0, Shadow(0.20f, 0.48f)),
        _ => new ShadowSpec(8, 12, 0, Shadow(0.22f, 0.52f)),
    };

    // ---- helpers (identical shape to PhotonTheme) ------------------------------------------------
    private static ColorToken Shadow(float lightAlpha, float darkAlpha) => new(
        Rgba(0x000000, (byte)MathF.Round(lightAlpha * 255)),
        Rgba(0x000000, (byte)MathF.Round(darkAlpha * 255)));

    private static ColorToken Token(uint lightRgb, uint darkRgb) => new(Rgb(lightRgb), Rgb(darkRgb));

    private static Color Rgb(uint rgb) =>
        Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color Rgba(uint rgb, byte alpha) =>
        Color.FromRgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);
}
