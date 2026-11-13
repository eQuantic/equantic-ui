
namespace eQuantic.UI.Primitives;

/// <summary>Active theme mode. Every <see cref="ColorToken"/> carries both; resolution is a field pick,
/// with the <see cref="ContrastLevel"/> beside it.</summary>
public enum ThemeMode : byte
{
    Light = 0,
    Dark = 1,
}

/// <summary>
/// A paired light/dark color (design spec §01) — components never hold raw colors, only tokens,
/// resolved once per build pass by the active <see cref="ThemeMode"/> and <see cref="ContrastLevel"/>.
/// <para>
/// A token holds a leg per mode at each contrast level. At <see cref="ContrastLevel.More"/> a leg is
/// the Standard one unless the token states another, so a theme that never heard of contrast levels
/// paints the same at both, and Photon's own palette steps up where the handoff's "More" values say
/// (<c>docs/design/tokens.json</c>, <c>moreLight</c> and <c>moreDark</c>).
/// </para>
/// </summary>
public readonly record struct ColorToken(Color Light, Color Dark)
{
    // Null while the More leg is the Standard one, so a token stated with More equal to Standard
    // and a token stated without it are the same value: equality, hashing and the web's variable
    // map all read the token, never how it was written.
    private readonly Color? _moreLight;
    private readonly Color? _moreDark;

    /// <summary>Same color in both modes.</summary>
    public ColorToken(Color both) : this(both, both) { }

    private ColorToken(Color light, Color dark, Color moreLight, Color moreDark) : this(light, dark)
    {
        _moreLight = moreLight == light ? null : moreLight;
        _moreDark = moreDark == dark ? null : moreDark;
    }

    /// <summary>The light leg at <see cref="ContrastLevel.More"/>: <see cref="Light"/> unless the token
    /// states another.</summary>
    public Color MoreLight => _moreLight ?? Light;

    /// <summary>The dark leg at <see cref="ContrastLevel.More"/>: <see cref="Dark"/> unless the token
    /// states another.</summary>
    public Color MoreDark => _moreDark ?? Dark;

    /// <summary>This token with its legs at <see cref="ContrastLevel.More"/>: the pair a theme states
    /// for the person who turned their system's contrast up. A leg equal to its Standard one is the
    /// Standard one.</summary>
    public ColorToken WithMore(Color light, Color dark) => new(Light, Dark, light, dark);

    /// <summary>Whether either More leg differs from its Standard one: the tokens the web switches
    /// under <c>prefers-contrast: more</c>.</summary>
    public bool DiffersAtMore => _moreLight is not null || _moreDark is not null;

    public Color Resolve(ThemeMode mode, ContrastLevel contrast) => (mode, contrast) switch
    {
        (ThemeMode.Dark, ContrastLevel.More) => MoreDark,
        (ThemeMode.Dark, _) => Dark,
        (_, ContrastLevel.More) => MoreLight,
        _ => Light,
    };

    /// <summary>Every leg with alpha scaled by <paramref name="opacity"/> — e.g. the disabled 38% group
    /// applied at the token level by components that author mode-free trees.</summary>
    public ColorToken WithOpacity(float opacity) =>
        new(Light.WithOpacity(opacity), Dark.WithOpacity(opacity),
            MoreLight.WithOpacity(opacity), MoreDark.WithOpacity(opacity));

    /// <summary>Per-leg channel midpoint with <paramref name="other"/> — the §10 hover derivation
    /// ("hover fill = midpoint of Base → Pressed"), the same color the interim
    /// <c>color-mix(in srgb, A 50%, B)</c> produced, now computed once at the token level so both
    /// realizers paint the identical value. At each contrast level, from that level's legs.</summary>
    public ColorToken MidpointWith(ColorToken other) =>
        new(Light.MidpointWith(other.Light), Dark.MidpointWith(other.Dark),
            MoreLight.MidpointWith(other.MoreLight), MoreDark.MidpointWith(other.MoreDark));
}

/// <summary>
/// The five sub-tokens every interactive variant resolves (spec §01): Base fill, OnBase content,
/// Pressed fill (a REAL token, not an overlay — the engine swaps the fill on the same rrect),
/// Subtle tinted-container fill, and OnSubtle content.
/// </summary>
public readonly record struct VariantColors(
    ColorToken Base,
    ColorToken OnBase,
    ColorToken Pressed,
    ColorToken Subtle,
    ColorToken OnSubtle)
{
    /// <summary>The DERIVED pointer-only hover fill for a filled control (spec §10): the midpoint
    /// of <see cref="Base"/> → <see cref="Pressed"/>, text staying <see cref="OnBase"/>. Derived,
    /// never a sixth slot — the five-tuple is the contract, and quiet variants (Outline/Ghost)
    /// hover on SurfaceSubtle instead, per the same section.</summary>
    public ColorToken Hover => Base.MidpointWith(Pressed);
}
