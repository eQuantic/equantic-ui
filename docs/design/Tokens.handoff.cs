// eQuantic.UI — Photon token core (Layer 1), mirrors the shipped SDK value-for-value.
// Values are platform-neutral: dp on mobile, px on web.
// Layer 1 is shared by web AND native — it lives in eQuantic.UI.Primitives, never under .Native.
// The theme is MODE-FREE: nothing here knows whether the user is in light or dark.
// The web realizer emits light-dark(light, dark) so a page renders once for both modes;
// the native realizer resolves the mode when it paints. Colors resolve as token.Resolve(ThemeMode).

namespace eQuantic.UI.Primitives;

// ─── Color ───────────────────────────────────────────────────────────────────

/// <summary>The colour primitive. Alpha is a BYTE, not a float.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);
    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    public Color WithOpacity(float opacity) => this with { A = (byte)(opacity * 255f + 0.5f) };

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0, 255);
    public static readonly Color White = new(255, 255, 255, 255);
}

public enum ThemeMode { Light, Dark }

/// <summary>A light/dark pair. Resolved by the realizer at paint time — never by the theme.</summary>
public readonly record struct ColorToken(Color Light, Color Dark)
{
    /// <summary>Same colour in both modes.</summary>
    public ColorToken(Color both) : this(both, both) { }

    public Color Resolve(ThemeMode mode) => mode == ThemeMode.Dark ? Dark : Light;

    public ColorToken WithOpacity(float opacity) =>
        new(Light.WithOpacity(opacity), Dark.WithOpacity(opacity));
}

public readonly record struct VariantColors(
    ColorToken Base, ColorToken OnBase, ColorToken Pressed, ColorToken Subtle, ColorToken OnSubtle);

// HOVER (v1.1 audit) — pointer-only ninth state, DERIVED; there is NO sixth tuple slot:
//   · filled variants: hover fill = midpoint(Base, Pressed) — web emits color-mix(in srgb, Base 50%, Pressed);
//     native desktop computes the same midpoint at paint. Text stays OnBase.
//   · quiet variants (Outline/Ghost): hover fill = SurfaceSubtle (the fill Pressed already uses).
//   · Link: underline on hover, color unchanged.
//   Hover never fires on touch; it clears when the pointer leaves the window; in/out = Motion.Press.
//   LANDED: VariantColors.Hover derives Base→Pressed at the token level (ColorToken.MidpointWith),
//   so both realizers paint the identical value and neither computes it at the call site. Read the
//   helper rather than re-deriving the midpoint; the five-tuple is unchanged — Hover is derived,
//   never a sixth slot.

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum Variant : byte
{
    Primary = 0, Secondary = 1, Destructive = 2, Outline = 3, Ghost = 4,
    Link = 5, Success = 6, Warning = 7, Info = 8, Tertiary = 9
}

/// <summary>Control size ladder. Named SizeVariant so it never collides with SizeValue (layout sizing).</summary>
public enum SizeVariant { Small, Medium, Large, XLarge }

/// <summary>How tight controls are — a property of the TARGET, never of the call site (v1.1 audit).
/// Touch keeps the §08 hit contract; pointer tightens chrome. Threads through Sizing.* and
/// ButtonStyles.Metrics; components read context.Density.</summary>
public enum Density : byte { Comfortable = 0, Compact = 1 }

public enum TypeRole : byte
{
    Display = 0, Heading = 1, Title = 2, BodyL = 3, BodyM = 4, Label = 5, Caption = 6,
    // v1.1 — the DENSE desktop rungs below Caption (sidebars, toolbars, status rails, inspectors):
    TitleSmall = 7, LabelSmall = 8, Overline = 9,
}
public enum ShapeScale { None, ExtraSmall, Small, Medium, Large, ExtraLarge, Full }
public enum FontWeight { Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, ExtraBold = 800 }

// ─── Type, shadow, motion ────────────────────────────────────────────────────

