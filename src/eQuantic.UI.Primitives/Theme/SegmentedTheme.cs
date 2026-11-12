namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of the segmented controls (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): SegmentedControl and
/// Stepper, every member OPTIONAL. A member left null is the next step of the slot order: for the
/// height, the padding, the label's size and the radius, the slot's own <see cref="Controls"/>, then the
/// theme's <see cref="IAppTheme.Controls"/>, then <see cref="Sizing"/>; for the rest, the component's
/// default.
/// <para>
/// <em>How does Flutter solve it?</em> <c>SegmentedButtonThemeData</c>: a nullable style the control
/// resolves against its defaults.
/// </para>
/// </summary>
public sealed record SegmentedTheme
{
    /// <summary>A ladder for segmented controls alone, ahead of the theme's.</summary>
    public ControlsTheme? Controls { get; init; }

    /// <summary>The track's lip around the selected segment (3).</summary>
    public float? Inset { get; init; }

    /// <summary>The track's fill (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? TrackFill { get; init; }

    /// <summary>The selected segment's fill (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? ThumbFill { get; init; }

    /// <summary>The selected segment's elevation (1).</summary>
    public int? ThumbElevation { get; init; }

    /// <summary>The width of the segmented control's outline around its track (0, none).</summary>
    public float? TrackBorderWidth { get; init; }

    /// <summary>The colour of the segmented control's outline (<see cref="IAppTheme.BorderStrong"/>),
    /// drawn only when <see cref="TrackBorderWidth"/> is above zero.</summary>
    public ColorToken? TrackBorderColor { get; init; }

    /// <summary>The stepper's border width (1).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>The stepper's border colour (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? BorderColor { get; init; }

    /// <summary>The size of the stepper's minus and plus glyphs (20), on the §07 whitelist (16, 20, 24
    /// or 32): an icon of any other size throws when it is built.</summary>
    public float? IconSize { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a nested slot merged the same way. Null
    /// when both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static SegmentedTheme? Merge(SegmentedTheme? under, SegmentedTheme? over) =>
        under is null ? over : over is null ? under : new SegmentedTheme
        {
            Controls = ControlsTheme.Merge(under.Controls, over.Controls),
            Inset = over.Inset ?? under.Inset,
            TrackFill = over.TrackFill ?? under.TrackFill,
            ThumbFill = over.ThumbFill ?? under.ThumbFill,
            ThumbElevation = over.ThumbElevation ?? under.ThumbElevation,
            TrackBorderWidth = over.TrackBorderWidth ?? under.TrackBorderWidth,
            TrackBorderColor = over.TrackBorderColor ?? under.TrackBorderColor,
            BorderWidth = over.BorderWidth ?? under.BorderWidth,
            BorderColor = over.BorderColor ?? under.BorderColor,
            IconSize = over.IconSize ?? under.IconSize,
        };
}
