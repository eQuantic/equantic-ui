namespace eQuantic.UI.Primitives;

/// <summary>Interactive variants — conceptual parity with the web SDK's Variant set (spec §01/§09).</summary>
public enum Variant : byte
{
    Primary = 0,
    Secondary = 1,
    Destructive = 2,
    Outline = 3,
    Ghost = 4,
    Link = 5,
    Success = 6,
    Warning = 7,
    Info = 8,
    /// <summary>The M3 tertiary role — a contrasting accent for balance. Photon tones it from Info.</summary>
    Tertiary = 9,
}

/// <summary>
/// Corner-shape scale — a semantic ladder (M3's shape scale) themes resolve to concrete radii, so
/// SHAPE is theme-driven: Photon returns its own radii, Material returns the M3 shape values, a brand
/// theme returns whatever it likes. Components request a role, never a hardcoded dp.
/// </summary>
public enum ShapeScale : byte
{
    None = 0,
    ExtraSmall = 1,
    Small = 2,
    Medium = 3,
    Large = 4,
    ExtraLarge = 5,
    /// <summary>Fully rounded (pills, avatars) — engine-clamped to min(w,h)/2.</summary>
    Full = 6,
}

/// <summary>
/// Control size tiers — heights 32 · 40 · 48 · 56 dp (spec §09). Named <c>SizeVariant</c> (not the
/// design sketch's <c>Size</c>) for parity with the web SDK and because <c>Size</c> is the engine's
/// geometry struct.
/// </summary>
public enum SizeVariant : byte
{
    Small = 0,
    Medium = 1,
    Large = 2,
    XLarge = 3,
}

/// <summary>
/// The Photon theme contract (spec §09 — conceptual parity with the web <c>IAppTheme</c>): every color
/// is a light/dark <see cref="ColorToken"/>, resolution happens once per build pass, and resolved styles
/// are plain structs on the primitive tree (zero allocation in steady state, plan D9). No CSS anywhere
/// (plan D11) — <c>StyleBuilder</c>/class-strings remain web-only.
/// </summary>
public interface IAppTheme
{
    // Surfaces & structure
    ColorToken Background { get; }
    ColorToken Surface { get; }
    ColorToken SurfaceSubtle { get; }
    /// <summary>The moving glint of the Skeleton shimmer (spec B16) — translucent white over
    /// SurfaceSubtle in both modes; decorative, no contrast requirement.</summary>
    ColorToken SurfaceHighlight { get; }
    ColorToken Border { get; }
    ColorToken BorderStrong { get; }

    // Text tiers
    ColorToken TextPrimary { get; }
    ColorToken TextSecondary { get; }
    ColorToken TextMuted { get; }
    ColorToken TextInverse { get; }

    // Interaction chrome
    ColorToken FocusRing { get; }
    ColorToken LinkColor { get; }
    ColorToken Scrim { get; }

    /// <summary>
    /// The five sub-tokens for a variant. Filled variants come from the palette; Outline / Ghost / Link
    /// are DERIVED (spec §01): transparent fill + Text/Border tokens, pressed = SurfaceSubtle
    /// (Link: pressed swaps the TEXT to Primary.Pressed — fill stays transparent).
    /// </summary>
    VariantColors Colors(Variant variant);

    /// <summary>The type scale row for a role (spec §02).</summary>
    TypeStyle Type(TypeRole role);

    /// <summary>Elevation level 0–5 (spec §05). Level 0 is none (+1dp Border); dark E1–E2 ALSO require a 1dp border.</summary>
    ShadowSpec Elevation(int level);

    /// <summary>The corner radius (dp) for a shape role — SHAPE is theme-driven (M3 customizes it).</summary>
    float Shape(ShapeScale scale);

    /// <summary>Disabled is not a color pair: a 38% opacity group over the resolved style (spec §01).</summary>
    float DisabledOpacity { get; }

    /// <summary>
    /// The colour of a code token. DEFAULTED from the palette the theme already has — a design
    /// system that ships one set of variants should not have to invent a second one to show code,
    /// and a theme that wants a real syntax palette overrides this and nothing else.
    /// <para>
    /// The mapping is chosen for READING, not for decoration: comments recede to the muted tier,
    /// strings and numbers take the two calm variants (success, warning), keywords take the primary
    /// accent, and the punctuation that only separates stays quieter than the code it separates.
    /// </para>
    /// </summary>
    ColorToken Code(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => Colors(Variant.Primary).Base,
        CodeTokenKind.Type => Colors(Variant.Info).Base,
        CodeTokenKind.String => Colors(Variant.Success).Base,
        CodeTokenKind.Number => Colors(Variant.Warning).Base,
        CodeTokenKind.Comment => TextMuted,
        CodeTokenKind.Function => Colors(Variant.Tertiary).Base,
        CodeTokenKind.Attribute => Colors(Variant.Warning).OnSubtle,
        CodeTokenKind.Property => Colors(Variant.Info).OnSubtle,
        CodeTokenKind.Constant => Colors(Variant.Destructive).Base,
        CodeTokenKind.Operator => TextSecondary,
        CodeTokenKind.Punctuation => TextSecondary,
        _ => TextPrimary,
    };
}