/// <param name="Mono">v1.1 — the monospaced face instead of the proportional one. Lives on the STYLE:
/// the measurer, the rasterizer and the raster cache all key on it.</param>
public readonly record struct TypeStyle(
    float Size, float LineHeight, FontWeight Weight, float Tracking, float MaxScale, bool Mono = false);

public readonly record struct ShadowSpec(float OffsetY, float Blur, float Spread, ColorToken Color);

/// <summary>Cubic bezier. Three named curves. (SpringSpec exists as a type below — no curve consumes it.)</summary>
public readonly record struct Curve(float X1, float Y1, float X2, float Y2)
{
    public static readonly Curve Standard   = new(0.2f, 0f, 0f, 1f);
    public static readonly Curve Decelerate = new(0f, 0f, 0f, 1f);
    public static readonly Curve Accelerate = new(0.3f, 0f, 1f, 1f);
}

/// <summary>Spring parameters for gesture releases — SHIPPED as a type (v1.1 audit) but consumed by
/// NOTHING: no realizer reads it; gesture release still runs the 200ms smoothstep Release glide.
/// Behavior stays REQUEST — do not spec springs yet.</summary>
public readonly record struct SpringSpec(float Stiffness, float Damping, float Mass)
{
    public static readonly SpringSpec Default = new(380, 34, 1);
}

public readonly record struct MotionSpec(int DurationMs, Curve Curve);

// Motion is NAMED. Spec animations as roles ("Motion.Enter"), never as raw milliseconds.
// The duration scale is 100 / 200 / 300 only — nothing sits between the rungs.
public static class Motion
{
    public const int FastMs = 100, BaseMs = 200, SlowMs = 300;

    public static readonly MotionSpec Press = new(FastMs, Curve.Standard);    // pressed / hover feedback
    public static readonly MotionSpec State = new(BaseMs, Curve.Standard);    // state swaps
    public static readonly MotionSpec Enter = new(SlowMs, Curve.Decelerate);  // things arriving
    public static readonly MotionSpec Exit  = new(BaseMs, Curve.Accelerate);  // things leaving (= ExitFor(SlowMs))

    /// <summary>Exit pairing for a non-standard enter duration: two thirds, accelerating (integer math).</summary>
    public static MotionSpec ExitFor(int enterMs) => new(enterMs * 2 / 3, Curve.Accelerate);

    public const int ReducedCrossfadeMs = 120;
}

// GESTURE RELEASE (v1.1) — the sanctioned interim is the RELEASE GLIDE: on release, pick the
// target from position + velocity, then glide there over Motion.BaseMs (200ms) with smoothstep
// interpolation. It lives in the gesture system, deliberately NOT a Curve constant.
// SpringSpec now SHIPS as a type (above) but is consumed by NOTHING — no realizer reads it; the
// spring BEHAVIOR remains the TARGET (REQUEST). Curve.Spring and Motion.Settle still do not exist.

public enum StyleChannels : byte
{
    None = 0, Colors = 1, Opacity = 2, Transform = 4, Shadow = 8, Filters = 16, Size = 32,
    All = Colors | Opacity | Transform | Shadow | Filters | Size    // 63
}
// Filters = element + backdrop blur. Size = the width/height/max-bounds morph channel.

public readonly record struct TransitionSpec(
    StyleChannels Channels, float DurationMs = Motion.BaseMs, float DelayMs = 0)
{
    public Curve Easing { get; init; } = Curve.Standard;

    public static TransitionSpec Of(StyleChannels channels, MotionSpec motion) =>
        new(channels, motion.DurationMs) { Easing = motion.Curve };

    public static TransitionSpec Colors(float durationMs = Motion.FastMs) =>
        new(StyleChannels.Colors, durationMs);

    public static TransitionSpec All(float durationMs = Motion.BaseMs) =>
        new(StyleChannels.All, durationMs);
}

// ─── Scales ──────────────────────────────────────────────────────────────────

public static class Space
{
    public const float S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 20, S6 = 24, S8 = 32, S10 = 40, S12 = 48, S16 = 64;
    /// <summary>Gutter between grid columns. There is no margin property — parents own spacing.</summary>
    public const float Gutter = S4;
}

