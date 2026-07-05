
namespace eQuantic.UI.Primitives;

/// <summary>
/// The default Photon theme — every value transcribed from the design system
/// (docs/design/Photon-Design-System.dc.html §01–§06; WCAG ratios verified by
/// <c>DesignTokenTests</c>). Brand anchors: eQuantic blue #0050A0 (Primary), eQuantic green #80B85C
/// (Success, darkened to clear AA in light mode).
/// </summary>
public sealed class PhotonTheme : IAppTheme
{
    public static readonly PhotonTheme Instance = new();

    // ---- Surfaces & structure (§01) -------------------------------------------------------------
    public ColorToken Background { get; } = Token(0xF5F6F8, 0x0C0F13);
    public ColorToken Surface { get; } = Token(0xFFFFFF, 0x14181E);
    public ColorToken SurfaceSubtle { get; } = Token(0xEFF1F4, 0x1C232B);
    public ColorToken SurfaceHighlight { get; } = new(Rgba(0xFFFFFF, 140), Rgba(0xFFFFFF, 18)); // 55% / 7%
    public ColorToken Border { get; } = Token(0xE2E5EA, 0x2A323D);
    public ColorToken BorderStrong { get; } = Token(0xC9CED6, 0x3D4754);

    // ---- Text tiers (§01) ------------------------------------------------------------------------
    public ColorToken TextPrimary { get; } = Token(0x171B21, 0xF2F4F7);
    public ColorToken TextSecondary { get; } = Token(0x4B5563, 0xAEB7C2);
    public ColorToken TextMuted { get; } = Token(0x5F6B7A, 0x8B95A3);
    public ColorToken TextInverse { get; } = Token(0xFFFFFF, 0x171B21);

    // ---- Interaction chrome (§01) ----------------------------------------------------------------
    public ColorToken FocusRing { get; } = Token(0x0050A0, 0x7CB5EE);
    public ColorToken LinkColor { get; } = Token(0x0050A0, 0x7CB5EE);
    public ColorToken Scrim { get; } = new(Rgba(0x0B0E12, 102), Rgba(0x000000, 143)); // 40% / 56%

    public float DisabledOpacity => 0.38f;

    // ---- Variants (§01 interactive table) --------------------------------------------------------
    private static readonly VariantColors PrimaryColors = new(
        Base: Token(0x0050A0, 0x5CA2E8),
        OnBase: Token(0xFFFFFF, 0x06263F),
        Pressed: Token(0x00427F, 0x7CB5EE),
        Subtle: Token(0xE8F1FA, 0x0F2740),
        OnSubtle: Token(0x003E7E, 0xA8CDF2));

    private static readonly VariantColors SecondaryColors = new(
        Base: Token(0xE9EDF2, 0x242C36),
        OnBase: Token(0x3A4350, 0xD7DDE5),
        Pressed: Token(0xDCE2EA, 0x2E3844),
        Subtle: Token(0xE9EDF2, 0x242C36),      // = Base (already subtle)
        OnSubtle: Token(0x3A4350, 0xD7DDE5));   // = OnBase

    private static readonly VariantColors DestructiveColors = new(
        Base: Token(0xB42318, 0xE5645C),
        OnBase: Token(0xFFFFFF, 0x3B0704),
        Pressed: Token(0x8F1D1D, 0xF28B85),
        Subtle: Token(0xFCEBEA, 0x3A1210),
        OnSubtle: Token(0x8F1D1D, 0xF4A9A4));

    private static readonly VariantColors SuccessColors = new(
        Base: Token(0x3B7A22, 0x85C05E),
        OnBase: Token(0xFFFFFF, 0x12290A),
        Pressed: Token(0x2C5E17, 0x9ACD74),
        Subtle: Token(0xEDF6E6, 0x16290C),
        OnSubtle: Token(0x2C5E17, 0xB5DB97));

    private static readonly VariantColors WarningColors = new(
        Base: Token(0x8A5A00, 0xE8B04B),
        OnBase: Token(0xFFFFFF, 0x2E1B02),
        Pressed: Token(0x6E4700, 0xF0C06B),
        Subtle: Token(0xFBF3DF, 0x2E2008),
        OnSubtle: Token(0x7A5200, 0xEECF8F));

