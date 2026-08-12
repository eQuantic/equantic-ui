
namespace eQuantic.UI.Primitives;

/// <summary>Active theme mode. Every <see cref="ColorToken"/> carries both; resolution is a field pick.</summary>
public enum ThemeMode : byte
{
    Light = 0,
    Dark = 1,
}

/// <summary>
/// A paired light/dark color (design spec §01) — components never hold raw colors, only tokens,
/// resolved once per build pass by the active <see cref="ThemeMode"/>.
/// </summary>
public readonly record struct ColorToken(Color Light, Color Dark)
{
    /// <summary>Same color in both modes.</summary>
    public ColorToken(Color both) : this(both, both) { }

    public Color Resolve(ThemeMode mode) => mode == ThemeMode.Dark ? Dark : Light;

    /// <summary>Both modes with alpha scaled by <paramref name="opacity"/> — e.g. the disabled 38% group
    /// applied at the token level by components that author mode-free trees.</summary>
    public ColorToken WithOpacity(float opacity) =>
        new(Light.WithOpacity(opacity), Dark.WithOpacity(opacity));

    /// <summary>Per-leg channel midpoint with <paramref name="other"/> — the §10 hover derivation
    /// ("hover fill = midpoint of Base → Pressed"), the same color the interim
    /// <c>color-mix(in srgb, A 50%, B)</c> produced, now computed once at the token level so both
    /// realizers paint the identical value.</summary>
    public ColorToken MidpointWith(ColorToken other) =>
        new(Light.MidpointWith(other.Light), Dark.MidpointWith(other.Dark));
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
