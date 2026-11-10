namespace eQuantic.UI.Primitives;

/// <summary>
/// The slot of the selection controls (<c>docs/ELEMENT-TREE-PLAN.md</c> §3.10): Checkbox, RadioGroup,
/// Switch and Slider, every member OPTIONAL and the component's default when null. These controls have
/// no size rung: a checkbox, a radio and a switch vary by density alone, so their sizes are a
/// <see cref="DensityValue"/>, read at <c>context.Density</c>. The accent each takes from
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
    public DensityValue? Box { get; init; }

    /// <summary>The border width of an unchecked box and of the radio's ring (2).</summary>
    public float? BorderWidth { get; init; }

    /// <summary>The border colour of an unchecked box and of an unselected ring
    /// (<see cref="IAppTheme.BorderStrong"/>).</summary>
    public ColorToken? BorderColor { get; init; }

    /// <summary>The checkbox's corner radius (the theme's extra-small shape).</summary>
    public float? CheckboxRadius { get; init; }

    /// <summary>The radio's filled centre (10, 9 compact).</summary>
    public DensityValue? RadioDot { get; init; }

    /// <summary>The height of a radio's row (44).</summary>
    public float? RadioRowHeight { get; init; }

    /// <summary>The gap between a box or a ring and its label (12).</summary>
    public float? LabelGap { get; init; }

    /// <summary>The switch's track width (52, 44 compact).</summary>
    public DensityValue? SwitchWidth { get; init; }

    /// <summary>The switch's track height (32, 26 compact).</summary>
    public DensityValue? SwitchHeight { get; init; }

    /// <summary>The switch's knob (26, 20 compact).</summary>
    public DensityValue? SwitchThumb { get; init; }

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
}
