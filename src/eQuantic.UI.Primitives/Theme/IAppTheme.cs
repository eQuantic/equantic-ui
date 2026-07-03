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

    /// <summary>Disabled is not a color pair: a 38% opacity group over the resolved style (spec §01).</summary>
    float DisabledOpacity { get; }
}