public static class Radius
{
    public const float Xs = 4, Sm = 6, Md = 10, Lg = 14, Xl = 20, Full = 999;
}

// The same four-step ladder as the SDK — do not rename: Md is 24, the dense step is 20.
public static class IconSize
{
    public const float Sm = 16, Dense = 20, Md = 24, Lg = 32;
}

public static class Touch
{
    /// <summary>iOS 44pt / Android 48dp — the engine enforces the stricter value on both.</summary>
    public const float MinTarget = 48;
    /// <summary>Drag past this distance and the press cancels without firing.</summary>
    public const float PressCancelSlop = 12;
}

// The control ladder. EVERY control — button, field, select, menu row, chip,
// segmented control, stepper — reads these instead of carrying its own numbers.
public static class Sizing
{
    public static float Height(SizeVariant s, Density density = Density.Comfortable) => s switch
    {
        SizeVariant.Small  => density == Density.Compact ? 26 : 32,
        SizeVariant.Medium => density == Density.Compact ? 32 : 40,
        SizeVariant.Large  => density == Density.Compact ? 40 : 48,
        _                  => density == Density.Compact ? 48 : 56,
    };

    public static float PaddingX(SizeVariant s, Density density = Density.Comfortable) => s switch
    {
        SizeVariant.Small  => density == Density.Compact ? Space.S2 : Space.S3,
        SizeVariant.Medium => density == Density.Compact ? Space.S3 : Space.S4,
        SizeVariant.Large  => density == Density.Compact ? Space.S4 : Space.S5,
        _                  => density == Density.Compact ? Space.S5 : Space.S6,
    };

    public static float Gap(SizeVariant s) => s switch
    {
        SizeVariant.Small => 6, SizeVariant.Medium => Space.S2,
        SizeVariant.Large => Space.S2, SizeVariant.XLarge => 10, _ => Space.S2
    };

    public static float LabelSize(SizeVariant s, Density density = Density.Comfortable) => s switch
    {
        SizeVariant.Small  => density == Density.Compact ? 11.5f : 13,
        SizeVariant.Medium => density == Density.Compact ? 13 : 15,
        SizeVariant.Large  => density == Density.Compact ? 14.5f : 16,
        _                  => density == Density.Compact ? 15.5f : 17,
    };

    public static float Icon(SizeVariant s) => s switch
    {
        SizeVariant.Small => IconSize.Sm, SizeVariant.Medium => IconSize.Dense,
        SizeVariant.Large => IconSize.Dense, SizeVariant.XLarge => IconSize.Md, _ => IconSize.Dense
    };

    public static float Radius(SizeVariant s) =>
        s == SizeVariant.XLarge ? Primitives.Radius.Lg : Primitives.Radius.Md;

    /// <summary>Compact returns the VISUAL height — no 48dp expansion. OPEN QUESTION vs §03
    /// (see tokens.json controlMetrics.openQuestion); do not silently resolve.</summary>
    public static float HitTarget(SizeVariant s, Density density = Density.Comfortable) =>
        density == Density.Compact
            ? Height(s, density)
            : s == SizeVariant.XLarge ? 56 : Touch.MinTarget;
}

/// <summary>A named-tuple view over Sizing — the same seven values in one call.</summary>
public static class ButtonStyles
{
    public static (float Height, float PadX, float Gap, float LabelSize, float IconSize, float Radius, float Hit)
        Metrics(SizeVariant s, Density density = Density.Comfortable) => (
            Sizing.Height(s, density), Sizing.PaddingX(s, density), Sizing.Gap(s), Sizing.LabelSize(s, density),
            Sizing.Icon(s), Sizing.Radius(s), Sizing.HitTarget(s, density));
}

// ─── Theme ───────────────────────────────────────────────────────────────────

// Mode-free by design: no IsDark. A theme that knows the mode would force SSR to know the
// user's preference and break single-render light-dark() output.
public interface IAppTheme
{
    ColorToken Background { get; }
    ColorToken Surface { get; }
    ColorToken SurfaceSubtle { get; }
    ColorToken SurfaceHighlight { get; }
    ColorToken Border { get; }
    ColorToken BorderStrong { get; }

