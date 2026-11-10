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

    /// <summary>The stepper's border width (1).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>The stepper's border colour (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? BorderColor { get; init; }

    /// <summary>The size of the stepper's minus and plus glyphs (20).</summary>
    public float? IconSize { get; init; }
}