    private static readonly VariantColors InfoColors = new(
        Base: Token(0x0C6C86, 0x4CC3DE),
        OnBase: Token(0xFFFFFF, 0x062B33),
        Pressed: Token(0x0A5468, 0x6ED0E6),
        Subtle: Token(0xE4F3F8, 0x0A2A32),
        OnSubtle: Token(0x0A5468, 0x9FDCEB));

    public VariantColors Colors(Variant variant)
    {
        var transparent = new ColorToken(Color.Transparent);
        return variant switch
        {
            Variant.Primary => PrimaryColors,
            Variant.Secondary => SecondaryColors,
            Variant.Destructive => DestructiveColors,
            Variant.Success => SuccessColors,
            Variant.Warning => WarningColors,
            Variant.Info => InfoColors,
            // Derived (§01): transparent fill + Text tokens; pressed fill = SurfaceSubtle.
            Variant.Outline or Variant.Ghost => new VariantColors(
                Base: transparent,
                OnBase: TextPrimary,
                Pressed: SurfaceSubtle,
                Subtle: transparent,
                OnSubtle: TextPrimary),
            // Link: text-only — fill stays transparent even pressed (the TEXT swaps to Primary.Pressed).
            Variant.Link => new VariantColors(
                Base: transparent,
                OnBase: LinkColor,
                Pressed: transparent,
                Subtle: transparent,
                OnSubtle: LinkColor),
            _ => PrimaryColors,
        };
    }

    // ---- Type scale (§02) ------------------------------------------------------------------------
    public TypeStyle Type(TypeRole role) => role switch
    {
        TypeRole.Display => new TypeStyle(34, 40, FontWeight.ExtraBold, -0.4f, 1.15f),
        TypeRole.Heading => new TypeStyle(28, 34, FontWeight.Bold, -0.3f, 1.15f),
        TypeRole.Title => new TypeStyle(20, 26, FontWeight.SemiBold, -0.2f, 1.25f),
        TypeRole.BodyL => new TypeStyle(17, 24, FontWeight.Regular, 0f, 1.3f),
        TypeRole.BodyM => new TypeStyle(15, 20, FontWeight.Regular, 0f, 1.3f),
        TypeRole.Label => new TypeStyle(13, 16, FontWeight.SemiBold, 0.1f, 1.3f),
        TypeRole.Caption => new TypeStyle(12, 16, FontWeight.Medium, 0.2f, 1.3f),
        _ => new TypeStyle(15, 20, FontWeight.Regular, 0f, 1.3f),
    };

    // ---- Elevation (§05) -------------------------------------------------------------------------
    // Light: #0F1720 at the listed alpha; dark: pure black, alphas from the spec's concrete values
    // (shadows must fight darker canvases). Exactly one shadow per node.
    public ShadowSpec Elevation(int level) => level switch
    {
        <= 0 => new ShadowSpec(0, 0, 0, new ColorToken(Color.Transparent)),
        1 => new ShadowSpec(1, 3, 0, Shadow(0.10f, 0.45f)),
        2 => new ShadowSpec(2, 8, 0, Shadow(0.12f, 0.48f)),
        3 => new ShadowSpec(6, 16, -2, Shadow(0.16f, 0.52f)),
        4 => new ShadowSpec(12, 28, -4, Shadow(0.20f, 0.56f)),
        _ => new ShadowSpec(20, 44, -6, Shadow(0.26f, 0.62f)),
    };

    private static ColorToken Shadow(float lightAlpha, float darkAlpha) => new(
        Rgba(0x0F1720, (byte)MathF.Round(lightAlpha * 255)),
        Rgba(0x000000, (byte)MathF.Round(darkAlpha * 255)));

    // ---- helpers ----------------------------------------------------------------------------------
    private static ColorToken Token(uint lightRgb, uint darkRgb) => new(Rgb(lightRgb), Rgb(darkRgb));

    private static Color Rgb(uint rgb) =>
        Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color Rgba(uint rgb, byte alpha) =>
        Color.FromRgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, alpha);
}
