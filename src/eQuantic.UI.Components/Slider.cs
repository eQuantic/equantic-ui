using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// A value picked from a CONTINUOUS range (spec B13) — brightness, a budget, a threshold — where the
/// exact number matters less than where it sits between the ends. When the number is what the user
/// is really after, a <see cref="Stepper"/> or a typed field is the better control. CONTROLLED: the
/// caller owns <see cref="Value"/>.
/// <para>
/// The track is filled up to the value, so the proportion reads without decoding a thumb position,
/// and the ends are labelled by the caller rather than by tick text nobody can hit. Pressing the
/// track moves ONE step toward the press — the scrollbar's page-click, which is what a track press
/// means everywhere else — and dragging anywhere on it SCRUBS continuously, relative to where the
/// value already is, so grabbing the thumb never makes it jump out from under the finger.
/// </para>
/// <para>
/// V1 FENCES — two things spec C7 asks for that this does not do, each because it belongs to
/// something larger than a component:
/// </para>
/// <list type="bullet">
/// <item><b>The first 12dp of a drag are swallowed.</b> The handoff asks for immediate capture on
/// the thumb; <see cref="Draggable"/> arms after <c>Touch.PressCancelSlop</c>, which is what stops
/// a sideways swipe hijacking a vertical scroll. A slider that opts out needs the slop to become a
/// property of the gesture, and that is the DRAGGABLE's decision to expose.</item>
/// <item><b>No detent dots and no value bubble.</b> Both are painted geometry over a track that is
/// SPLIT at the thumb into two flex halves, so neither can be placed without the pixel width the
/// component deliberately does not have — they join whatever gives a component its own measure.
/// </item>
/// </list>
/// </summary>
public sealed class Slider : StatelessComponent
{
    /// <summary>Track thickness; the thumb is the target, the track is the readout.</summary>
    private const float TrackHeight = 4;

    /// <summary>
    /// Spec C7: 24dp, and the figure is a TARGET rather than a decoration — the thumb is what a
    /// finger goes for, and the track around it is only the readout. It was 20 with a 2dp border,
    /// which read as a smaller knob inside a heavier ring; the handoff asks for a bigger knob with
    /// a hairline (<see cref="ThumbBorder"/>).
    /// </summary>
    private const float ThumbSize = 24;

    /// <summary>Spec C7: 1dp. A 2dp ring on a 20dp knob was two deviations compounding — the
    /// border grew as the thumb shrank, so the white face was 16dp across instead of 22.</summary>
    private const float ThumbBorder = 1;

    public Slider(float value, Action<float>? onChanged = null)
    {
        Value = value;
        OnChanged = onChanged;
    }

    public float Value { get; init; }
    public Action<float>? OnChanged { get; init; }

    public float Min { get; init; }
    public float Max { get; init; } = 1;

    /// <summary>Granularity of one track press. 0 = a tenth of the range.</summary>
    public float Step { get; init; }

    public bool Disabled { get; init; }
    public Variant Variant { get; init; } = Variant.Primary;

    /// <summary>Announced by the control — "Brightness", not "0.4".</summary>
    public string Label { get; init; } = "";

    /// <summary>
    /// The value IN WORDS, when the number is not what a person would say — "R$ 400", "40%",
    /// "Large" (spec C7: valuetext for units). A reader says this INSTEAD of the number, so the
    /// caller that owns <see cref="Value"/> owns its wording too: this control is CONTROLLED, and
    /// only the app knows whether 0.4 is a ratio, a currency or the fourth of six named steps.
    /// <para>Null announces the number, which is right for a bare ratio and wrong for anything with
    /// a unit — the announcement is the one place a slider's units can exist at all, since the ends
    /// are labelled by the caller outside the control.</para>
    /// </summary>
    public string? ValueText { get; init; }

