namespace eQuantic.UI.Primitives;

/// <summary>
/// The IconButton's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): what makes an icon button look like
/// itself and was written in the component, every member OPTIONAL. A member left null is the next step of
/// the slot order: for the side, the slot's own <see cref="Controls"/>, then the theme's
/// <see cref="IAppTheme.Controls"/>, then <see cref="Sizing"/>; for the rest, the component's default.
/// The fills and tints a kind takes from <see cref="IAppTheme.Colors"/> stay there.
/// <para>
/// <em>How does Flutter solve it?</em> <c>IconButtonThemeData</c> and its <c>ButtonStyle</c>: nullable
/// members the component resolves against its defaults.
/// </para>
/// </summary>
public sealed record IconButtonTheme
{
    /// <summary>A ladder for icon buttons alone, ahead of the theme's: the button's side is its height.</summary>
    public ControlsTheme? Controls { get; init; }

    /// <summary>The glyph's size per button size (16, 20, 24 and 24), each on the §07 whitelist (16, 20,
    /// 24 or 32): an icon of any other size throws when it is built.</summary>
    public SizeLadder? IconSize { get; init; }

    /// <summary>The corner radius (the theme's full shape: a circle).</summary>
    public float? Radius { get; init; }

    /// <summary>The outline kind's border width (1).</summary>
    public float? OutlineWidth { get; init; }

    /// <summary>The outline kind's border colour (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? OutlineColor { get; init; }

    /// <summary>The glyph's tint on the quiet kinds, standard and outline, while not selected
    /// (<see cref="IAppTheme.TextSecondary"/>).</summary>
    public ColorToken? GlyphColor { get; init; }

    /// <summary>The fill of the quiet kinds while hovered and pressed (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? QuietFill { get; init; }

    /// <summary>The opacity of the pressed colour over the tonal kind's container (0.24).</summary>
    public float? TonalPressOpacity { get; init; }
}