    ColorToken TextPrimary { get; }
    ColorToken TextSecondary { get; }
    ColorToken TextMuted { get; }
    ColorToken TextInverse { get; }

    ColorToken FocusRing { get; }
    ColorToken LinkColor { get; }
    ColorToken Scrim { get; }
    float DisabledOpacity { get; }

    VariantColors Colors(Variant v);
    TypeStyle Type(TypeRole role);
    ShadowSpec Elevation(int level);   // 0–5
    float Shape(ShapeScale scale);
}

public sealed class PhotonTheme : IAppTheme
{
    // Local shorthand for this mirror file only — NOT an SDK member.
    static Color Hex(uint rgb) => new((byte)(rgb >> 16), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
    static ColorToken Pair(uint light, uint dark) => new(Hex(light), Hex(dark));

    public ColorToken Background    => Pair(0xF5F6F8, 0x0C0F13);
    public ColorToken Surface       => Pair(0xFFFFFF, 0x14181E);
    public ColorToken SurfaceSubtle => Pair(0xEFF1F4, 0x1C232B);

    /// <summary>Top-edge highlight over a surface — translucent, so it composites over whatever is beneath.</summary>
    public ColorToken SurfaceHighlight =>
        new(Color.White.WithOpacity(0.55f), Color.White.WithOpacity(0.07f));

    public ColorToken Border       => Pair(0xE2E5EA, 0x2A323D);
    public ColorToken BorderStrong => Pair(0xC9CED6, 0x3D4754);

    public ColorToken TextPrimary   => Pair(0x171B21, 0xF2F4F7);  // 17.3 / 16.2 on Surface
    public ColorToken TextSecondary => Pair(0x4B5563, 0xAEB7C2);  //  7.6 /  8.8
    public ColorToken TextMuted     => Pair(0x5F6B7A, 0x8B95A3);  //  5.4 /  5.9
    public ColorToken TextInverse   => Pair(0xFFFFFF, 0x171B21);

    public ColorToken FocusRing => Pair(0x0050A0, 0x7CB5EE);
    public ColorToken LinkColor => Pair(0x0050A0, 0x7CB5EE);

    public ColorToken Scrim => new(
        Hex(0x0B0E12).WithOpacity(0.40f),
        Color.Black.WithOpacity(0.56f));

    public float DisabledOpacity => 0.38f;

    public float Shape(ShapeScale scale) => scale switch
    {
        ShapeScale.None       => 0,
        ShapeScale.ExtraSmall => Radius.Xs,
        ShapeScale.Small      => Radius.Sm,
        ShapeScale.Medium     => Radius.Md,
        ShapeScale.Large      => Radius.Lg,
        ShapeScale.ExtraLarge => Radius.Xl,
        _                     => Radius.Full
    };

    static readonly VariantColors InfoColors = new(
        Pair(0x0C6C86, 0x4CC3DE), Pair(0xFFFFFF, 0x062B33),
        Pair(0x0A5468, 0x6ED0E6), Pair(0xE4F3F8, 0x0A2A32),
        Pair(0x0A5468, 0x9FDCEB));

    static readonly ColorToken Clear = new(Color.Transparent);

    public VariantColors Colors(Variant v) => v switch
    {
        Variant.Primary => new(
            Pair(0x0050A0, 0x5CA2E8), Pair(0xFFFFFF, 0x06263F),
            Pair(0x00427F, 0x7CB5EE), Pair(0xE8F1FA, 0x0F2740),
            Pair(0x003E7E, 0xA8CDF2)),

        Variant.Secondary => new(
            Pair(0xE9EDF2, 0x242C36), Pair(0x3A4350, 0xD7DDE5),
            Pair(0xDCE2EA, 0x2E3844), Pair(0xE9EDF2, 0x242C36),
            Pair(0x3A4350, 0xD7DDE5)),

        Variant.Destructive => new(
            Pair(0xB42318, 0xE5645C), Pair(0xFFFFFF, 0x3B0704),
            Pair(0x8F1D1D, 0xF28B85), Pair(0xFCEBEA, 0x3A1210),
            Pair(0x8F1D1D, 0xF4A9A4)),

        Variant.Success => new(   // brand green darkened in light mode to clear AA
            Pair(0x3B7A22, 0x85C05E), Pair(0xFFFFFF, 0x12290A),
            Pair(0x2C5E17, 0x9ACD74), Pair(0xEDF6E6, 0x16290C),
            Pair(0x2C5E17, 0xB5DB97)),

        Variant.Warning => new(
            Pair(0x8A5A00, 0xE8B04B), Pair(0xFFFFFF, 0x2E1B02),
            Pair(0x6E4700, 0xF0C06B), Pair(0xFBF3DF, 0x2E2008),
            Pair(0x7A5200, 0xEECF8F)),

        // Photon has no distinct tertiary palette; it reuses Info (the contrasting accent).
        Variant.Info or Variant.Tertiary => InfoColors,

        // Derived variants: transparent fill, text/border tokens, SurfaceSubtle pressed.
        Variant.Outline => new(Clear, TextPrimary, SurfaceSubtle, Clear, TextPrimary),
        Variant.Ghost   => new(Clear, TextPrimary, SurfaceSubtle, Clear, TextPrimary),
        // Link is the audited exception this file's own colour rules name: its Base is INK, used as
        // text on Surface, and Pressed is the darker ink the text swaps to. This line used to put
        // the ink in OnSubtle with Base and Pressed transparent — disagreeing with tokens.json in
        // the same folder, which publishes link.base and link.pressed with contrast figures measured
        // against the BACKGROUND. Nothing fills a Link, so the slots carry ink without painting a box.
        Variant.Link    => new(LinkColor, LinkColor, Pair(0x00427F, 0xA8CDF2), Clear, LinkColor),

        _ => Colors(Variant.Primary)
    };

    public TypeStyle Type(TypeRole role) => role switch
    {
        TypeRole.Display => new(34, 40, FontWeight.ExtraBold, -0.4f, 1.15f),
        TypeRole.Heading => new(28, 34, FontWeight.Bold,      -0.3f, 1.15f),
        TypeRole.Title   => new(20, 26, FontWeight.SemiBold,  -0.2f, 1.25f),
        TypeRole.BodyL   => new(17, 24, FontWeight.Regular,    0.0f, 1.30f),
        TypeRole.BodyM   => new(15, 20, FontWeight.Regular,    0.0f, 1.30f),
        TypeRole.Label   => new(13, 16, FontWeight.SemiBold,   0.1f, 1.30f),
        TypeRole.Caption => new(12, 16, FontWeight.Medium,     0.2f, 1.30f),
        TypeRole.TitleSmall => new(15, 20, FontWeight.Bold,     -0.1f, 1.30f),  // v1.1
        TypeRole.LabelSmall => new(11, 15, FontWeight.Medium,    0.1f, 1.30f),  // v1.1
        TypeRole.Overline   => new(10, 14, FontWeight.ExtraBold, 1.0f, 1.20f),  // v1.1
        _ => new(15, 20, FontWeight.Regular, 0.0f, 1.30f)
    };

    // One analytic rrect shadow per level. Never stack shadows.
    // Dark alphas are explicit SDK values, not an offset; levels 1–2 additionally require a 1dp Border.
    public ShadowSpec Elevation(int level) => level switch
    {
        0 => new(0,  0,  0,  Shadow(0.00f, 0.00f)),
        1 => new(1,  3,  0,  Shadow(0.10f, 0.45f)),
        2 => new(2,  8,  0,  Shadow(0.12f, 0.48f)),
        3 => new(6,  16, -2, Shadow(0.16f, 0.52f)),
        4 => new(12, 28, -4, Shadow(0.20f, 0.56f)),
        _ => new(20, 44, -6, Shadow(0.26f, 0.62f))
    };

    static ColorToken Shadow(float light, float dark) =>
        new(Hex(0x0F1720).WithOpacity(light), Color.Black.WithOpacity(dark));
}