    public override VisualNode Build(ComponentContext context)
    {
        var theme = context.Theme;
        var span = Max - Min;
        var fraction = span <= 0 ? 0f : Math.Clamp((Value - Min) / span, 0f, 1f);
        // What the control ACTUALLY holds, which is where the thumb is — `fraction` clamps, so the
        // announcement has to clamp with it. A caller passing 99 into a 0..10 slider draws a thumb
        // at the end; announcing the raw 99 would put aria-valuenow outside the aria-valuemax
        // beside it (invalid ARIA on its own) and make the pixels and the words disagree about the
        // same control. A collapsed range has one value and it is Min.
        var announced = span <= 0 ? Min : Math.Clamp(Value, Min, Max);
        var step = Step > 0 ? Step : span / 10f;
        var accent = theme.Colors(Variant).Base;
        var fill = Disabled ? theme.BorderStrong : accent;

        var thumb = new Box(new BoxStyle
        {
            Width = ThumbSize,
            Height = ThumbSize,
            Background = theme.Surface,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Full)),
            BorderWidth = ThumbBorder,
            // The BORDER token, not the accent. This hairline is the ELEVATION contract's — "dark
            // E1-E2 also require a 1dp border", IAppTheme.Elevation — and its job is to separate a
            // raised surface from the one behind it, which a Primary-blue ring does not do. The
            // variant already reads: it is the filled half of the track. A tinted ring made the
            // knob a second readout of the same fact and the Switch, whose knob is the same
            // surface at the same elevation, carries none.
            BorderColor = theme.Border,
            Elevation = 2,
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        });

        // Proportional layout WITHOUT knowing the pixel width: the two track halves are flex weights.
        // A zero weight would collapse the side entirely, so each end keeps a hairline of presence.
        var row = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Center };

        row.Add(new Flexible(TrackHalf(fill, filled: true, enabled: !Disabled,
            onPressed: () => OnChanged?.Invoke(Math.Max(Min, Value - step))), Weight(fraction)));
        row.Add(thumb);
        // Spec C7: the REST half is SurfaceSubtle — the unfilled rail is a groove, not a border.
        // BorderStrong is the token for a line that DIVIDES, and using it here painted the rail as
        // dark as the outline of a text field, so a slider at 10% read as mostly-full.
        row.Add(new Flexible(TrackHalf(theme.SurfaceSubtle, filled: false, enabled: !Disabled,
            onPressed: () => OnChanged?.Invoke(Math.Min(Max, Value + step))), Weight(1 - fraction)));

        // The gesture is NORMALIZED because the track is fluid: the component cannot know its pixel
        // width and the host can, so it reports a fraction. It does not FOLLOW, either — the value
        // places the thumb through the flex weights above, and translating it as well would move it
        // twice.
        VisualNode surface = Disabled ? row : new Draggable(row)
        {
            Axis = DragAxis.Horizontal,
            Normalized = true,
            Follows = false,
            Min = 0,
            Max = 1,
            RestOffset = fraction,
            OnMoved = f => OnChanged?.Invoke(Quantize(Min + f * span, step)),
        };

        var box = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            // The control's own intrinsic width, exactly why <input type=range> is ~129px in the
            // UA stylesheet: in a shrink-to-fit container a fluid track has no width of its own,
            // and a slider that collapses to its thumb is not a slider.
            MinWidth = 120,
            Height = Touch.MinTarget,
            Opacity = Disabled ? theme.DisabledOpacity : 1f,
        }, surface);

        // ONE Tab stop for the whole control, and the arrows nudge by the same step a track press
        // moves — the keyboard is a third hand on the same knob, not a different control.
        return Disabled
            ? box
            : new Adjustable(box, direction =>
                OnChanged?.Invoke(Quantize(Value + direction * step, step)))
            {
                Label = Label,
                // Spec C7: the value, its bounds and the words for it. Before this the host emitted
                // role="slider" with no aria-valuenow — invalid ARIA, and a screen-reader user heard
                // "Brightness, slider" and never which way it was set.
                Value = new AdjustableValue(announced, Min, Max) { Text = ValueText },
            };
    }

    /// <summary>A scrub lands on the same values a press does — a stepped slider has no in-between
    /// positions, however finely the finger moves.</summary>
    private float Quantize(float value, float step) =>
        Math.Clamp(Step > 0 ? Min + MathF.Round((value - Min) / step) * step : value, Min, Max);

    /// <summary>
    /// Flex weights are integers, so the fraction becomes per-mille — 0.1% granularity, finer than
    /// any pointer. A collapsed side still has to exist, or the thumb would sit off the track.
    /// </summary>
    private static int Weight(float fraction) => Math.Max(1, (int)MathF.Round(fraction * 1000));

    /// <summary>
    /// One side of the track: a hairline bar inside a full-height press target, so the press area is
    /// <see cref="Touch.MinTarget"/> tall while the visible track stays 4dp.
    /// </summary>
    private static VisualNode TrackHalf(ColorToken color, bool filled, bool enabled, Action onPressed)
    {
        var bar = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = TrackHeight,
            Background = color,
            // Only the outer end is rounded — the inner end butts against the thumb.
            CornerRadius = filled
                ? new CornerRadii(TrackHeight / 2, 0, 0, TrackHeight / 2)
                : new CornerRadii(0, TrackHeight / 2, TrackHeight / 2, 0),
            Transition = TransitionSpec.Of(StyleChannels.Colors, Motion.Press),
        });

        var centered = new Column(gap: 0) { Height = SizeValue.Fill, Main = MainAlign.Center };
        centered.Add(bar);

        var target = new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }, centered);
        return enabled
            ? new Pressable(target, onPressed) { Label = filled ? "Decrease" : "Increase" }
            : target;
    }
}
