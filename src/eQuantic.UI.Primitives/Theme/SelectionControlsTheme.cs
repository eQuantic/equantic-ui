namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of the selection controls (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): Checkbox, RadioGroup,
/// Switch and Slider, every member OPTIONAL and the component's default when null. These controls have
/// no size rung: a checkbox, a radio and a switch vary by density alone, so their sizes are a
/// <see cref="DensityPair"/>, read at <c>context.Density</c>. The accent each takes from
/// <see cref="IAppTheme.Colors"/> stays there.
/// <para>
/// <em>How does Flutter solve it?</em> <c>CheckboxThemeData</c>, <c>RadioThemeData</c>,
/// <c>SwitchThemeData</c> and <c>SliderThemeData</c>, one each; one slot holds the four here, as the
/// controls share the box, the border and the label's gap.
/// </para>
/// </summary>
public sealed record SelectionControlsTheme
{
    /// <summary>The checkbox's square and the radio's circle (22, 20 compact).</summary>
    public DensityPair? Box { get; init; }

    /// <summary>The radio's circle where it differs from the checkbox's square, ahead of
    /// <see cref="Box"/> for the radio (Material 3 draws an 18dp checkbox and a 20dp radio).</summary>
    public DensityPair? RadioSize { get; init; }

    /// <summary>The border width of an unchecked box and of the radio's ring (2).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>The border colour of an unchecked box and of an unselected ring
    /// (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? BorderColor { get; init; }

    /// <summary>The checkbox's corner radius (the theme's extra-small shape).</summary>
    public float? CheckboxRadius { get; init; }

    /// <summary>The radio's filled centre (10, 9 compact).</summary>
    public DensityPair? RadioDot { get; init; }

    /// <summary>The height of a radio's row (44).</summary>
    public float? RadioRowHeight { get; init; }

    /// <summary>The gap between a box or a ring and its label (12).</summary>
    public float? LabelGap { get; init; }

    /// <summary>The switch's track width (52, 44 compact).</summary>
    public DensityPair? SwitchWidth { get; init; }

    /// <summary>The switch's track height (32, 26 compact).</summary>
    public DensityPair? SwitchHeight { get; init; }

    /// <summary>The switch's knob (26, 20 compact).</summary>
    public DensityPair? SwitchThumb { get; init; }

    /// <summary>The knob's inset from every edge of the track (3); its travel is the track's slack.</summary>
    public float? SwitchInset { get; init; }

    /// <summary>The track of a switch that is off (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? SwitchOffTrack { get; init; }

    /// <summary>The fill of the switch's knob and of the slider's thumb (<see cref="IAppTheme.Surface"/>).</summary>
    public ColorToken? ThumbFill { get; init; }

    /// <summary>The switch knob's elevation (1).</summary>
    public int? SwitchThumbElevation { get; init; }

    /// <summary>The slider's track height (4).</summary>
    public float? SliderTrackHeight { get; init; }

    /// <summary>The slider's thumb (24).</summary>
    public float? SliderThumbSize { get; init; }

    /// <summary>The slider thumb's hairline (1).</summary>
    public float? SliderThumbBorder { get; init; }

    /// <summary>The slider thumb's hairline colour (<see cref="IAppTheme.Border"/>).</summary>
    public ColorToken? SliderThumbBorderColor { get; init; }

    /// <summary>The slider thumb's elevation (2).</summary>
    public int? SliderThumbElevation { get; init; }

    /// <summary>The slider's track past the thumb (<see cref="IAppTheme.SurfaceSubtle"/>).</summary>
    public ColorToken? SliderRestTrack { get; init; }

    /// <summary>The width a slider never shrinks below (120).</summary>
    public float? SliderMinWidth { get; init; }

    /// <summary>
    /// <paramref name="over"/>'s members laid over <paramref name="under"/>'s: what <paramref name="over"/>
    /// names wins, what it leaves null is <paramref name="under"/>'s, a nested slot merged the same way. Null
    /// when both are. It is how <see cref="AppThemeExtensions.With"/> lays a slot over a theme's.
    /// </summary>
    public static SelectionControlsTheme? Merge(SelectionControlsTheme? under, SelectionControlsTheme? over) =>
        under is null ? over : over is null ? under : new SelectionControlsTheme
        {
            Box = DensityPair.Merge(under.Box, over.Box),
            RadioSize = DensityPair.Merge(under.RadioSize, over.RadioSize),
            BorderWidth = over.BorderWidth ?? under.BorderWidth,
            BorderColor = over.BorderColor ?? under.BorderColor,
            CheckboxRadius = over.CheckboxRadius ?? under.CheckboxRadius,
            RadioDot = DensityPair.Merge(under.RadioDot, over.RadioDot),
            RadioRowHeight = over.RadioRowHeight ?? under.RadioRowHeight,
            LabelGap = over.LabelGap ?? under.LabelGap,
            SwitchWidth = DensityPair.Merge(under.SwitchWidth, over.SwitchWidth),
            SwitchHeight = DensityPair.Merge(under.SwitchHeight, over.SwitchHeight),
            SwitchThumb = DensityPair.Merge(under.SwitchThumb, over.SwitchThumb),
            SwitchInset = over.SwitchInset ?? under.SwitchInset,
            SwitchOffTrack = over.SwitchOffTrack ?? under.SwitchOffTrack,
            ThumbFill = over.ThumbFill ?? under.ThumbFill,
            SwitchThumbElevation = over.SwitchThumbElevation ?? under.SwitchThumbElevation,
            SliderTrackHeight = over.SliderTrackHeight ?? under.SliderTrackHeight,
            SliderThumbSize = over.SliderThumbSize ?? under.SliderThumbSize,
            SliderThumbBorder = over.SliderThumbBorder ?? under.SliderThumbBorder,
            SliderThumbBorderColor = over.SliderThumbBorderColor ?? under.SliderThumbBorderColor,
            SliderThumbElevation = over.SliderThumbElevation ?? under.SliderThumbElevation,
            SliderRestTrack = over.SliderRestTrack ?? under.SliderRestTrack,
            SliderMinWidth = over.SliderMinWidth ?? under.SliderMinWidth,
        };
}
