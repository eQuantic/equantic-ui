namespace eQuantic.UI.Primitives;

/// <summary>
/// The Chip's slot (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10), every member OPTIONAL. A member left null
/// is the next step of the slot order: for the height, the slot's own <see cref="Controls"/> at the small
/// rung, then the theme's <see cref="IAppTheme.Controls"/>, then <see cref="Sizing"/>; for the rest, the
/// component's default. The selected and tag tints a kind takes from <see cref="IAppTheme.Colors"/> stay
/// there, and the hover of a filter chip is still the midpoint of its fill toward the border.
/// <para>
/// <em>How does Flutter solve it?</em> <c>ChipThemeData</c>: the chip's padding, shape, label style and
/// colours, nullable, on <c>ThemeData</c>.
/// </para>
/// </summary>
public sealed record ChipTheme
{
    /// <summary>A ladder for chips alone, ahead of the theme's: a chip is as tall as a small control.</summary>
    public ControlsTheme? Controls { get; init; }

    /// <summary>The horizontal padding (12).</summary>
    public float? PaddingX { get; init; }

    /// <summary>The gap between the glyphs and the label (6).</summary>
    public float? Gap { get; init; }

    /// <summary>The label's point size (13).</summary>
    public float? LabelSize { get; init; }

    /// <summary>The label's line height (16).</summary>
    public float? LabelLineHeight { get; init; }

    /// <summary>The label's weight (semi-bold).</summary>
    public FontWeight? LabelWeight { get; init; }

    /// <summary>The label's tracking (0).</summary>
    public float? LabelTracking { get; init; }

    /// <summary>The corner radius (the theme's full shape: a pill).</summary>
    public float? Radius { get; init; }

    /// <summary>The fill of an unselected filter chip and of an input or suggestion chip
    /// (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? Fill { get; init; }

    /// <summary>A selected filter chip's border width (1).</summary>
    public float? SelectedBorderWidth { get; init; }

    /// <summary>The opacity of the pressed colour over a selected filter chip (0.24).</summary>
    public float? PressOpacity { get; init; }

    /// <summary>The size of a selected filter chip's check (16), a size on the §07 whitelist (16, 20, 24
    /// or 32): an icon of any other size throws when it is built.</summary>
    public float? IconSize { get; init; }

    /// <summary>The size of an input chip's remove glyph (20), on the §07 whitelist.</summary>
    public float? RemoveIconSize { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a nested slot merged the same way. Null
    /// when both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static ChipTheme? Merge(ChipTheme? under, ChipTheme? over) =>
        under is null ? over : over is null ? under : new ChipTheme
        {
            Controls = ControlsTheme.Merge(under.Controls, over.Controls),
            PaddingX = over.PaddingX ?? under.PaddingX,
            Gap = over.Gap ?? under.Gap,
            LabelSize = over.LabelSize ?? under.LabelSize,
            LabelLineHeight = over.LabelLineHeight ?? under.LabelLineHeight,
            LabelWeight = over.LabelWeight ?? under.LabelWeight,
            LabelTracking = over.LabelTracking ?? under.LabelTracking,
            Radius = over.Radius ?? under.Radius,
            Fill = over.Fill ?? under.Fill,
            SelectedBorderWidth = over.SelectedBorderWidth ?? under.SelectedBorderWidth,
            PressOpacity = over.PressOpacity ?? under.PressOpacity,
            IconSize = over.IconSize ?? under.IconSize,
            RemoveIconSize = over.RemoveIconSize ?? under.RemoveIconSize,
        };
}
