namespace eQuantic.UI.Primitives;

/// <summary>
/// The theme's control ladder, the slot every sized control reads (<c>docs/ELEMENT-TREE-PLAN.md</c>
/// §3.10): the numbers <see cref="Sizing"/> holds, which no theme reached before, so a theme that changes
/// how tall a control is changes every control of that size at once. Every member OPTIONAL: one left
/// null, or a rung of it, is <see cref="Sizing"/>'s.
/// <para>
/// <em>How does Flutter solve it?</em> <c>ThemeData</c>'s component themes and its
/// <c>VisualDensity</c>: a control's size is the theme's to say, and the density shifts it. Here a
/// value that varies by density holds both sides, picked at <c>context.Density</c>.
/// </para>
/// </summary>
public sealed record ControlsTheme
{
    /// <summary>A control's height per size and density (<see cref="Sizing.Height"/>).</summary>
    public DensityLadder? Height { get; init; }

    /// <summary>The horizontal padding inside a control (<see cref="Sizing.PaddingX"/>).</summary>
    public DensityLadder? PaddingX { get; init; }

    /// <summary>The label's point size (<see cref="Sizing.LabelSize"/>).</summary>
    public DensityLadder? LabelSize { get; init; }

    /// <summary>The gap between a control's icon and its label (<see cref="Sizing.Gap"/>).</summary>
    public SizeLadder? Gap { get; init; }

    /// <summary>The size of a control's icon (<see cref="Sizing.Icon"/>).</summary>
    public SizeLadder? Icon { get; init; }

    /// <summary>A control's corner radius (<see cref="Sizing.Radius"/>).</summary>
    public SizeLadder? Radius { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s ladders laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a nested slot merged the same way. Null
    /// when both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static ControlsTheme? Merge(ControlsTheme? under, ControlsTheme? over) =>
        under is null ? over : over is null ? under : new ControlsTheme
        {
            Height = DensityLadder.Merge(under.Height, over.Height),
            PaddingX = DensityLadder.Merge(under.PaddingX, over.PaddingX),
            LabelSize = DensityLadder.Merge(under.LabelSize, over.LabelSize),
            Gap = SizeLadder.Merge(under.Gap, over.Gap),
            Icon = SizeLadder.Merge(under.Icon, over.Icon),
            Radius = SizeLadder.Merge(under.Radius, over.Radius),
        };
}
