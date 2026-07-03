
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
    ColorToken OnSubtle);
